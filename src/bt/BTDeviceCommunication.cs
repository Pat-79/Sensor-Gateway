using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensorGateway.Configuration;
using HashtagChris.DotNetBlueZ;
using HashtagChris.DotNetBlueZ.Extensions;

namespace SensorGateway.Bluetooth
{
    /// <summary>
    /// Handles Bluetooth device communication protocols and characteristic operations.
    /// Follows Single Responsibility Principle by focusing solely on BLE communication.
    /// </summary>
    public class BTDeviceCommunication
    {
        #region Private Communication Fields
        private GattCharacteristic? _commandChar = null;
        private GattCharacteristic? _responseChar = null;
        private bool _communicationInProgress = false;

        // Notification waiting fields
        private readonly ManualResetEventSlim _notificationReceived = new ManualResetEventSlim(false);
        private bool _waitingForNotification = false;

        // Dependencies
        private readonly Func<HashtagChris.DotNetBlueZ.IGattService1?> _getCurrentService;
        private readonly Func<HashtagChris.DotNetBlueZ.IGattService1, string, Task<GattCharacteristic?>> _getCharacteristicAsync;
        private readonly Func<Task> _clearBufferAsync;
        private readonly Func<byte[], Task> _appendToBufferAsync;
        private readonly Func<Task<bool>> _isConnectedAsync;
        private readonly Func<Task<bool>> _connectAsync;
        private readonly Action<string, byte[]> _onNotificationDataReceived;
        #endregion

        #region Events
        public delegate void NotificationDataReceivedHandler(object sender, byte[]? data, string uuid);

        /// <summary>
        /// Event triggered when new notification data is received from the device.
        /// This event allows subscribers to handle incoming data asynchronously.
        /// </summary>
        public event NotificationDataReceivedHandler? NotificationDataReceived;
        #endregion

        #region Properties
        /// <summary>
        /// Gets whether communication is currently in progress.
        /// </summary>
        public bool CommunicationInProgress => _communicationInProgress;

        /// <summary>
        /// Gets the current command characteristic.
        /// </summary>
        public GattCharacteristic? CommandCharacteristic => _commandChar;

        /// <summary>
        /// Gets the current response characteristic.
        /// </summary>
        public GattCharacteristic? ResponseCharacteristic => _responseChar;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of BTDeviceCommunication with necessary dependencies.
        /// </summary>
        /// <param name="getCurrentService">Function to get the current service</param>
        /// <param name="getCharacteristicAsync">Function to get a characteristic from a service</param>
        /// <param name="clearBufferAsync">Function to clear the device buffer</param>
        /// <param name="appendToBufferAsync">Function to append data to buffer</param>
        /// <param name="isConnectedAsync">Function to check connection status</param>
        /// <param name="connectAsync">Function to connect to device</param>
        /// <param name="onNotificationDataReceived">Action to handle notification events</param>
        public BTDeviceCommunication(
            Func<HashtagChris.DotNetBlueZ.IGattService1?> getCurrentService,
            Func<HashtagChris.DotNetBlueZ.IGattService1, string, Task<GattCharacteristic?>> getCharacteristicAsync,
            Func<Task> clearBufferAsync,
            Func<byte[], Task> appendToBufferAsync,
            Func<Task<bool>> isConnectedAsync,
            Func<Task<bool>> connectAsync,
            Action<string, byte[]> onNotificationDataReceived)
        {
            _getCurrentService = getCurrentService;
            _getCharacteristicAsync = getCharacteristicAsync;
            _clearBufferAsync = clearBufferAsync;
            _appendToBufferAsync = appendToBufferAsync;
            _isConnectedAsync = isConnectedAsync;
            _connectAsync = connectAsync;
            _onNotificationDataReceived = onNotificationDataReceived;
        }
        #endregion

        #region Communication Methods

        /// <summary>
        /// Sets up BLE notifications for the response characteristic and handles initial data retrieval.
        /// This method subscribes to characteristic value changes and attempts to retrieve any existing data.
        /// Initial data retrieval failure is handled gracefully and won't block the setup process.
        /// </summary>
        /// <param name="characteristicUuid">The UUID of the response characteristic to set up notifications for</param>
        public async Task SetNotificationsAsync(string characteristicUuid)
        {
            // Ensure that we're not interfering with another operation
            if (CommunicationInProgress)
            {
                throw new InvalidOperationException("Communication is already in progress. Please wait for the current operation to complete.");
            }

            // Connect to service and characteristic
            var service = _getCurrentService();
            if (service == null)
            {
                throw new InvalidOperationException($"Service not initialized. Call SetServiceAsync first to initialize the service connection.");
            }

            _responseChar = await _getCharacteristicAsync(service, characteristicUuid);
            if (_responseChar == null)
            {
                throw new InvalidOperationException("Response characteristic not initialized");
            }

            // Subscribe to notifications
            _responseChar.Value += ReceiveNotificationData;

            //byte[] value;
            //try
            //{
            //    Console.WriteLine("Reading current characteristic value...");
            //    value = await _responseChar.GetValueAsync();
            //    await _clearBufferAsync();
            //}
            //catch (Exception ex)
            //{
            //    Console.Error.WriteLine($"Error reading characteristic value: {ex.Message}");
            //    return;
            //}
        }

