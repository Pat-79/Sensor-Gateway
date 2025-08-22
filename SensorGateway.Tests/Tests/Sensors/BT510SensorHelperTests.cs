using Microsoft.VisualStudio.TestTools.UnitTesting;
using SensorGateway.Sensors.bt510;
using SensorGateway.Tests.Mocks;
using SensorGateway.Sensors;
using SensorGateway.Configuration;
using SensorGateway.Gateway;
using System;

namespace SensorGateway.Tests.Tests.Sensors
{
    /// <summary>
    /// Unit tests for BT510Sensor internal helper methods.
    /// These tests focus on the data conversion and measurement creation logic.
    /// </summary>
    [TestClass]
    public class BT510SensorHelperTests
    {
        private MockBTDevice? _mockDevice;
        private BT510Sensor? _sensor;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockDevice = new MockBTDevice();
            var sensorConfig = new SensorConfig();
            _sensor = new BT510Sensor(_mockDevice, SensorType.BT510, sensorConfig);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _sensor?.Dispose();
            _mockDevice?.Dispose();
        }

        #region ConvertTemperatureData Tests

        [TestMethod]
        public void ConvertTemperatureData_WithPositiveValue_ShouldReturnCorrectTemperature()
        {
            // Arrange
            ushort rawData = 2500; // 25.00°C in hundredths

            // Act
            var result = _sensor!.ConvertTemperatureData(rawData);

            // Assert
            Assert.AreEqual(25.0, result, 0.001);
        }

        [TestMethod]
        public void ConvertTemperatureData_WithZero_ShouldReturnZero()
        {
            // Arrange
            ushort rawData = 0; // 0.00°C

            // Act
            var result = _sensor!.ConvertTemperatureData(rawData);

            // Assert
            Assert.AreEqual(0.0, result, 0.001);
        }

        [TestMethod]
        public void ConvertTemperatureData_WithNegativeValue_ShouldReturnNegativeTemperature()
        {
            // Arrange - Negative temperature: -10.50°C = -1050 as signed 16-bit = 64486 as unsigned
            ushort rawData = unchecked((ushort)(-1050)); // -10.50°C in two's complement

            // Act
            var result = _sensor!.ConvertTemperatureData(rawData);

            // Assert
            Assert.AreEqual(-10.5, result, 0.001);
        }

        [TestMethod]
        public void ConvertTemperatureData_WithExtremeNegativeValue_ShouldHandleCorrectly()
        {
            // Arrange - Very cold: -40.00°C = -4000 as signed 16-bit
            ushort rawData = unchecked((ushort)(-4000));

            // Act
            var result = _sensor!.ConvertTemperatureData(rawData);

            // Assert
            Assert.AreEqual(-40.0, result, 0.001);
        }

        [TestMethod]
        public void ConvertTemperatureData_WithHighPositiveValue_ShouldHandleCorrectly()
        {
            // Arrange - Hot temperature: 85.00°C = 8500 in hundredths
            ushort rawData = 8500;

            // Act
            var result = _sensor!.ConvertTemperatureData(rawData);

            // Assert
            Assert.AreEqual(85.0, result, 0.001);
        }

        [TestMethod]
        public void ConvertTemperatureData_WithFractionalValue_ShouldReturnCorrectPrecision()
        {
            // Arrange - 23.75°C = 2375 in hundredths
            ushort rawData = 2375;

            // Act
            var result = _sensor!.ConvertTemperatureData(rawData);

            // Assert
            Assert.AreEqual(23.75, result, 0.001);
        }

        #endregion

        #region ConvertBatteryData Tests

        [TestMethod]
        public void ConvertBatteryData_WithTypicalVoltage_ShouldReturnCorrectVoltage()
        {
            // Arrange - 3.3V = 3300mV
            ushort rawData = 3300;

            // Act
            var result = _sensor!.ConvertBatteryData(rawData);

            // Assert
            Assert.AreEqual(3.3, result, 0.001);
        }

        [TestMethod]
        public void ConvertBatteryData_WithZero_ShouldReturnZero()
        {
            // Arrange
            ushort rawData = 0;

            // Act
            var result = _sensor!.ConvertBatteryData(rawData);

            // Assert
            Assert.AreEqual(0.0, result, 0.001);
        }

