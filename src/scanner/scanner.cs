using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensorGateway.Bluetooth;
using SensorGateway.Configuration;
using HashtagChris.DotNetBlueZ;
using HashtagChris.DotNetBlueZ.Extensions;

namespace SensorGateway.Gateway
{
    /// <summary>
    /// Singleton device scanner that continuously scans for BLE devices and spawns worker processes
    /// </summary>
    public sealed class Scanner : IDisposable
    {
        private static readonly Lazy<Scanner> _instance = new Lazy<Scanner>(() => new Scanner());
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _backgroundScanningTask;
        private readonly ConcurrentDictionary<string, DateTime> _discoveredDevices;
        private readonly ConcurrentDictionary<string, bool> _devicesInProcess;
        private readonly SemaphoreSlim _scanSemaphore;
        private readonly object _configLock = new object();
        private bool _disposed = false;

        // Configuration for automatic scanning (protected by _configLock)
        private List<string> _devicePrefixes = new List<string> { "DTT-" }; // Default prefixes
        private TimeSpan _scanInterval = TimeSpan.FromMinutes(5); // Scan every 5 minutes
        private TimeSpan _scanDuration = TimeSpan.FromSeconds(30); // Each scan lasts 30 seconds
        private volatile bool _autoScanEnabled = true; // Enable auto-scanning

        /// <summary>
        /// Gets the singleton instance of the Scanner
        /// </summary>
        public static Scanner Instance => _instance.Value;

        /// <summary>
        /// Event fired when a new device is discovered
        /// </summary>
        public event EventHandler<DeviceDiscoveredEventArgs>? DeviceDiscovered;

        /// <summary>
        /// Event fired when a scan operation starts
        /// </summary>
        public event EventHandler<ScanStartedEventArgs>? ScanStarted;

        /// <summary>
        /// Event fired when a scan operation completes
        /// </summary>
        public event EventHandler<ScanCompletedEventArgs>? ScanCompleted;

        private Scanner()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _discoveredDevices = new ConcurrentDictionary<string, DateTime>();
            _devicesInProcess = new ConcurrentDictionary<string, bool>();
            _scanSemaphore = new SemaphoreSlim(1, 1);

            // Start the background scanning thread
            _backgroundScanningTask = Task.Run(BackgroundScanningLoop, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Configures the automatic scanning behavior
        /// </summary>
        /// <param name="devicePrefixes">Device prefixes to scan for</param>
        /// <param name="scanInterval">Time between scans</param>
        /// <param name="scanDuration">Duration of each scan</param>
        /// <param name="enabled">Whether auto-scanning is enabled</param>
        public void ConfigureAutoScan(IEnumerable<string> devicePrefixes, TimeSpan scanInterval, TimeSpan scanDuration, bool enabled = true)
        {
            lock (_configLock)
            {
                _devicePrefixes = devicePrefixes?.ToList() ?? new List<string>();
                _scanInterval = scanInterval;
                _scanDuration = scanDuration;
                _autoScanEnabled = enabled;
                
                var prefixString = string.Join(", ", _devicePrefixes.Select(p => $"'{p}'"));
                Console.WriteLine($"📋 Auto-scan configured: Prefixes={prefixString}, Interval={_scanInterval.TotalMinutes}min, Duration={_scanDuration.TotalSeconds}s, Enabled={_autoScanEnabled}");
            }
        }

        /// <summary>
        /// Gets the current auto-scan configuration (thread-safe)
        /// </summary>
        /// <returns>Tuple containing current configuration</returns>
        private (List<string> prefixes, TimeSpan interval, TimeSpan duration, bool enabled) GetAutoScanConfig()
        {
            lock (_configLock)
            {
                return (_devicePrefixes.ToList(), _scanInterval, _scanDuration, _autoScanEnabled);
            }
        }

        /// <summary>
        /// Starts automatic scanning (if not already started)
        /// </summary>
        public void StartAutoScan()
        {
            _autoScanEnabled = true;
            Console.WriteLine("▶️ Auto-scanning enabled");
        }

        /// <summary>
        /// Stops automatic scanning
        /// </summary>
        public void StopAutoScan()
        {
            _autoScanEnabled = false;
            Console.WriteLine("⏸️ Auto-scanning disabled");
        }

        /// <summary>
        /// Manually triggers a scan (in addition to automatic scans)
        /// </summary>
        /// <param name="devicePrefixes">Collection of device name prefixes to filter for</param>
        /// <param name="scanDuration">Duration to scan for devices</param>
        /// <param name="cancellationToken">Cancellation token for the scan operation</param>
        /// <returns>Number of devices found during the scan</returns>
        public async Task<int> ScanForDevicesAsync(IEnumerable<string> devicePrefixes, TimeSpan scanDuration, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Scanner));

