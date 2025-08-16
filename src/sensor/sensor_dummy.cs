using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GatewaySensor.Sensors
{
    /// <summary>
    /// Dummy sensor implementation for testing and development purposes.
    /// Generates fake sensor data without requiring actual hardware.
    /// </summary>
    public class DummySensor : Sensor
    {
        private readonly Random _random = new Random();

        public DummySensor(BTDevice? device, SensorType sensorType) : base(device, sensorType)
        {
        }

        #region ISensor Implementation - Override Abstract Methods

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
        public override Task<IEnumerable<Measurement>> ProcessLogAsync(CancellationToken cancellationToken = default)
        {
            // For dummy sensor, processing is the same as downloading
            return DownloadLogAsync(cancellationToken);
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
        public override Task<IEnumerable<Measurement>> ProcessAdvertisementAsync(CancellationToken cancellationToken = default)
        {
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
        public override Task<Dictionary<string, object>> GetConfigurationAsync(CancellationToken cancellationToken = default)
        {
            var config = new Dictionary<string, object>
            {
                ["sensorType"] = "Dummy",
                ["sampleRate"] = 60,
                ["enabled"] = true,
                ["lastUpdated"] = DateTime.UtcNow
            };
            return Task.FromResult(config);
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

        /// <summary>
        /// Synchronous version of DownloadLog for dummy sensor
        /// </summary>
        public override IEnumerable<Measurement> DownloadLog()
        {
            return DownloadLogAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Synchronous version of ProcessLog for dummy sensor
        /// </summary>
        public override IEnumerable<Measurement> ProcessLog()
        {
            return ProcessLogAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Synchronous version of ParseAdvertisement for dummy sensor
        /// </summary>
        public override IEnumerable<Measurement> ParseAdvertisement()
        {
            return ParseAdvertisementAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Synchronous version of ProcessAdvertisement for dummy sensor
        /// </summary>
        public override IEnumerable<Measurement> ProcessAdvertisement()
        {
            return ProcessAdvertisementAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Synchronous version of GetConfiguration for dummy sensor
        /// </summary>
        public override Dictionary<string, object> GetConfiguration()
        {
            return GetConfigurationAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Synchronous version of UpdateConfiguration for dummy sensor
        /// </summary>
        public override bool UpdateConfiguration(Dictionary<string, object> configuration)
        {
            return UpdateConfigurationAsync(configuration).GetAwaiter().GetResult();
        }

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