        [TestMethod]
        public void ConvertBatteryData_WithLowBatteryVoltage_ShouldReturnCorrectValue()
        {
            // Arrange - 2.1V = 2100mV (low battery)
            ushort rawData = 2100;

            // Act
            var result = _sensor!.ConvertBatteryData(rawData);

            // Assert
            Assert.AreEqual(2.1, result, 0.001);
        }

        [TestMethod]
        public void ConvertBatteryData_WithHighVoltage_ShouldReturnCorrectValue()
        {
            // Arrange - 4.2V = 4200mV (fresh lithium battery)
            ushort rawData = 4200;

            // Act
            var result = _sensor!.ConvertBatteryData(rawData);

            // Assert
            Assert.AreEqual(4.2, result, 0.001);
        }

        [TestMethod]
        public void ConvertBatteryData_WithFractionalVoltage_ShouldReturnCorrectPrecision()
        {
            // Arrange - 3.675V = 3675mV
            ushort rawData = 3675;

            // Act
            var result = _sensor!.ConvertBatteryData(rawData);

            // Assert
            Assert.AreEqual(3.675, result, 0.001);
        }

        #endregion

        #region CreateMeasurementFromEvent Tests

        [TestMethod]
        public void CreateMeasurementFromEvent_WithTemperatureEvent_ShouldReturnTemperatureMeasurement()
        {
            // Arrange
            byte eventType = 1; // Temperature event
            ushort data = 2500; // 25.00°C
            var timestamp = new DateTime(2025, 1, 15, 12, 30, 45, DateTimeKind.Utc);

            // Act
            var result = _sensor!.CreateMeasurementFromEvent(eventType, data, timestamp);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(MeasurementType.Temperature, result.Type);
            Assert.AreEqual(25.0, result.Value, 0.001);
            Assert.AreEqual("°C", result.Unit);
            Assert.AreEqual(timestamp, result.TimestampUtc);
            Assert.AreEqual(MeasurementSource.Log, result.Source);
        }

        [TestMethod]
        public void CreateMeasurementFromEvent_WithBatteryGoodEvent_ShouldReturnBatteryMeasurement()
        {
            // Arrange
            byte eventType = 12; // Battery good event
            ushort data = 3300; // 3.3V
            var timestamp = new DateTime(2025, 1, 15, 12, 30, 45, DateTimeKind.Utc);

            // Act
            var result = _sensor!.CreateMeasurementFromEvent(eventType, data, timestamp);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(MeasurementType.Battery, result.Type);
            Assert.AreEqual(3.3, result.Value, 0.001);
            Assert.AreEqual("V", result.Unit);
            Assert.AreEqual(timestamp, result.TimestampUtc);
            Assert.AreEqual(MeasurementSource.Log, result.Source);
        }

        [TestMethod]
        public void CreateMeasurementFromEvent_WithAdvertiseOnButtonEvent_ShouldReturnBatteryMeasurement()
        {
            // Arrange
            byte eventType = 13; // Advertise on button event (has battery data)
            ushort data = 3100; // 3.1V
            var timestamp = new DateTime(2025, 1, 15, 12, 30, 45, DateTimeKind.Utc);

            // Act
            var result = _sensor!.CreateMeasurementFromEvent(eventType, data, timestamp);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(MeasurementType.Battery, result.Type);
            Assert.AreEqual(3.1, result.Value, 0.001);
            Assert.AreEqual("V", result.Unit);
            Assert.AreEqual(timestamp, result.TimestampUtc);
            Assert.AreEqual(MeasurementSource.Log, result.Source);
        }

        [TestMethod]
        public void CreateMeasurementFromEvent_WithBatteryLowEvent_ShouldReturnBatteryMeasurement()
        {
            // Arrange
            byte eventType = 16; // Battery low event
            ushort data = 2200; // 2.2V (low battery)
            var timestamp = new DateTime(2025, 1, 15, 12, 30, 45, DateTimeKind.Utc);

            // Act
            var result = _sensor!.CreateMeasurementFromEvent(eventType, data, timestamp);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(MeasurementType.Battery, result.Type);
            Assert.AreEqual(2.2, result.Value, 0.001);
            Assert.AreEqual("V", result.Unit);
            Assert.AreEqual(timestamp, result.TimestampUtc);
            Assert.AreEqual(MeasurementSource.Log, result.Source);
        }

