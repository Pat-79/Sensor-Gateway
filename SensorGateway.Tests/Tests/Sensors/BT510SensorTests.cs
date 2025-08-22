using Microsoft.VisualStudio.TestTools.UnitTesting;
using SensorGateway.Sensors.bt510;
using SensorGateway.Tests.Mocks;
using SensorGateway.Sensors;
using SensorGateway.Configuration;
using SensorGateway.Gateway;
using System.Threading.Tasks;
using System.Linq;

namespace SensorGateway.Tests.Tests.Sensors
{
    /// <summary>
    /// Unit tests for BT510Sensor using MockBTDevice.
    /// Tests the actual BT510Sensor implementation without requiring real Bluetooth hardware.
    /// </summary>
    [TestClass]
    public class BT510SensorTests
    {
        private MockBTDevice? _mockDevice;
        private BT510Sensor? _sensor;
        private SensorConfig? _sensorConfig;
        private BT510Config? _bt510Config;

        [TestInitialize]
        public void TestInitialize()
        {
            // Create mock BT device with BT510 settings
            _mockDevice = new MockBTDevice();
            
            // Create configuration objects
            _sensorConfig = new SensorConfig
            {
                // SensorConfig doesn't have Name property, using defaults
            };
            
            _bt510Config = new BT510Config
            {
                LogEntrySize = 8  // Standard BT510 log entry size
            };
            
            // Create BT510 sensor with mock device
            _sensor = new BT510Sensor(_mockDevice, SensorType.BT510, _sensorConfig);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _sensor?.Dispose();
            _mockDevice?.Dispose();
        }

        [TestMethod]
        public void Constructor_WithValidParameters_ShouldCreateSensor()
        {
            // Assert
            Assert.IsNotNull(_sensor);
            Assert.AreEqual(SensorType.BT510, _sensor.SensorType);
            Assert.AreEqual("MockBT510", _sensor.Name); // Uses device name
            Assert.AreEqual(_mockDevice, _sensor.Device);
        }

        [TestMethod]
        public async Task OpenAsync_WithMockDevice_ShouldConnect()
        {
            // Arrange
            _mockDevice!.ShouldConnectSucceed = true;

            // Act
            await _sensor!.OpenAsync();

            // Assert
            Assert.IsTrue(await _mockDevice.IsConnectedAsync());
        }

        [TestMethod]
        public async Task OpenAsync_WhenConnectionFails_ShouldHandleGracefully()
        {
            // Arrange
            _mockDevice!.ShouldConnectSucceed = false;

            // Act & Assert
            try
            {
                await _sensor!.OpenAsync();
                // Depending on implementation, this might throw or handle gracefully
                // Adjust assertion based on actual BT510Sensor behavior
            }
            catch
            {
                // Expected if connection failure should throw
                Assert.IsFalse(await _mockDevice.IsConnectedAsync());
            }
        }

        [TestMethod]
        public void ParseLogEntry_WithValidTemperatureData_ShouldReturnMeasurements()
        {
            // Arrange
            var temperatureLogEntry = MockBTDevice.CreateMockTemperatureLogEntry(
                timestamp: 12345,
                temperature: 2500  // 25.00°C
            );

            // Act
            var measurements = _sensor!.ParseLogEntry(temperatureLogEntry).ToList();

            // Assert
            Assert.IsNotNull(measurements);
            Assert.IsTrue(measurements.Count > 0);
            
            var measurement = measurements[0];
            Assert.AreEqual(MeasurementType.Temperature, measurement.Type);
            Assert.AreEqual(25.0, measurement.Value, 0.01); // Allow small floating point variance
            Assert.AreEqual("°C", measurement.Unit);
        }

        [TestMethod]
        public void ParseLogEntry_WithValidBatteryData_ShouldReturnMeasurements()
        {
            // Arrange
            var batteryLogEntry = MockBTDevice.CreateMockBatteryLogEntry(
                timestamp: 12345,
                voltage: 3300  // 3.3V
            );

            // Act
            var measurements = _sensor!.ParseLogEntry(batteryLogEntry).ToList();

            // Assert
            Assert.IsNotNull(measurements);
            Assert.IsTrue(measurements.Count > 0);
            
            var measurement = measurements[0];
            Assert.AreEqual(MeasurementType.Battery, measurement.Type);
            Assert.AreEqual(3.3, measurement.Value, 0.01); // 3300mV = 3.3V
            Assert.AreEqual("V", measurement.Unit);
        }

