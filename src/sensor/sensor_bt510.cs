using System;
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
        /// Initializes the BT510 sensor connection and configuration
        /// </summary>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        #region ISensor Implementation - Override Abstract Methods

        /// <summary>
        /// Downloads log data from the BT510 sensor using JSON-RPC commands
        /// </summary>
        public override async Task<IEnumerable<Measurement>> DownloadLogAsync(CancellationToken cancellationToken = default)
        {
            var measurements = new List<Measurement>();

            try
            {
                // 1. Connect to device
                if (!await Device!.IsConnectedAsync().ConfigureAwait(false))
                {
                    await Device.ConnectAsync().ConfigureAwait(false);
                }

                // 2. Initialize communication setup
                await InitializeCommunicationAsync().ConfigureAwait(false);

                // 3. Synchronize time to ensure accurate timestamps
                //await SynchronizeTimeAsync().ConfigureAwait(false);

                // 4. Download all log data using the convenience method
                var logDataBatches = await DownloadAllLogsAsync().ConfigureAwait(false);

                // 5. Parse the data into structured measurements
                foreach (var logBatch in logDataBatches)
                {
                    var parsedMeasurements = ParseLogEntry(logBatch);
                    measurements.AddRange(parsedMeasurements);
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
        public override async Task<IEnumerable<Measurement>> ProcessLogAsync(CancellationToken cancellationToken = default)
        {
            // Get a token for this scan operation
            // This ensures we don't exceed the maximum concurrent operations
            // Note: This is a blocking call that will wait until a token is available
            // If this times out, it means too many scans are running concurrently
            // This is important to prevent overwhelming the Bluetooth stack
            // The use of `using` ensures the token is returned to the pool automatically
            using var token = await BTManager.Instance.GetTokenAsync(TimeSpan.FromSeconds(180), cancellationToken);
            
            throw new NotImplementedException();
        }

        /// <summary>
        /// Parses BLE advertisement data from BT510 sensor
        /// </summary>
        public override async Task<IEnumerable<Measurement>> ParseAdvertisementAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken); // Simulate async operation
            throw new NotImplementedException();
        }

        /// <summary>
        /// Processes advertisement data from BT510 sensor
        /// </summary>
        public override async Task<IEnumerable<Measurement>> ProcessAdvertisementAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken); // Simulate async operation
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets measurements from BT510 sensor from specified source
        /// </summary>
        public override async Task<IEnumerable<Measurement>> GetMeasurementsAsync(MeasurementSource source = MeasurementSource.Both, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken); // Simulate async operation
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets current configuration from BT510 sensor via JSON-RPC
        /// </summary>
        public override async Task<Dictionary<string, object>> GetConfigurationAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken); // Simulate async operation
            throw new NotImplementedException();
        }

        /// <summary>
        /// Updates BT510 sensor configuration via JSON-RPC commands
        /// </summary>
        public override async Task<bool> UpdateConfigurationAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken); // Simulate async operation
            throw new NotImplementedException();
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Parses a single log entry into measurement objects
        /// </summary>
        private IEnumerable<Measurement> ParseLogEntry(object logEntry)
        {
            var measurements = new List<Measurement>();

            try
            {
                // Convert the log entry to a dictionary for easier parsing
                if (logEntry is not System.Text.Json.JsonElement jsonElement)
                {
                    return measurements;
                }

                // Extract timestamp if available
                var timestamp = DateTime.UtcNow; // Default fallback
                if (jsonElement.TryGetProperty("timestamp", out var timestampElement))
                {
                    if (timestampElement.TryGetInt64(out var epochSeconds))
                    {
                        timestamp = DateTimeOffset.FromUnixTimeSeconds(epochSeconds).DateTime;
                    }
                }

                // Extract temperature measurement
                if (jsonElement.TryGetProperty("temperature", out var tempElement) && 
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

                // Extract battery measurement  
                if (jsonElement.TryGetProperty("battery", out var batteryElement) && 
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

                // Handle nested data objects
                if (jsonElement.TryGetProperty("data", out var dataElement))
                {
                    var nestedMeasurements = ParseDataElement(dataElement, timestamp);
                    measurements.AddRange(nestedMeasurements);
                }
            }
            catch (Exception ex)
            {
                // Log parsing error but continue with other entries
                Console.WriteLine($"Warning: Failed to parse log entry: {ex.Message}");
            }

            return measurements;
        }

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
        /// Parses binary log data from BT510 into measurement objects using configuration
        /// </summary>
        private IEnumerable<Measurement> ParseLogEntry(byte[] logData)
        {
            var measurements = new List<Measurement>();

            try
            {
                // ✅ Use your configuration instead of magic number
                int eventSize = _bt510Config.LogEntrySize;
                var eventCount = logData.Length / eventSize;

                for (int i = 0; i < eventCount; i++)
                {
                    var eventOffset = i * eventSize;
                    
                    // Extract fields from the 8-byte structure (little-endian format)
                    var timestamp = BitConverter.ToUInt32(logData, eventOffset);
                    var data = BitConverter.ToUInt16(logData, eventOffset + 4);
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