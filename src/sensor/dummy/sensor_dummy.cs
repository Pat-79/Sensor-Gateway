using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensorGateway.Bluetooth;
using SensorGateway.Gateway;
using SensorGateway.Configuration;

namespace SensorGateway.Sensors
{
    /// <summary>
    /// Dummy sensor implementation for testing and development purposes.
    /// Generates fake sensor data without requiring actual hardware.
    /// </summary>
    public class DummySensor : Sensor
    {
        private readonly Random _random = new Random();
        private readonly SensorConfig _sensorConfig;

        /// <summary>
        /// Initializes a new instance of the DummySensor with optional configuration
        /// </summary>
        /// <param name="device">The Bluetooth device interface</param>
        /// <param name="sensorType">Type of sensor</param>
        /// <param name="sensorConfig">Sensor configuration (optional)</param>
        public DummySensor(IBTDevice device, SensorType sensorType, SensorConfig? sensorConfig = null)
            : base(device, sensorType, device.Name ?? "DummySensor")
        {
            _sensorConfig = sensorConfig ?? new SensorConfig();
        }


        #region ISensor Implementation - Override Abstract Methods

        /// <summary>
        /// Open the sensor connection
        /// </summary>
        public override async Task OpenAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(100, cancellationToken); // Simulate async open
        }
        /// <summary>
        /// Close the sensor connection
        /// </summary>
        public override async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(100, cancellationToken); // Simulate async close
        }

        /// <summary>
        /// Downloads simulated log data from the dummy sensor
        /// </summary>
        public override Task<IEnumerable<Measurement>> DownloadLogAsync(CancellationToken cancellationToken = default)
        {
            // Generate fake log data for testing
            var measurements = new List<Measurement>();
            for (int i = 0; i < 10; i++)
            {
                measurements.Add(new Measurement
                {
                    TimestampUtc = DateTime.UtcNow.AddMinutes(-i * 5),
                    Value = 20 + _random.NextDouble() * 10,
                    Type = MeasurementType.Temperature,
                    Unit = "°C",
                    Source = MeasurementSource.Log
                });
            }
            return Task.FromResult<IEnumerable<Measurement>>(measurements);
        }

        /// <summary>
        /// Processes simulated log data from dummy sensor
        /// </summary>
        public override async Task<IEnumerable<Measurement>> ProcessLogAsync(ISensor.ExecuteAfterDownload? callback = null, CancellationToken cancellationToken = default)
        {
            var measurements = await DownloadLogAsync(cancellationToken);
            // For dummy sensor, processing is the same as downloading
            if (callback != null)
            {
                callback(measurements);
            }
            return measurements;
        }

        /// <summary>
        /// Parses simulated advertisement data from dummy sensor
        /// </summary>
        public override Task<IEnumerable<Measurement>> ParseAdvertisementAsync(CancellationToken cancellationToken = default)
        {
            // Generate fake advertisement data
            var measurement = new Measurement
            {
                TimestampUtc = DateTime.UtcNow,
                Value = 22 + _random.NextDouble() * 5,
                Type = MeasurementType.Temperature,
                Unit = "°C",
                Source = MeasurementSource.Advertisement
            };
            return Task.FromResult<IEnumerable<Measurement>>(new[] { measurement });
        }

        /// <summary>
        /// Processes simulated advertisement data from dummy sensor
        /// </summary>
        public override Task<IEnumerable<Measurement>> ProcessAdvertisementAsync(ISensor.ExecuteAfterDownload? callback = null, CancellationToken cancellationToken = default)
        {
            if (callback != null)
            {
                var measurements = ParseAdvertisementAsync(cancellationToken);
                callback(measurements.Result);
            }
            return ParseAdvertisementAsync(cancellationToken);
        }

        /// <summary>
        /// Gets measurements from dummy sensor from specified source
        /// </summary>
        public override async Task<IEnumerable<Measurement>> GetMeasurementsAsync(MeasurementSource source = MeasurementSource.Both, CancellationToken cancellationToken = default)
        {
            var measurements = new List<Measurement>();

            switch (source)
            {
                case MeasurementSource.Log:
                    measurements.AddRange(await DownloadLogAsync(cancellationToken));
                    break;
                case MeasurementSource.Advertisement:
                    measurements.AddRange(await ParseAdvertisementAsync(cancellationToken));
                    break;
                case MeasurementSource.Both:
                    measurements.AddRange(await DownloadLogAsync(cancellationToken));
                    measurements.AddRange(await ParseAdvertisementAsync(cancellationToken));
                    break;
            }

            return measurements;
        }

        /// <summary>
        /// Gets simulated configuration from dummy sensor
        /// </summary>
        public override Task<Dictionary<string, object>?> GetConfigurationAsync(CancellationToken cancellationToken = default)
        {
            var config = new Dictionary<string, object>
            {
                ["sensorType"] = "Dummy",
                ["sampleRate"] = 60,
                ["enabled"] = true,
                ["lastUpdated"] = DateTime.UtcNow
            };
            return Task.FromResult<Dictionary<string, object>?>(config);
        }

        /// <summary>
        /// Updates dummy sensor configuration (simulation)
        /// </summary>
        public override Task<bool> UpdateConfigurationAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
        {
            // Simulate configuration update
            Console.WriteLine($"🔧 Dummy sensor configuration updated with {configuration.Count} settings");
            return Task.FromResult(true);
        }

        #endregion

        #region Virtual Method Overrides (Optional - these are virtual in base class)

        // Removed dangerous sync-over-async patterns

        #endregion

        #region Additional Dummy-Specific Methods

        /// <summary>
        /// Generates random temperature measurement
        /// </summary>
        public double GenerateRandomTemperature()
        {
            return 20 + _random.NextDouble() * 15; // 20-35°C
        }

        /// <summary>
        /// Generates random humidity measurement
        /// </summary>
        public double GenerateRandomHumidity()
        {
            return 30 + _random.NextDouble() * 40; // 30-70%
        }

        #endregion
    }
}