            var prefixList = devicePrefixes?.ToList() ?? throw new ArgumentNullException(nameof(devicePrefixes));
            if (!prefixList.Any())
                throw new ArgumentException("At least one device prefix must be provided", nameof(devicePrefixes));

            await _scanSemaphore.WaitAsync(cancellationToken);
            try
            {
                return await PerformScanAsync(prefixList, scanDuration, cancellationToken, isManualScan: true);
            }
            finally
            {
                _scanSemaphore.Release();
            }
        }

        /// <summary>
        /// Manually triggers a scan with a single prefix (convenience method)
        /// </summary>
        /// <param name="devicePrefix">Device name prefix to filter for</param>
        /// <param name="scanDuration">Duration to scan for devices</param>
        /// <param name="cancellationToken">Cancellation token for the scan operation</param>
        /// <returns>Number of devices found during the scan</returns>
        public async Task<int> ScanForDevicesAsync(string devicePrefix, TimeSpan scanDuration, CancellationToken cancellationToken = default)
        {
            return await ScanForDevicesAsync(new[] { devicePrefix }, scanDuration, cancellationToken);
        }

        /// <summary>
        /// Performs the actual scanning logic (used by both manual and automatic scans)
        /// </summary>
        private async Task<int> PerformScanAsync(IList<string> prefixList, TimeSpan scanDuration, CancellationToken cancellationToken, bool isManualScan = false)
        {
            var prefixString = string.Join(", ", prefixList.Select(p => $"'{p}'"));
            var scanType = isManualScan ? "Manual" : "Auto";
            Console.WriteLine($"🔍 {scanType} scan starting for devices with prefixes {prefixString} for {scanDuration.TotalSeconds} seconds...");
            
            // Thread-safe event invocation
            InvokeEventAsync(ScanStarted, new ScanStartedEventArgs(prefixList, scanDuration));

            var adapter = await BlueZManager.GetAdapterAsync(AppConfig.Bluetooth.AdapterName);
            var discoveredCount = 0;
            var scanEndTime = DateTime.Now.Add(scanDuration);

            // Get a token for this scan operation
            // Start discovery
            using var token = await BTManager.Instance.GetTokenAsync(TimeSpan.FromSeconds(60), cancellationToken);
            await adapter.StartDiscoveryAsync();

            //BTToken? token = null;
            try
            {
                // This ensures we don't exceed the maximum concurrent operations
                // Note: This is a blocking call that will wait until a token is available
                // If this times out, it means too many scans are running concurrently
                // This is important to prevent overwhelming the Bluetooth stack
                // The use of `using` ensures the token is returned to the pool automatically
               
                // Scan for the specified duration
                while (DateTime.Now < scanEndTime && !cancellationToken.IsCancellationRequested)
                {
                    var devices = await adapter.GetDevicesAsync();

                    foreach (var device in devices)
                    {
                        try
                        {
                            var deviceName = await device.GetAliasAsync();
                            var deviceAddress = await device.GetAddressAsync();
                            var mData = await Device1Extensions.GetManufacturerDataAsync(device);

                            // Check if device matches any prefix and should be processed
                            if (!string.IsNullOrEmpty(deviceName) &&
                                MatchesAnyPrefix(deviceName, prefixList) &&
                                TryMarkDeviceForProcessing(deviceAddress))
                            {
                                var matchedPrefix = GetMatchedPrefix(deviceName, prefixList);
                                Console.WriteLine($"📱 Found device: {deviceName} ({deviceAddress}) [matched prefix: {matchedPrefix}]");

                                discoveredCount++;

                                // Thread-safe event invocation
                                var deviceDiscoveredArgs = new DeviceDiscoveredEventArgs(device, deviceName, deviceAddress, matchedPrefix);
                                InvokeEventAsync(DeviceDiscovered, deviceDiscoveredArgs);

                                // Start worker process without waiting
                                _ = Task.Run(() => ProcessDeviceAsync(device, cancellationToken), cancellationToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Error processing device: {ex.Message}");
                        }
                    }

                    // Short delay before next scan iteration
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (TimeoutException exT)
            {
                Console.WriteLine($"⏰ Scan timed out: {exT.Message}");
            }
            catch (OperationCanceledException exC)
            {
                Console.WriteLine($"🚫 Scan cancelled: {exC.Message}");
            }
            catch (ObjectDisposedException exO)
            {
                Console.WriteLine($"🗑️ Scanner disposed during scan: {exO.Message}");
            }
            catch(InvalidOperationException exIO)
            {
                Console.WriteLine($"⚠️ Invalid operation during scan: {exIO.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during scan: {ex.Message}");
            }
            finally
            {
                // Stop discovery
                await adapter.StopDiscoveryAsync();
            }

            Console.WriteLine($"✅ {scanType} scan completed. Found {discoveredCount} devices matching prefixes {prefixString}");
            
            // Thread-safe event invocation
            InvokeEventAsync(ScanCompleted, new ScanCompletedEventArgs(prefixList, discoveredCount, scanDuration));

            return discoveredCount;
        }

        /// <summary>
        /// Background scanning loop that runs continuously and performs automatic scans
        /// </summary>
        private async Task BackgroundScanningLoop()
        {
            Console.WriteLine("🚀 Scanner background thread started with auto-scanning enabled");
            
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Get current configuration thread-safely
                        var (prefixes, interval, duration, enabled) = GetAutoScanConfig();
                        
                        // Perform automatic scan if enabled and prefixes are configured
                        if (enabled && prefixes.Any())
                        {
                            // Wait for scan semaphore to be available (don't block manual scans)
                            if (await _scanSemaphore.WaitAsync(1000, _cancellationTokenSource.Token))
                            {
                                try
                                {
                                    await PerformScanAsync(prefixes, duration, _cancellationTokenSource.Token, isManualScan: false);
                                }
                                finally
                                {
                                    _scanSemaphore.Release();
                                }
                            }
                            else
                            {
                                Console.WriteLine("⏭️ Skipping auto-scan (manual scan in progress)");
                            }
                        }

                        // Clean up old discovered devices
                        CleanupOldDevices();
                        
                        // Wait for next scan interval
                        await Task.Delay(interval, _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when shutting down
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error in auto-scan: {ex.Message}");
                        // Wait a bit before retrying after an error
                        await Task.Delay(TimeSpan.FromMinutes(1), _cancellationTokenSource.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down
            }
            
            Console.WriteLine("🛑 Scanner background thread stopped");
        }

        /// <summary>
        /// Atomically checks and marks a device for processing
        /// </summary>
        /// <param name="deviceAddress">Device address to check and mark</param>
        /// <returns>True if device should be processed (and was marked), false otherwise</returns>
        private bool TryMarkDeviceForProcessing(string deviceAddress)
        {
            var now = DateTime.Now;
            
            // Check if device is currently being processed
            if (_devicesInProcess.ContainsKey(deviceAddress))
            {
                return false; // Skip devices currently in process
            }
            
            // Check if device was processed recently (within 5 minutes)
            if (_discoveredDevices.TryGetValue(deviceAddress, out var lastProcessed))
            {
                if (now - lastProcessed <= TimeSpan.FromMinutes(5))
                {
                    return false; // Too recent, skip
                }
            }
            
            // Atomically try to mark as discovered and in process
            var wasUpdated = false;
            _discoveredDevices.AddOrUpdate(
                deviceAddress,
                now, // Add new entry
                (key, existingTime) => 
                {
                    if (now - existingTime > TimeSpan.FromMinutes(5))
                    {
                        wasUpdated = true;
                        return now;
                    }
                    return existingTime;
                }
            );
            
            // If this is a new entry, wasUpdated will be false but we should process
            var isNewEntry = !_discoveredDevices.ContainsKey(deviceAddress) || wasUpdated;
            
            if (isNewEntry || wasUpdated)
            {
                // Mark as in process
                _devicesInProcess.TryAdd(deviceAddress, true);
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Thread-safe event invocation that doesn't block the scanning thread
        /// </summary>
        /// <typeparam name="T">Event args type</typeparam>
        /// <param name="eventHandler">Event handler to invoke</param>
        /// <param name="args">Event arguments</param>
        private void InvokeEventAsync<T>(EventHandler<T>? eventHandler, T args) where T : EventArgs
        {
            var handler = eventHandler;
            if (handler != null)
            {
                // Invoke on thread pool to avoid blocking scan thread
                _ = Task.Run(() =>
                {
                    try
                    {
                        handler.Invoke(this, args);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error in event handler: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// Checks if a device name matches any of the provided prefixes
        /// </summary>
        /// <param name="deviceName">Device name to check</param>
        /// <param name="prefixes">List of prefixes to match against</param>
        /// <returns>True if device name matches any prefix, false otherwise</returns>
        private static bool MatchesAnyPrefix(string deviceName, IList<string> prefixes)
        {
            return prefixes.Any(prefix => deviceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the first prefix that matches the device name
        /// </summary>
        /// <param name="deviceName">Device name to check</param>
        /// <param name="prefixes">List of prefixes to match against</param>
        /// <returns>The matched prefix, or empty string if no match</returns>
        private static string GetMatchedPrefix(string deviceName, IList<string> prefixes)
        {
            return prefixes.FirstOrDefault(prefix => deviceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        }

        /// <summary>
        /// Removes old discovered devices from tracking
        /// </summary>
        private void CleanupOldDevices()
        {
            var cutoffTime = DateTime.Now.AddHours(-1);
            var oldDevices = _discoveredDevices
                .Where(kvp => kvp.Value < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var deviceAddress in oldDevices)
            {
                _discoveredDevices.TryRemove(deviceAddress, out _);
                // Don't remove from _devicesInProcess here - let the worker handle that
            }

            if (oldDevices.Count > 0)
            {
                Console.WriteLine($"🧹 Cleaned up {oldDevices.Count} old device entries");
            }
        }

        /// <summary>
        /// Processes a discovered device (worker process implementation)
        /// </summary>
        /// <param name="device">The discovered device</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private async Task ProcessDeviceAsync(Device device, CancellationToken cancellationToken)
        {
            string deviceAddress = "unknown";
            try
            {
                deviceAddress = await device.GetAddressAsync();
                var deviceName = await device.GetAliasAsync();
                
                Console.WriteLine($"⚙️ Worker process started for device: {deviceName} ({deviceAddress})");
                
                // TODO: Implement actual device processing logic
                // This is where you would:
                // 1. Create a sensor using SensorFactory
                // 2. Connect to the device
                // 3. Download measurements
                // 4. Process data
                // 5. Store results
                
                // Simulate some work
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                
                Console.WriteLine($"✅ Worker process completed for device: {deviceName} ({deviceAddress})");
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Worker process failed for device {deviceAddress}: {ex.Message}");
            }
            finally
            {
                // Remove from devices in process when worker completes
                _devicesInProcess.TryRemove(deviceAddress, out _);
            }
        }

        /// <summary>
        /// Disposes the Scanner and stops background operations
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Console.WriteLine("🛑 Shutting down Scanner...");

                // Clear event handlers to prevent memory leaks
                DeviceDiscovered = null;
                ScanStarted = null;
                ScanCompleted = null;        
                        
                _cancellationTokenSource.Cancel();
                
                try
                {
                    _backgroundScanningTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error during Scanner shutdown: {ex.Message}");
                }
                
                _cancellationTokenSource.Dispose();
                _scanSemaphore.Dispose();
                _disposed = true;
                
                Console.WriteLine("✅ Scanner shutdown complete");
            }
        }
    }

    /// <summary>
    /// Event arguments for device discovery events
    /// </summary>
    public class DeviceDiscoveredEventArgs : EventArgs
    {
        public Device Device { get; }
        public string DeviceName { get; }
        public string DeviceAddress { get; }
        public string MatchedPrefix { get; }
        public DateTime DiscoveredAt { get; }

        public DeviceDiscoveredEventArgs(Device device, string deviceName, string deviceAddress, string matchedPrefix)
        {
            Device = device;
            DeviceName = deviceName;
            DeviceAddress = deviceAddress;
            MatchedPrefix = matchedPrefix;
            DiscoveredAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Event arguments for scan started events
    /// </summary>
    public class ScanStartedEventArgs : EventArgs
    {
        public IReadOnlyList<string> DevicePrefixes { get; }
        public TimeSpan ScanDuration { get; }
        public DateTime StartedAt { get; }

        public ScanStartedEventArgs(IEnumerable<string> devicePrefixes, TimeSpan scanDuration)
        {
            DevicePrefixes = devicePrefixes.ToList().AsReadOnly();
            ScanDuration = scanDuration;
            StartedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Event arguments for scan completed events
    /// </summary>
    public class ScanCompletedEventArgs : EventArgs
    {
        public IReadOnlyList<string> DevicePrefixes { get; }
        public int DevicesFound { get; }
        public TimeSpan ScanDuration { get; }
        public DateTime CompletedAt { get; }

        public ScanCompletedEventArgs(IEnumerable<string> devicePrefixes, int devicesFound, TimeSpan scanDuration)
        {
            DevicePrefixes = devicePrefixes.ToList().AsReadOnly();
            DevicesFound = devicesFound;
            ScanDuration = scanDuration;
            CompletedAt = DateTime.Now;
        }
    }
}