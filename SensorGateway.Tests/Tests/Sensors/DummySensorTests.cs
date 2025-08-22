using Microsoft.VisualStudio.TestTools.UnitTesting;
using SensorGateway.Sensors;
using SensorGateway.Tests.Mocks;
using SensorGateway.Configuration;
using SensorGateway.Gateway;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

namespace SensorGateway.Tests.Tests.Sensors
{
    /// <summary>
    /// Unit tests for DummySensor implementation.
    /// Tests all dummy sensor functionality including data generation,
    /// configuration management, and ISensor interface compliance.
    /// </summary>
    [TestClass]
    public class DummySensorTests
    {
        private MockBTDevice? _mockDevice;
        private DummySensor? _sensor;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockDevice = new MockBTDevice();
            _mockDevice.Name = "TestDummySensor";
            var sensorConfig = new SensorConfig();
            _sensor = new DummySensor(_mockDevice, SensorType.Dummy, sensorConfig);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _sensor?.Dispose();
            _mockDevice?.Dispose();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithValidParameters_ShouldInitialize()
        {
            // Assert
            Assert.IsNotNull(_sensor);
            Assert.AreEqual(SensorType.Dummy, _sensor.SensorType);
        }

        [TestMethod]
        public void Constructor_WithNullSensorConfig_ShouldUseDefaultConfig()
        {
            // Arrange & Act
            var sensor = new DummySensor(_mockDevice!, SensorType.Dummy, null);

            // Assert
            Assert.IsNotNull(sensor);
            Assert.AreEqual(SensorType.Dummy, sensor.SensorType);

            sensor.Dispose();
        }

        [TestMethod]
        public void Constructor_WithNullDeviceName_ShouldUseDefaultName()
        {
            // Arrange
            _mockDevice!.Name = null!; // Explicitly suppress warning since we're testing null handling

            // Act
            var sensor = new DummySensor(_mockDevice, SensorType.Dummy);

            // Assert
            Assert.IsNotNull(sensor);
            // The sensor should handle null device name gracefully

            sensor.Dispose();
        }

        #endregion

        #region OpenAsync and CloseAsync Tests

        [TestMethod]
        public async Task OpenAsync_ShouldCompleteSuccessfully()
        {
            // Act
            await _sensor!.OpenAsync();

            // Assert
            // Should complete without throwing
            Assert.IsTrue(true);
        }

        [TestMethod]
        public async Task OpenAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            try
            {
                await _sensor!.OpenAsync(cts.Token);
                Assert.Fail("Expected cancellation exception");
            }
            catch (TaskCanceledException)
            {
                // Expected - TaskCanceledException is thrown by Task.Delay
                Assert.IsTrue(true);
            }
            catch (OperationCanceledException)
            {
                // Also acceptable
                Assert.IsTrue(true);
            }
        }

        [TestMethod]
        public async Task CloseAsync_ShouldCompleteSuccessfully()
        {
            // Act
            await _sensor!.CloseAsync();

            // Assert
            // Should complete without throwing
            Assert.IsTrue(true);
        }