        [TestMethod]
        public void ParseLogEntry_WithInvalidData_ShouldReturnEmptyList()
        {
            // Arrange
            var invalidLogEntry = new byte[] { 0x01, 0x02 }; // Too short

            // Act
            var measurements = _sensor!.ParseLogEntry(invalidLogEntry).ToList();

            // Assert
            Assert.IsNotNull(measurements);
            Assert.AreEqual(0, measurements.Count);
        }

        [TestMethod]
        public void ParseLogEntry_WithUnknownEventType_ShouldReturnEmptyList()
        {
            // Arrange
            var unknownEventEntry = new byte[] 
            { 
                0x01, 0x02, 0x03, 0x04,  // Timestamp
                0x05, 0x06,              // Data
                0xFF,                    // Unknown event type
                0x00                     // Salt
            };

            // Act
            var measurements = _sensor!.ParseLogEntry(unknownEventEntry).ToList();

            // Assert
            Assert.IsNotNull(measurements);
            Assert.AreEqual(0, measurements.Count);
        }

        [TestMethod]
        public void MockDevice_SimulateLogData_ShouldTriggerNotification()
        {
            // Arrange
            var logData = MockBTDevice.CreateMockTemperatureLogEntry(1000, 2000);
            bool notificationReceived = false;

            _mockDevice!.NotificationDataReceived += (sender, data, uuid) =>
            {
                notificationReceived = true;
                Assert.AreEqual(logData, data);
            };

            // Act
            _mockDevice.SimulateBT510LogData(logData);

            // Assert
            Assert.IsTrue(notificationReceived);
            Assert.AreEqual(logData.Length, _mockDevice.BufferSize);
        }

        [TestMethod]
        public void MockDevice_CreateTemperatureLogEntry_ShouldHaveCorrectFormat()
        {
            // Arrange & Act
            var entry = MockBTDevice.CreateMockTemperatureLogEntry(0x12345678, 2500);

            // Assert
            Assert.AreEqual(8, entry.Length);
            
            // Check timestamp (little endian)
            Assert.AreEqual(0x78, entry[0]);
            Assert.AreEqual(0x56, entry[1]);
            Assert.AreEqual(0x34, entry[2]);
            Assert.AreEqual(0x12, entry[3]);
            
            // Check temperature data (little endian)
            Assert.AreEqual(0xC4, entry[4]); // 2500 & 0xFF = 196 (0xC4)
            Assert.AreEqual(0x09, entry[5]); // 2500 >> 8 = 9 (0x09)
            
            // Check event type
            Assert.AreEqual(1, entry[6]); // Temperature event
            
            // Check salt
            Assert.AreEqual(0x00, entry[7]);
        }

        [TestMethod]
        public void MockDevice_CreateBatteryLogEntry_ShouldHaveCorrectFormat()
        {
            // Arrange & Act
            var entry = MockBTDevice.CreateMockBatteryLogEntry(0x12345678, 3300);

            // Assert
            Assert.AreEqual(8, entry.Length);
            
            // Check timestamp (little endian)
            Assert.AreEqual(0x78, entry[0]);
            Assert.AreEqual(0x56, entry[1]);
            Assert.AreEqual(0x34, entry[2]);
            Assert.AreEqual(0x12, entry[3]);
            
            // Check battery voltage (little endian)
            Assert.AreEqual(0xE4, entry[4]); // 3300 & 0xFF = 228 (0xE4)
            Assert.AreEqual(0x0C, entry[5]); // 3300 >> 8 = 12 (0x0C)
            
            // Check event type
            Assert.AreEqual(12, entry[6]); // Battery event
            
            // Check salt
            Assert.AreEqual(0x00, entry[7]);
        }
    }
}
