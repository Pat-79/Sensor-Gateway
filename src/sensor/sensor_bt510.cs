using System;
using System.Buffers.Binary; // Add this using
using System.Collections.Generic;
using System.Linq; // Add this for LINQ operations in DisplayMeasurements
using System.Threading;
using System.Threading.Tasks;
using SensorGateway.Bluetooth;
using SensorGateway.Configuration; // Add this using
using SensorGateway.Gateway;

namespace SensorGateway.Sensors.bt510
{
    /// <summary>
    /// BT510 sensor implementation that communicates via JSON-RPC over BLE
    /// 
    /// TOKEN USAGE POLICY:
    /// - DownloadLogAsync/ProcessLogAsync: Requires token (active BT communication)
    /// - GetConfigurationAsync/UpdateConfigurationAsync: Requires token (active BT communication) 
    /// - ParseAdvertisementAsync/ProcessAdvertisementAsync: No token needed (local processing)
    /// - GetMeasurementsAsync: Requires token if includes Log source
    /// </summary>
    public partial class BT510Sensor : Sensor, IAsyncInitializable
    {
        private readonly SensorConfig _sensorConfig;
        private readonly BT510Config _bt510Config;

        /// <summary>
        /// Initializes a new instance of the BT510Sensor with optional configuration
        /// </summary>
        /// <param name="device">The Bluetooth device</param>
        /// <param name="sensorType">Type of sensor</param>
        /// <param name="sensorConfig">Sensor configuration (optional)</param>
        public BT510Sensor(BTDevice device, SensorType sensorType, SensorConfig? sensorConfig = null)
            : base(device, sensorType)
        {
            _sensorConfig = sensorConfig ?? new SensorConfig();
            _bt510Config = _sensorConfig.BT510; // Use the nested BT510 config
        }

        /// <summary>
        /// Open the sensor connection
        /// </summary>
        public override async Task OpenAsync(CancellationToken cancellationToken = default)
        {
            if(!await Device!.IsConnectedAsync())
            {
                await Device!.ConnectAsync();
            }

            await InitializeAsync(cancellationToken);
        }
        /// <summary>
        /// Close the sensor connection
        /// </summary>
        public override async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if(await Device!.IsConnectedAsync())
            {
                await Device!.DisconnectAsync();
            }
        }