        /// <summary>
        /// Sets up BLE notifications for the response characteristic and handles initial data retrieval.
        /// This method subscribes to characteristic value changes and attempts to retrieve any existing data.
        /// Initial data retrieval failure is handled gracefully and won't block the setup process.
        /// </summary>
        /// <param name="characteristicUuid">The UUID of the response characteristic to set up notifications for</param>
        public void SetNotifications(string characteristicUuid)
        {
            SetNotificationsAsync(characteristicUuid).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sets the command characteristic for the device, allowing it to send commands to the device.
        /// </summary>
        /// <param name="characteristicUuid">The UUID of the command characteristic to set</param>
        public async Task SetCommandCharacteristicAsync(string characteristicUuid)
        {
            // Ensure that we're not interfering with another operation
            if (CommunicationInProgress)
            {
                throw new InvalidOperationException("Communication is already in progress. Please wait for the current operation to complete.");
            }

            // Connect to service and characteristic
            var service = _getCurrentService();
            if (service == null)
            {
                throw new InvalidOperationException($"Service not initialized. Call SetNotificationsAsync first to initialize the service connection.");
            }

            characteristicUuid = BlueZManager.NormalizeUUID(characteristicUuid);
            _commandChar = await _getCharacteristicAsync(service, characteristicUuid);
            if (_commandChar == null)
            {
                throw new InvalidOperationException($"Command characteristic '{characteristicUuid}' not found in service.");
            }
        }

        /// <summary>
        /// Sets the command characteristic for the device, allowing it to send commands to the device.
        /// </summary>
        /// <param name="characteristicUuid">The UUID of the command characteristic to set</param>
        public void SetCommandCharacteristic(string characteristicUuid)
        {
            SetCommandCharacteristicAsync(characteristicUuid).GetAwaiter().GetResult();
        }

        /// <summary>
        /// WriteWithoutResponse is used to write data tot he device, without a response from the device.
        /// This is a fire-and-forget operation, meaning the call will return before the data has been written.
        /// However, it can optionally wait for notification data to be received before returning.
        /// This method is also known as a "write command" (as opposed to a write request).
        /// </summary>
        /// <param name="data">The data to write to the characteristic</param>
        /// <param name="waitForNotificationDataReceived">If true, waits for notification data to be received before returning</param>
        /// <exception cref="ArgumentException">Thrown when data is null or empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the device is not connected or another communication operation is in progress.
        /// </exception>
        public async Task WriteWithoutResponseAsync(byte[] data, bool waitForNotificationDataReceived = false)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Data cannot be null or empty", nameof(data));
            }

            if (_commandChar == null)
            {
                throw new InvalidOperationException("Command characteristic is not initialized. Please set the command characteristic before writing data.");
            }

            if (!await _isConnectedAsync())
            {
                await _connectAsync();
            }

            if (CommunicationInProgress)
            {
                throw new InvalidOperationException("Communication is already in progress. Please wait for the current operation to complete.");
            }

            // Set communication in progress flag
            _communicationInProgress = true;

            try
            {
                // If waiting for notification data, reset the event and set the waiting flag
                // This allows us to wait for the notification data to be received after writing
                if (waitForNotificationDataReceived)
                {
                    _notificationReceived.Reset();
                    _waitingForNotification = true;
                }


                await _clearBufferAsync();

                // Write the data without artificial delay
                await _commandChar.WriteValueAsync(data, new Dictionary<string, object>());

                if (waitForNotificationDataReceived)
                {
                    // Use proper async waiting with cancellation support
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(BTDeviceConstants.NOTIFICATION_WAIT_TIMEOUT_SECONDS));

                    try
                    {
                        await Task.Run(() =>
                        {
                            while (_waitingForNotification && _communicationInProgress && !cts.Token.IsCancellationRequested)
                            {
                                if (_notificationReceived.Wait(100, cts.Token))
                                    break;
                            }
                        }, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw new TimeoutException("Timed out waiting for notification data");
                    }
                }
            }
            finally
            {
                _waitingForNotification = false;
                // Let StopCommunication() handle _communicationInProgress = false
            }
        }