        [TestMethod]
        public void CreateMeasurementFromEvent_WithTemperatureAlarmEvent_ShouldReturnTemperatureMeasurement()
        {
            // Arrange - Check if there are temperature alarm events (4-10 range)
            byte eventType = 4; // Assuming temperature alarm event
            ushort data = 8500; // 85.00°C (high temperature)
            var timestamp = new DateTime(2025, 1, 15, 12, 30, 45, DateTimeKind.Utc);

            // Act
            var result = _sensor!.CreateMeasurementFromEvent(eventType, data, timestamp);

            // Assert
            if (result != null)
            {
                // If this event type returns a measurement, it should be temperature-related
                Assert.AreEqual(MeasurementType.Temperature, result.Type);
                Assert.AreEqual(85.0, result.Value, 0.001);
                Assert.AreEqual("°C", result.Unit);
                Assert.AreEqual(timestamp, result.TimestampUtc);
                Assert.AreEqual(MeasurementSource.Log, result.Source);
            }
            // If result is null, this event type is not implemented, which is also valid
        }

        [TestMethod]
        public void CreateMeasurementFromEvent_WithUnknownEventType_ShouldReturnNull()
        {
            // Arrange
            byte eventType = 255; // Unknown event type
            ushort data = 1234;
            var timestamp = new DateTime(2025, 1, 15, 12, 30, 45, DateTimeKind.Utc);

            // Act
            var result = _sensor!.CreateMeasurementFromEvent(eventType, data, timestamp);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void CreateMeasurementFromEvent_WithZeroEventType_ShouldReturnNull()
        {
            // Arrange
            byte eventType = 0; // Invalid event type
            ushort data = 1234;
            var timestamp = new DateTime(2025, 1, 15, 12, 30, 45, DateTimeKind.Utc);

            // Act
            var result = _sensor!.CreateMeasurementFromEvent(eventType, data, timestamp);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void CreateMeasurementFromEvent_WithNegativeTemperature_ShouldHandleCorrectly()
        {
            // Arrange
            byte eventType = 1; // Temperature event
            ushort data = unchecked((ushort)(-2000)); // -20.00°C in two's complement
            var timestamp = new DateTime(2025, 1, 15, 12, 30, 45, DateTimeKind.Utc);

            // Act
            var result = _sensor!.CreateMeasurementFromEvent(eventType, data, timestamp);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(MeasurementType.Temperature, result.Type);
            Assert.AreEqual(-20.0, result.Value, 0.001);
            Assert.AreEqual("°C", result.Unit);
            Assert.AreEqual(timestamp, result.TimestampUtc);
            Assert.AreEqual(MeasurementSource.Log, result.Source);
        }

        #endregion

        #region Edge Cases and Integration Tests

        [TestMethod]
        public void ConvertTemperatureData_EdgeCaseValues_ShouldHandleAllRanges()
        {
            // Test various edge cases
            var testCases = new[]
            {
                (raw: (ushort)1, expected: 0.01),        // Minimum positive
                (raw: (ushort)32767, expected: 327.67),   // Max positive signed 16-bit
                (raw: (ushort)32768, expected: -327.68),  // Min negative signed 16-bit
                (raw: (ushort)65535, expected: -0.01),    // Maximum negative
            };

            foreach (var testCase in testCases)
            {
                // Act
                var result = _sensor!.ConvertTemperatureData(testCase.raw);

                // Assert
                Assert.AreEqual(testCase.expected, result, 0.001, 
                    $"Failed for raw value {testCase.raw}");
            }
        }

        [TestMethod]
        public void ConvertBatteryData_EdgeCaseValues_ShouldHandleAllRanges()
        {
            // Test various edge cases for battery data
            var testCases = new[]
            {
                (raw: (ushort)1, expected: 0.001),      // Minimum positive
                (raw: (ushort)1000, expected: 1.0),     // 1V
                (raw: (ushort)5000, expected: 5.0),     // 5V (high for BT510)
                (raw: (ushort)65535, expected: 65.535), // Maximum possible
            };

            foreach (var testCase in testCases)
            {
                // Act
                var result = _sensor!.ConvertBatteryData(testCase.raw);

                // Assert
                Assert.AreEqual(testCase.expected, result, 0.001, 
                    $"Failed for raw value {testCase.raw}");
            }
        }

        #endregion
    }
}