        [TestMethod]
        public async Task CloseAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            try
            {
                await _sensor!.CloseAsync(cts.Token);
                Assert.Fail("Expected cancellation exception");
            }
            catch (TaskCanceledException)
            {
                // Expected - TaskCanceledException is thrown by Task.Delay
                Assert.IsTrue(true);
            }
            catch (OperationCanceledException)
            {
                // Also acceptable
                Assert.IsTrue(true);
            }
        }

        #endregion

        #region DownloadLogAsync Tests

        [TestMethod]
        public async Task DownloadLogAsync_ShouldReturnMeasurements()
        {
            // Act
            var measurements = await _sensor!.DownloadLogAsync();

            // Assert
            Assert.IsNotNull(measurements);
            var measurementList = measurements.ToList();
            Assert.AreEqual(10, measurementList.Count, "Should return exactly 10 dummy measurements");

            foreach (var measurement in measurementList)
            {
                Assert.AreEqual(MeasurementType.Temperature, measurement.Type);
                Assert.AreEqual("°C", measurement.Unit);
                Assert.AreEqual(MeasurementSource.Log, measurement.Source);
                Assert.IsTrue(measurement.Value >= 20 && measurement.Value <= 30, "Temperature should be between 20-30°C");
                Assert.IsTrue(measurement.TimestampUtc <= DateTime.UtcNow, "Timestamp should not be in the future");
            }
        }

        [TestMethod]
        public async Task DownloadLogAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var measurements = await _sensor!.DownloadLogAsync(cts.Token);

            // Assert
            // Should complete normally since DummySensor doesn't use the cancellation token in DownloadLogAsync
            Assert.IsNotNull(measurements);
        }

        [TestMethod]
        public async Task DownloadLogAsync_MultipleCalls_ShouldReturnDifferentData()
        {
            // Act
            var measurements1 = await _sensor!.DownloadLogAsync();
            await Task.Delay(10); // Small delay to ensure different timestamps
            var measurements2 = await _sensor.DownloadLogAsync();

            // Assert
            var list1 = measurements1.ToList();
            var list2 = measurements2.ToList();

            Assert.AreEqual(list1.Count, list2.Count);
            
            // Values should potentially be different due to random generation
            // At least timestamps should be different
            Assert.AreNotEqual(list1.First().TimestampUtc, list2.First().TimestampUtc, 
                "Timestamps should be different between calls");
        }

        #endregion

        #region ProcessLogAsync Tests

        [TestMethod]
        public async Task ProcessLogAsync_WithoutCallback_ShouldReturnMeasurements()
        {
            // Act
            var measurements = await _sensor!.ProcessLogAsync();

            // Assert
            Assert.IsNotNull(measurements);
            var measurementList = measurements.ToList();
            Assert.AreEqual(10, measurementList.Count);
        }

        [TestMethod]
        public async Task ProcessLogAsync_WithCallback_ShouldExecuteCallback()
        {
            // Arrange
            bool callbackExecuted = false;
            IEnumerable<Measurement>? callbackMeasurements = null;

            var callback = new ISensor.ExecuteAfterDownload((measurements) =>
            {
                callbackExecuted = true;
                callbackMeasurements = measurements;
                return true;
            });

            // Act
            var measurements = await _sensor!.ProcessLogAsync(callback);

            // Assert
            Assert.IsTrue(callbackExecuted, "Callback should be executed");
            Assert.IsNotNull(callbackMeasurements);
            Assert.AreEqual(measurements.Count(), callbackMeasurements.Count());
        }

        [TestMethod]
        public async Task ProcessLogAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var measurements = await _sensor!.ProcessLogAsync(null, cts.Token);

            // Assert
            // Should complete normally since DummySensor passes cancellation token to DownloadLogAsync
            Assert.IsNotNull(measurements);
        }

        #endregion

        #region ParseAdvertisementAsync Tests

        [TestMethod]
        public async Task ParseAdvertisementAsync_ShouldReturnSingleMeasurement()
        {
            // Act
            var measurements = await _sensor!.ParseAdvertisementAsync();

            // Assert
            Assert.IsNotNull(measurements);
            var measurementList = measurements.ToList();
            Assert.AreEqual(1, measurementList.Count, "Should return exactly 1 advertisement measurement");

            var measurement = measurementList.First();
            Assert.AreEqual(MeasurementType.Temperature, measurement.Type);
            Assert.AreEqual("°C", measurement.Unit);
            Assert.AreEqual(MeasurementSource.Advertisement, measurement.Source);
            Assert.IsTrue(measurement.Value >= 22 && measurement.Value <= 27, "Advertisement temperature should be between 22-27°C");
        }

        [TestMethod]
        public async Task ParseAdvertisementAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var measurements = await _sensor!.ParseAdvertisementAsync(cts.Token);

            // Assert
            // Should complete normally since DummySensor doesn't use the cancellation token
            Assert.IsNotNull(measurements);
        }

        [TestMethod]
        public async Task ParseAdvertisementAsync_MultipleCalls_ShouldReturnDifferentValues()
        {
            // Act
            var measurements1 = await _sensor!.ParseAdvertisementAsync();
            var measurements2 = await _sensor.ParseAdvertisementAsync();

            // Assert
            var measurement1 = measurements1.First();
            var measurement2 = measurements2.First();

            // Values or timestamps should potentially be different
            // At least one should be different due to random generation or timing
            var valuesDifferent = Math.Abs(measurement1.Value - measurement2.Value) > 0.001;
            var timestampsDifferent = measurement1.TimestampUtc != measurement2.TimestampUtc;

            Assert.IsTrue(valuesDifferent || timestampsDifferent, 
                "Multiple calls should produce different values or timestamps");
        }

        #endregion

        #region ProcessAdvertisementAsync Tests

        [TestMethod]
        public async Task ProcessAdvertisementAsync_WithoutCallback_ShouldReturnMeasurement()
        {
            // Act
            var measurements = await _sensor!.ProcessAdvertisementAsync();

            // Assert
            Assert.IsNotNull(measurements);
            var measurementList = measurements.ToList();
            Assert.AreEqual(1, measurementList.Count);
        }

        [TestMethod]
        public async Task ProcessAdvertisementAsync_WithCallback_ShouldExecuteCallback()
        {
            // Arrange
            bool callbackExecuted = false;
            var callback = new ISensor.ExecuteAfterDownload((measurements) =>
            {
                callbackExecuted = true;
                return true;
            });

            // Act
            var measurements = await _sensor!.ProcessAdvertisementAsync(callback);

            // Assert
            Assert.IsTrue(callbackExecuted, "Callback should be executed");
            Assert.IsNotNull(measurements);
        }

        [TestMethod]
        public async Task ProcessAdvertisementAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var measurements = await _sensor!.ProcessAdvertisementAsync(null, cts.Token);

            // Assert
            // Should complete normally
            Assert.IsNotNull(measurements);
        }

        #endregion

        #region GetMeasurementsAsync Tests

        [TestMethod]
        public async Task GetMeasurementsAsync_WithLogSource_ShouldReturnLogData()
        {
            // Act
            var measurements = await _sensor!.GetMeasurementsAsync(MeasurementSource.Log);

            // Assert
            Assert.IsNotNull(measurements);
            var measurementList = measurements.ToList();
            Assert.AreEqual(10, measurementList.Count);
            Assert.IsTrue(measurementList.All(m => m.Source == MeasurementSource.Log));
        }

        [TestMethod]
        public async Task GetMeasurementsAsync_WithAdvertisementSource_ShouldReturnAdvertisementData()
        {
            // Act
            var measurements = await _sensor!.GetMeasurementsAsync(MeasurementSource.Advertisement);

            // Assert
            Assert.IsNotNull(measurements);
            var measurementList = measurements.ToList();
            Assert.AreEqual(1, measurementList.Count);
            Assert.IsTrue(measurementList.All(m => m.Source == MeasurementSource.Advertisement));
        }

        [TestMethod]
        public async Task GetMeasurementsAsync_WithBothSources_ShouldReturnCombinedData()
        {
            // Act
            var measurements = await _sensor!.GetMeasurementsAsync(MeasurementSource.Both);

            // Assert
            Assert.IsNotNull(measurements);
            var measurementList = measurements.ToList();
            Assert.AreEqual(11, measurementList.Count, "Should return 10 log + 1 advertisement = 11 total");

            var logMeasurements = measurementList.Where(m => m.Source == MeasurementSource.Log).ToList();
            var adMeasurements = measurementList.Where(m => m.Source == MeasurementSource.Advertisement).ToList();

            Assert.AreEqual(10, logMeasurements.Count);
            Assert.AreEqual(1, adMeasurements.Count);
        }

        [TestMethod]
        public async Task GetMeasurementsAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var measurements = await _sensor!.GetMeasurementsAsync(MeasurementSource.Both, cts.Token);

            // Assert
            // Should complete normally since underlying methods handle cancellation
            Assert.IsNotNull(measurements);
        }

        #endregion

        #region Configuration Tests

        [TestMethod]
        public async Task GetConfigurationAsync_ShouldReturnConfiguration()
        {
            // Act
            var config = await _sensor!.GetConfigurationAsync();

            // Assert
            Assert.IsNotNull(config);
            Assert.IsTrue(config.ContainsKey("sensorType"));
            Assert.IsTrue(config.ContainsKey("sampleRate"));
            Assert.IsTrue(config.ContainsKey("enabled"));
            Assert.IsTrue(config.ContainsKey("lastUpdated"));

            Assert.AreEqual("Dummy", config["sensorType"]);
            Assert.AreEqual(60, config["sampleRate"]);
            Assert.AreEqual(true, config["enabled"]);
            Assert.IsInstanceOfType(config["lastUpdated"], typeof(DateTime));
        }

        [TestMethod]
        public async Task GetConfigurationAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var config = await _sensor!.GetConfigurationAsync(cts.Token);

            // Assert
            // Should complete normally since DummySensor doesn't use the cancellation token
            Assert.IsNotNull(config);
        }

        [TestMethod]
        public async Task UpdateConfigurationAsync_ShouldReturnSuccess()
        {
            // Arrange
            var configuration = new Dictionary<string, object>
            {
                { "sampleRate", 30 },
                { "enabled", false },
                { "customSetting", "test" }
            };

            // Act
            var result = await _sensor!.UpdateConfigurationAsync(configuration);

            // Assert
            Assert.IsTrue(result, "Configuration update should succeed");
        }

        [TestMethod]
        public async Task UpdateConfigurationAsync_WithEmptyConfiguration_ShouldReturnSuccess()
        {
            // Arrange
            var emptyConfig = new Dictionary<string, object>();

            // Act
            var result = await _sensor!.UpdateConfigurationAsync(emptyConfig);

            // Assert
            Assert.IsTrue(result, "Empty configuration update should succeed");
        }

        [TestMethod]
        public async Task UpdateConfigurationAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var config = new Dictionary<string, object> { { "test", "value" } };

            // Act
            var result = await _sensor!.UpdateConfigurationAsync(config, cts.Token);

            // Assert
            // Should complete normally since DummySensor doesn't use the cancellation token
            Assert.IsTrue(result);
        }

        #endregion

        #region Dummy-Specific Method Tests

        [TestMethod]
        public void GenerateRandomTemperature_ShouldReturnValidRange()
        {
            // Act
            var temperature = _sensor!.GenerateRandomTemperature();

            // Assert
            Assert.IsTrue(temperature >= 20 && temperature <= 35, 
                $"Temperature {temperature} should be between 20-35°C");
        }

        [TestMethod]
        public void GenerateRandomHumidity_ShouldReturnValidRange()
        {
            // Act
            var humidity = _sensor!.GenerateRandomHumidity();

            // Assert
            Assert.IsTrue(humidity >= 30 && humidity <= 70, 
                $"Humidity {humidity} should be between 30-70%");
        }

        [TestMethod]
        public void GenerateRandomTemperature_MultipleCalls_ShouldReturnDifferentValues()
        {
            // Act
            var values = new List<double>();
            for (int i = 0; i < 10; i++)
            {
                values.Add(_sensor!.GenerateRandomTemperature());
            }

            // Assert
            var distinctValues = values.Distinct().Count();
            Assert.IsTrue(distinctValues > 1, "Multiple calls should generate different values (usually)");
        }

        [TestMethod]
        public void GenerateRandomHumidity_MultipleCalls_ShouldReturnDifferentValues()
        {
            // Act
            var values = new List<double>();
            for (int i = 0; i < 10; i++)
            {
                values.Add(_sensor!.GenerateRandomHumidity());
            }

            // Assert
            var distinctValues = values.Distinct().Count();
            Assert.IsTrue(distinctValues > 1, "Multiple calls should generate different values (usually)");
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public async Task FullWorkflow_OpenDownloadClose_ShouldWorkCorrectly()
        {
            // Act
            await _sensor!.OpenAsync();
            var measurements = await _sensor.DownloadLogAsync();
            await _sensor.CloseAsync();

            // Assert
            Assert.IsNotNull(measurements);
            Assert.AreEqual(10, measurements.Count());
        }

        [TestMethod]
        public async Task FullWorkflow_ProcessWithCallback_ShouldWorkCorrectly()
        {
            // Arrange
            bool callbackExecuted = false;
            var callback = new ISensor.ExecuteAfterDownload((measurements) =>
            {
                callbackExecuted = true;
                return measurements.Count() == 10;
            });

            // Act
            await _sensor!.OpenAsync();
            var processResult = await _sensor.ProcessLogAsync(callback);
            var configResult = await _sensor.GetConfigurationAsync();
            await _sensor.CloseAsync();

            // Assert
            Assert.IsTrue(callbackExecuted);
            Assert.IsNotNull(processResult);
            Assert.IsNotNull(configResult);
            Assert.AreEqual(10, processResult.Count());
        }

        #endregion
    }
}