        /// <summary>
        /// WriteWithoutResponse is used to write data tot he device, without a response from the device.
        /// This is a fire-and-forget operation, meaning the call will return before the data has been written.
        /// However, it can optionally wait for notification data to be received before returning.
        /// This method is also known as a "write command" (as opposed to a write request).
        /// </summary>
        /// <param name="data">The data to write to the characteristic</param>
        /// <param name="waitForNotificationDataReceived">If true, waits for notification data to be received before returning</param>
        /// <exception cref="ArgumentException">Thrown when data is null or empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the device is not connected or another communication operation is in progress.
        /// </exception>
        public void WriteWithoutResponse(byte[] data, bool waitForNotificationDataReceived = false)
        {
            WriteWithoutResponseAsync(data, waitForNotificationDataReceived).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Stops any ongoing communication by resetting the communication flag.
        /// This method also releases any threads waiting for notification data.
        /// </summary>
        public void StopCommunication()
        {
            // Stop any ongoing communication by resetting the communication flag
            _communicationInProgress = false;
            
            // Release any waiting WriteWithoutResponseAsync calls
            if (_waitingForNotification)
            {
                _waitingForNotification = false;
                _notificationReceived.Set(); // Wake up waiting threads
            }
        }

        /// <summary>
        /// Asynchronously starts communication by clearing the buffer and setting the communication flag.
        /// </summary>
        /// <returns>A task that represents the asynchronous communication start operation.</returns>
        public async Task StartCommunicationAsync()
        {
            await _clearBufferAsync();
            _communicationInProgress = true;
        }

        /// <summary>
        /// Synchronously starts communication (compatibility method).
        /// </summary>
        /// <remarks>
        /// This method blocks the calling thread. Consider using StartCommunicationAsync for better performance.
        /// </remarks>
        public void StartCommunication()
        {
            StartCommunicationAsync().GetAwaiter().GetResult();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles incoming notification data from BLE characteristics in a thread-safe manner.
        /// This method processes characteristic value changes, appends data to the internal buffer,
        /// and triggers the NotificationDataReceived event for subscribers.
        /// </summary>
        /// <param name="characteristic">The BLE characteristic that sent the notification.</param>
        /// <param name="e">Event arguments containing the notification data and metadata.</param>
        /// <returns>A task that represents the asynchronous notification processing operation.</returns>
        private async Task ReceiveNotificationData(GattCharacteristic characteristic, GattCharacteristicValueEventArgs e)
        {
            try
            {
                // For high-frequency data processing (1025 bytes from BT510 sensors),
                // use memory pooling to reduce GC pressure during concurrent operations
                if (e.Value?.Length > 100) // Use pooling for larger payloads  
                {
                    using var pooledData = BTMemoryPool.CreatePooledCopy(e.Value);
                    await _appendToBufferAsync(pooledData.Array.AsSpan(0, pooledData.Length).ToArray());
                    
                    var uuid = await characteristic.GetUUIDAsync();
                    OnNotificationDataReceived(uuid, e.Value); // Use original data for events
                }
                else if (e.Value != null)
                {
                    // For small payloads, pooling overhead isn't worth it
                    await _appendToBufferAsync(e.Value);
                    var uuid = await characteristic.GetUUIDAsync();
                    OnNotificationDataReceived(uuid, e.Value);
                }

                // Signal that notification was received if we're waiting
                // TODO: Check how we can detect the end of multiple notifications
                /*
                if (_waitingForNotification)
                {
                    _notificationReceived.Set();
                }
                */
            }
            catch (Exception ex)
            {
                var characteristicUuid = "Unknown";
                try
                {
                    characteristicUuid = await characteristic.GetUUIDAsync().ConfigureAwait(false);
                }
                catch { /* Ignore errors getting UUID */ }

                var errorContext = new
                {
                    CharacteristicUuid = characteristicUuid,
                    DataLength = e.Value?.Length ?? 0,
                    Timestamp = DateTime.UtcNow
                };
                
                Console.Error.WriteLine($"Error in ReceiveNotificationData: {ex.Message}");
                Console.Error.WriteLine($"Context: Characteristic={errorContext.CharacteristicUuid}, " +
                                      $"DataLength={errorContext.DataLength}, " +
                                      $"Time={errorContext.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
                
                // Attempt recovery by resetting communication state
                if (_communicationInProgress && _waitingForNotification)
                {
                    Console.WriteLine("🔄 Attempting communication recovery after notification error");
                    StopCommunication();
                }
            }
        }

        /// <summary>
        /// Handles notification data received from the device and raises the NotificationDataReceived event.
        /// This method is typically called when new data arrives from the device's notifications,
        /// allowing subscribers to process the incoming data asynchronously.
        /// </summary>
        /// <param name="data">The raw notification data received from the device</param>
        protected virtual void OnNotificationDataReceived(string characteristicUuid, byte[] data)
        {
            NotificationDataReceived?.Invoke(this, data, characteristicUuid);
        }

        #endregion

        #region IDisposable Support
        /// <summary>
        /// Disposes of the communication resources.
        /// </summary>
        public void Dispose()
        {
            StopCommunication();
            _notificationReceived?.Dispose();
        }
        #endregion
    }
}
