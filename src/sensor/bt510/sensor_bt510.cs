using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensorGateway.Bluetooth;
using SensorGateway.Configuration;
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
        private int _mtu = 244; // Default MTU size for BLE

        /// <summary>
        /// Initializes a new instance of the BT510Sensor with optional configuration
        /// </summary>
        /// <param name="device">The Bluetooth device interface</param>
        /// <param name="sensorType">Type of sensor</param>
        /// <param name="sensorConfig">Sensor configuration (optional)</param>
        public BT510Sensor(IBTDevice device, SensorType sensorType, SensorConfig? sensorConfig = null)
            : base(device, sensorType, device.Name ?? "BT510Sensor")
        {
            _sensorConfig = sensorConfig ?? new SensorConfig();
            _bt510Config = _sensorConfig.BT510; // Use the nested BT510 config
        }

        /// <summary>
        /// Initializes a new BT510Sensor with the specified device and configuration.
        /// Sets up event handlers for device notifications and prepares the sensor for communication.
        /// </summary>
        /// <param name="device">The Bluetooth device interface for communication</param>
        /// <param name="sensorConfig">Configuration settings for the sensor</param>
        /// <param name="bt510Config">BT510-specific configuration settings</param>
        public BT510Sensor(IBTDevice device, SensorConfig sensorConfig, BT510Config bt510Config)
            : base(device, SensorType.BT510, device.Name ?? "BT510Sensor")
        {
            _sensorConfig = sensorConfig;
            _bt510Config = bt510Config;
        }

        /// <summary>
        /// Open the sensor connection
        /// </summary>
        public override async Task OpenAsync(CancellationToken cancellationToken = default)
        {
            if (!await Device!.IsConnectedAsync())
            {
                await Device!.ConnectAsync();
            }

            await InitializeAsync(cancellationToken);

            // Retrieve MTU value
            try
            {
                _mtu = await GetAsync<int>("mtu");
            }
            catch
            {
                // Ignore errors retrieving MTU, use current value (probably default 244)
            }
        }
        
        /// <summary>
        /// Close the sensor connection
        /// </summary>
        public override async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if (await Device!.IsConnectedAsync())
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
                // 0. Check if Device is null
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("DownloadLogAsync was canceled.", cancellationToken);
                }
                
                // 1. Connect to device
                await OpenAsync();

                // 2. Synchronize time to ensure accurate timestamps
                await SynchronizeTimeAsync();

                // 3. Download, process, and acknowledge logs in batches
                return await ProcessLogBatchesAsync(callback, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log the error if needed
                throw new InvalidOperationException($"Failed to download log data from BT510 sensor: {ex.Message}", ex);
            }
            finally
            {
                // 7. Disconnect from device
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

        internal async Task<IEnumerable<Measurement>> ProcessLogBatchesAsync(ISensor.ExecuteAfterDownload? callback = null, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("DownloadLogAsync was canceled.", cancellationToken);
            }

            // 3. Download, process, and acknowledge logs in batches
            var allMeasurements = new List<Measurement>();
            uint logCount;

            const int maxLoops = 10;
            int loopCount = 0;

            while ((logCount = await PrepareLogAsync() ?? 0) > 0 && loopCount < maxLoops)
            {
                loopCount++;

                // 4. Download log batch
                var batchMeasurements = await GetMeasurementsAsync(MeasurementSource.Log, cancellationToken);
                allMeasurements.AddRange(batchMeasurements);

                // 5. Call callback and handle acknowledgment
                var shouldAck = callback?.Invoke(batchMeasurements) ?? true;

                if (shouldAck)
                {
                    var ackCount = await AckLogAsync(batchMeasurements.Count());
                    if (ackCount < batchMeasurements.Count())
                    {
                        Console.WriteLine($"Warning: Only {ackCount} out of {batchMeasurements.Count()} log entries acknowledged.");
                    }
                }
            }

            if (loopCount >= maxLoops)
            {
                Console.WriteLine($"Warning: Maximum loop limit ({maxLoops}) reached while processing log data.");
            }

            return allMeasurements.AsEnumerable();
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
            // Check if Device is null
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("DownloadLogAsync was canceled.", cancellationToken);
            }

            var measurements = new List<Measurement>();

            // Get measurements from advertisement data if requested
            if (source == MeasurementSource.Advertisement || source == MeasurementSource.Both)
            {
                measurements.AddRange(GetMeasurementsAdvertisement());
            }

            // Get measurements from log data if requested
            if (source == MeasurementSource.Log || source == MeasurementSource.Both)
            {
                measurements.AddRange(await GetMeasurementsLogAsync());
            }
            
            // Sort measurements by timestamp before returning
            return measurements.OrderBy(m => m.TimestampUtc).ToList().AsReadOnly();
        }

        /// <summary>
        /// Read measurement from advertisement data
        /// Parse BT510 advertisement data according to the 1M PHY specification
        /// </summary>
        private IEnumerable<Measurement> GetMeasurementsAdvertisement()
        {
            var advData = Device?.AdvertisementData;
            var measurements = new List<Measurement>();

            if (advData == null || advData.Count == 0)
            {
                return measurements; // No advertisement data to process
            }

            advData.TryGetValue(0x00FF, out var manufacturerDataObj); // 0x00FF = Manufacturer Specific Data
            if(manufacturerDataObj is not byte[] manufacturerData || manufacturerData.Length < 31)
            {
                return measurements; // No valid manufacturer data
            }
            else 
            {
                return ParseAdvertisementEntry(manufacturerData);
            }
        }

        /// <summary>
        /// Downloads all log data from BT510 sensor using JSON-RPC commands
        /// </summary>
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
            return await SetAsync(configuration);
        }

        #endregion

        #region Helper Methods
        public IEnumerable<Measurement> ParseAdvertisementEntry(byte[] advertisementData)
        {
            var measurements = new List<Measurement>();

            if (advertisementData == null || advertisementData.Length < 31)
            {
                return measurements; // No advertisement data to process
            }

            try
            {
                // Parse BT510 advertisement data according to 1M PHY specification (31 bytes total)
                // Reference: BT510 EZ Guide pages 7-8, Table 1: 1M PHY

                // Parse the record header
                var recordType = advertisementData[19];     // Byte 19: Record Type - See 4.1.4 Record Event Types
                var recordNumberLSB = advertisementData[20]; // Byte 20: Record Number LSB
                var recordNumberMSB = advertisementData[21]; // Byte 21: Record Number MSB
                var recordNumber = (ushort)(recordNumberLSB | (recordNumberMSB << 8));

                // Parse epoch timestamp (bytes 22-25)
                var epoch = BinaryPrimitives.ReadUInt32LittleEndian(advertisementData.AsSpan(22, 4));

                // Parse sensor data (bytes 26-29: Data byte 0, Data byte 1, Data byte 2 MSB, Data byte 3 MSB)
                // Advertisement uses 32-bit data value (4 bytes) vs 16-bit in log data
                var sensorData = BinaryPrimitives.ReadUInt32LittleEndian(advertisementData.AsSpan(26, 4));

                // Note: Byte 30 is "Reset Count LSB" for testing purposes

                // Convert epoch to DateTime
                var timestamp = DateTimeOffset.FromUnixTimeSeconds(epoch).DateTime;

                // Create measurement based on record type
                var measurement = CreateMeasurementFromEventAdvertisement(recordType, sensorData, timestamp);
                if (measurement != null)
                {
                    // Override source to Advertisement since this came from advertisement data
                    measurement.Source = MeasurementSource.Advertisement;
                    measurements.Add(measurement);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to parse BT510 advertisement data: {ex.Message}");
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
                    var measurement = CreateMeasurementFromEventLogbook(type, data, eventTime);
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
        /// Creates a measurement from a BT510 event based on the event type (16-bit log data)
        /// </summary>
        internal Measurement? CreateMeasurementFromEventLogbook(byte eventType, ushort data, DateTime timestamp)
        {
            return CreateMeasurementFromEvent(eventType, (uint)data, timestamp, MeasurementSource.Log);
        }

        /// <summary>
        /// Creates a measurement from a BT510 advertisement event (32-bit data)
        /// </summary>
        internal Measurement? CreateMeasurementFromEventAdvertisement(byte eventType, uint data, DateTime timestamp)
        {
            return CreateMeasurementFromEvent(eventType, data, timestamp, MeasurementSource.Advertisement);
        }

        /// <summary>
        /// Creates a measurement from a BT510 event based on the event type (unified implementation)
        /// </summary>
        private Measurement? CreateMeasurementFromEvent(byte eventType, uint data, DateTime timestamp, MeasurementSource source)
        {
            return eventType switch
            {
                1 => new Measurement // TEMPERATURE
                {
                    Type = MeasurementType.Temperature,
                    Value = ConvertTemperatureData(data),
                    Unit = "°C",
                    TimestampUtc = timestamp,
                    Source = source
                },
                12 => new Measurement // BATTERY GOOD
                {
                    Type = MeasurementType.Battery,
                    Value = ConvertBatteryData(data),
                    Unit = "V",
                    TimestampUtc = timestamp,
                    Source = source
                },
                13 => new Measurement // ADVERTISE ON BUTTON (also has battery data)
                {
                    Type = MeasurementType.Battery,
                    Value = ConvertBatteryData(data),
                    Unit = "V",
                    TimestampUtc = timestamp,
                    Source = source
                },
                16 => new Measurement // BATTERY BAD
                {
                    Type = MeasurementType.Battery,
                    Value = ConvertBatteryData(data),
                    Unit = "V",
                    TimestampUtc = timestamp,
                    Source = source
                },
                4 or 5 or 6 or 7 or 8 or 9 or 10 => new Measurement // Temperature alarms
                {
                    Type = MeasurementType.Temperature,
                    Value = ConvertTemperatureData(data),
                    Unit = "°C",
                    TimestampUtc = timestamp,
                    Source = source
                },
                _ => null // Ignore other event types for now
            };
        }

        /// <summary>
        /// Converts temperature data from hundredths of degrees C (16-bit unsigned, convert to signed for temperature)
        /// </summary>
        internal double ConvertTemperatureData(uint data)
        {
            // Temperature is stored as unsigned initially, but needs two's complement conversion to signed
            var signedData = unchecked((short)data); // Two's complement conversion to signed
            return signedData / 100.0;
        }

        /// <summary>
        /// Converts battery data from millivolts (16-bit unsigned)
        /// </summary>
        internal double ConvertBatteryData(uint data)
        {
            // Battery data remains unsigned (no conversion needed)
            return data / 1000.0; // Convert from millivolts to volts
        }
        #endregion
    }
}