        /// <summary>
        /// Initializes the BT510 sensor connection and configuration
        /// </summary>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return InitializeCommunicationAsync();
        }

        #region ISensor Implementation - Override Abstract Methods

        /// <summary>
        /// Downloads log data from the BT510 sensor using JSON-RPC commands
        /// </summary>
        public override async Task<IEnumerable<Measurement>> DownloadLogAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // 0. Check if Device is null
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("DownloadLogAsync was canceled.", cancellationToken);
                }

                // 1. Connect to device
                await OpenAsync();

                // 2. Synchronize time to ensure accurate timestamps
                await SynchronizeTimeAsync();

                // 3. Download all log data using the convenience method                
                return await GetMeasurementsAsync(MeasurementSource.Log, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log the error if needed
                throw new InvalidOperationException($"Failed to download log data from BT510 sensor: {ex.Message}", ex);
            }
            finally
            {
                // 6. Disconnect from device
                try
                {
                    if (await Device!.IsConnectedAsync().ConfigureAwait(false))
                    {
                        await Device.DisconnectAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    // Don't throw on disconnect failures, just log if needed
                    Console.WriteLine($"Warning: Failed to disconnect from BT510 sensor: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Processes raw log data from BT510 sensor into structured measurements
        /// </summary>
        public override async Task<IEnumerable<Measurement>> ProcessLogAsync(ISensor.ExecuteAfterDownload? callback = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // 1. Connect to device
                await OpenAsync();

                // 2. Synchronize time to ensure accurate timestamps
                await SynchronizeTimeAsync();

                // 3. Download all log data using the convenience method                
                var measurements = await GetMeasurementsAsync(MeasurementSource.Log, cancellationToken);

                // 6. Call callback method if provided
                var callbackResult = true;
                if (callback != null)
                {
                    callbackResult = callback(measurements);
                }

                // 7. Do acknowledgment
                if (callbackResult)
                {
                    var ackCount = await AckLogAsync(measurements.Count());
                    if (ackCount < measurements.Count())
                    {
                        Console.WriteLine($"Warning: Only {ackCount} out of {measurements.Count()} log entries acknowledged.");
                    }
                }

                return measurements;
            }
            catch (Exception ex)
            {
                // Log the error if needed
                throw new InvalidOperationException($"Failed to download log data from BT510 sensor: {ex.Message}", ex);
            }
            finally
            {
                // 6. Disconnect from device
                try
                {
                    await CloseAsync();
                }
                catch (Exception ex)
                {
                    // Don't throw on disconnect failures, just log if needed
                    Console.WriteLine($"Warning: Failed to disconnect from BT510 sensor: {ex.Message}");
                }
            }

        }

        /// <summary>
        /// Parses BLE advertisement data from BT510 sensor
        /// </summary>
        public override async Task<IEnumerable<Measurement>> ParseAdvertisementAsync(CancellationToken cancellationToken = default)
        {
            return await GetMeasurementsAsync(MeasurementSource.Advertisement, cancellationToken);
        }

        /// <summary>
        /// Processes advertisement data from BT510 sensor
        /// </summary>
        public override async Task<IEnumerable<Measurement>> ProcessAdvertisementAsync(ISensor.ExecuteAfterDownload? callback = null, CancellationToken cancellationToken = default)
        {
            var measurements = await GetMeasurementsAsync(MeasurementSource.Advertisement, cancellationToken);

            // 6. Call callback method if provided
            if (callback != null)
            {
                callback(measurements);
            }
            
            return measurements;
        }

        /// <summary>
        /// Gets measurements from BT510 sensor from specified source
        /// </summary>
        public override async Task<IEnumerable<Measurement>> GetMeasurementsAsync(MeasurementSource source = MeasurementSource.Both, CancellationToken cancellationToken = default)
        {
            // 0. Check if Device is null
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("DownloadLogAsync was canceled.", cancellationToken);
            }

            var measurements = new List<Measurement>();

            if (source == MeasurementSource.Advertisement || source == MeasurementSource.Both)
            {
                measurements.AddRange(GetMeasurementsAdvertisement());
            }

            if (source == MeasurementSource.Log || source == MeasurementSource.Both)
            {

                // 4. Parse the data into structured measurements
                measurements.AddRange(await GetMeasurementsLogAsync());
            }
            
            return measurements;
        }

        private IEnumerable<Measurement> GetMeasurementsAdvertisement()
        {
            var advData = Device?.AdvertisementData;
            var measurements = new List<Measurement>();

            if(advData == null || advData.Count == 0)
            {
                return measurements; // No advertisement data to process
            }

            foreach (var data in advData)
            {
                // Do magic parsing here
            }

            return measurements;
        }

        private async Task<IEnumerable<Measurement>> GetMeasurementsLogAsync()
        {
            // 1. Connect to device
            await Device!.ConnectAsync();
            if (!await Device!.IsConnectedAsync())
            {
                throw new InvalidOperationException("Failed to connect to BT510 sensor.");
            }

            // 2. Initialize communication setup
            await InitializeCommunicationAsync();

            // 3. Download all log data using the convenience method
            var logData = await DownloadAllLogsAsync();
            if (logData == null)
            {
                return Enumerable.Empty<Measurement>(); // No data to process
            }

            return ParseLogEntry(logData);
        }

        /// <summary>
        /// Gets current configuration from BT510 sensor via JSON-RPC
        /// </summary>
        public override async Task<Dictionary<string, object>?> GetConfigurationAsync(CancellationToken cancellationToken = default)
        {
            List<string>? properties = null;
            
            // If no specific properties requested, return all common ones
            if (properties == null || properties.Count == 0)
            {
                properties = new List<string>();
            }

            var response = await GetAsync(properties.ToArray()).ConfigureAwait(false);
            return response;
        }

        /// <summary>
        /// Updates BT510 sensor configuration via JSON-RPC commands
        /// </summary>
        public override async Task<bool> UpdateConfigurationAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
        {
            return await UpdateConfigurationAsync(configuration);
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Parses a data element from a log entry
        /// </summary>
        private IEnumerable<Measurement> ParseDataElement(System.Text.Json.JsonElement dataElement, DateTime timestamp)
        {
            var measurements = new List<Measurement>();

            try
            {
                // Temperature
                if (dataElement.TryGetProperty("temperature", out var tempElement) && 
                    tempElement.TryGetDouble(out var temperature))
                {
                    measurements.Add(new Measurement
                    {
                        Type = MeasurementType.Temperature,
                        Value = temperature,
                        Unit = "°C",
                        TimestampUtc = timestamp,
                        Source = MeasurementSource.Log
                    });
                }

                // Battery
                if (dataElement.TryGetProperty("battery", out var batteryElement) && 
                    batteryElement.TryGetDouble(out var battery))
                {
                    measurements.Add(new Measurement
                    {
                        Type = MeasurementType.Battery,
                        Value = battery,
                        Unit = "V",
                        TimestampUtc = timestamp,
                        Source = MeasurementSource.Log
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to parse data element: {ex.Message}");
            }

            return measurements;
        }

        /// <summary>
        /// Parses binary log data from BT510 into measurement objects using explicit little-endian format
        /// </summary>
        public IEnumerable<Measurement> ParseLogEntry(byte[] logData)
        {
            var measurements = new List<Measurement>();

            try
            {
                int eventSize = _bt510Config.LogEntrySize;
                var eventCount = logData.Length / eventSize;

                for (int i = 0; i < eventCount; i++)
                {
                    var eventOffset = i * eventSize;
                    
                    // Explicitly handle little-endian format regardless of host architecture
                    var timestamp = BinaryPrimitives.ReadUInt32LittleEndian(logData.AsSpan(eventOffset, 4));
                    var data = BinaryPrimitives.ReadUInt16LittleEndian(logData.AsSpan(eventOffset + 4, 2));
                    var type = logData[eventOffset + 6];
                    var salt = logData[eventOffset + 7];

                    // Convert timestamp to DateTime (seconds since Jan 1, 1970)
                    var eventTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;

                    // Process based on event type
                    var measurement = CreateMeasurementFromEvent(type, data, eventTime);
                    if (measurement != null)
                    {
                        measurements.Add(measurement);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to parse binary log data: {ex.Message}");
            }

            return measurements;
        }

        /// <summary>
        /// Creates a measurement from a BT510 event based on the event type
        /// </summary>
        private Measurement? CreateMeasurementFromEvent(byte eventType, ushort data, DateTime timestamp)
        {
            return eventType switch
            {
                1 => new Measurement // TEMPERATURE
                {
                    Type = MeasurementType.Temperature,
                    Value = ConvertTemperatureData(data),
                    Unit = "°C",
                    TimestampUtc = timestamp,
                    Source = MeasurementSource.Log
                },
                12 => new Measurement // BATTERY GOOD
                {
                    Type = MeasurementType.Battery,
                    Value = ConvertBatteryData(data),
                    Unit = "V",
                    TimestampUtc = timestamp,
                    Source = MeasurementSource.Log
                },
                13 => new Measurement // ADVERTISE ON BUTTON (also has battery data)
                {
                    Type = MeasurementType.Battery,
                    Value = ConvertBatteryData(data),
                    Unit = "V",
                    TimestampUtc = timestamp,
                    Source = MeasurementSource.Log
                },
                16 => new Measurement // BATTERY BAD
                {
                    Type = MeasurementType.Battery,
                    Value = ConvertBatteryData(data),
                    Unit = "V",
                    TimestampUtc = timestamp,
                    Source = MeasurementSource.Log
                },
                4 or 5 or 6 or 7 or 8 or 9 or 10 => new Measurement // Temperature alarms
                {
                    Type = MeasurementType.Temperature,
                    Value = ConvertTemperatureData(data),
                    Unit = "°C",
                    TimestampUtc = timestamp,
                    Source = MeasurementSource.Log
                },
                _ => null // Ignore other event types for now
            };
        }

        /// <summary>
        /// Converts temperature data from hundredths of degrees C (signed 16-bit)
        /// </summary>
        private double ConvertTemperatureData(ushort data)
        {
            // Temperature is stored as hundredths of degrees C in a signed 16-bit number
            var signedData = unchecked((short)data);
            return signedData / 100.0;
        }

        /// <summary>
        /// Converts battery data from millivolts (unsigned 16-bit)
        /// </summary>
        private double ConvertBatteryData(ushort data)
        {
            // Battery is stored as millivolts in an unsigned 16-bit number
            return data / 1000.0; // Convert to volts
        }

        /// <summary>
        /// Helper method to display measurement data on the console in a formatted way
        /// </summary>
        public static void DisplayMeasurements(IEnumerable<Measurement> measurements)
        {
            var measurementList = measurements.ToList();
            
            if (!measurementList.Any())
            {
                Console.WriteLine("No measurements found.");
                return;
            }

            Console.WriteLine($"\n📊 Found {measurementList.Count} measurements:");
            Console.WriteLine(new string('=', 80));

            // Group measurements by type for better display
            var groupedMeasurements = measurementList
                .GroupBy(m => m.Type)
                .OrderBy(g => g.Key);

            foreach (var group in groupedMeasurements)
            {
                Console.WriteLine($"\n🌡️  {group.Key} Measurements ({group.Count()} entries):");
                Console.WriteLine(new string('-', 60));

                var sortedMeasurements = group.OrderBy(m => m.TimestampUtc);

                foreach (var measurement in sortedMeasurements)
                {
                    var timestamp = measurement.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss");
                    var value = measurement.Value.ToString("F2");
                    var unit = measurement.Unit;
                    var source = measurement.Source;

                    // Color coding for different measurement types
                    var originalColor = Console.ForegroundColor;
                    switch (measurement.Type)
                    {
                        case MeasurementType.Temperature:
                            Console.ForegroundColor = ConsoleColor.Red;
                            break;
                        case MeasurementType.Battery:
                            Console.ForegroundColor = ConsoleColor.Green;
                            break;
                        default:
                            Console.ForegroundColor = ConsoleColor.White;
                            break;
                    }

                    Console.WriteLine($"  {timestamp} UTC | {value,8} {unit,-2} | {source}");
                    Console.ForegroundColor = originalColor;
                }
            }

            // Display summary statistics
            Console.WriteLine($"\n📈 Summary Statistics:");
            Console.WriteLine(new string('-', 40));

            foreach (var group in groupedMeasurements)
            {
                var values = group.Select(m => m.Value).ToList();
                var min = values.Min();
                var max = values.Max();
                var avg = values.Average();
                var unit = group.First().Unit;

                Console.WriteLine($"{group.Key,-12}: Min={min:F2}{unit}, Max={max:F2}{unit}, Avg={avg:F2}{unit}");
            }

            // Display time range
            var earliestTime = measurementList.Min(m => m.TimestampUtc);
            var latestTime = measurementList.Max(m => m.TimestampUtc);
            var timeSpan = latestTime - earliestTime;

            Console.WriteLine($"\n🕐 Time Range:");
            Console.WriteLine($"   From: {earliestTime:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"   To:   {latestTime:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"   Span: {timeSpan.TotalHours:F1} hours");
            
            Console.WriteLine(new string('=', 80));
        }

        /// <summary>
        /// Test helper method to download and display BT510 measurements
        /// </summary>
        public async Task TestDownloadAndDisplayAsync()
        {
            try
            {
                Console.WriteLine("🔄 Starting BT510 measurement download...");
                
                // Download measurements using the existing method
                var measurements = await DownloadLogAsync().ConfigureAwait(false);
                
                // Display the results
                DisplayMeasurements(measurements);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Error downloading measurements: {ex.Message}");
                Console.ResetColor();
            }
        }

        #endregion
    }
}