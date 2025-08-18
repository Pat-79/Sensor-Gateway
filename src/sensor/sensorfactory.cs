using System;
using System.Threading;
using System.Threading.Tasks;
using SensorGateway.Bluetooth;
using SensorGateway.Sensors.bt510;

namespace SensorGateway.Sensors
{
    #region Factory Class
    /// <summary>
    /// Factory class for creating sensor instances
    /// </summary>
    public static class SensorFactory
    {
        /// <summary>
        /// Creates a sensor instance based on the specified type
        /// </summary>
        /// <param name="device">Optional BlueZ device object for physical sensors</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when sensorType is not supported</exception>
        public static ISensor Create(object? device = null)
        {
            return CreateAsync(device).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Creates a sensor instance asynchronously based on the specified type
        /// </summary>
        /// <param name="device">Optional BlueZ device object for physical sensors</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>A task that represents the asynchronous creation operation</returns>
        public static async Task<ISensor> CreateAsync(object? device = null, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Sensor creation was canceled.", cancellationToken);
            }

            if (device == null)
            {
                // If no device is provided, throw an exception
                throw new FactoryException("Device cannot be null for physical sensors. Use CreateSensor(SensorType.Dummy) for dummy sensor.");
            }

            var btDevice = BTDeviceFactory.FromObject(device) ?? throw new FactoryException("Failed to create BTDevice from provided object.");

            // Determine the sensor type asynchronously
            SensorType sensorType = await DetermineSensorTypeAwait(device);
            ISensor sensor = sensorType switch
            {
                SensorType.BT510 => new BT510Sensor(btDevice, sensorType),
                SensorType.Dummy => new DummySensor(btDevice, sensorType),
                _ => throw new ArgumentOutOfRangeException(nameof(device), $"Unsupported sensor type: {sensorType}")
            };

            // Perform any async initialization if needed
            if (sensor is IAsyncInitializable asyncSensor)
            {
                await asyncSensor.InitializeAsync(cancellationToken);
            }

            return sensor;
        }

        /// <summary>
        /// Determines the sensor type based on the provided device's manufacturer data
        /// </summary>
        /// <param name="device">The BlueZ device to analyze</param>
        /// <returns>The determined sensor type</returns>
        /// <exception cref="ArgumentNullException">Thrown when device is null</exception>
        /// <remarks>
        /// This method checks the manufacturer data of the device to determine if it is a BT510 sensor or a dummy sensor.
        /// If no manufacturer data is available, it defaults to Dummy sensor.
        /// </remarks>
        /// <exception cref="FactoryException">Thrown if the device cannot be analyzed</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the sensor type is not supported</exception>
        /// <returns>A task that represents the asynchronous operation, with the sensor type as the result</returns>
        public static Task<SensorType> DetermineSensorTypeAwait(object device)
        {
            if (device == null)
            {
                // If no device is provided, throw an exception
                throw new ArgumentNullException(nameof(device), "Device cannot be null");
            }

            // Here we could add logic to determine if the device corresponds to a BT510 or other sensor
            // For now, we assume BT510 if a valid address is provided
            var sensorType = BTDeviceFactory.DetermineDeviceType(device) switch
            {
                DeviceType.BT510 => SensorType.BT510,
                DeviceType.Dummy => SensorType.Dummy,
                _ => throw new ArgumentOutOfRangeException(nameof(device), "Unsupported device type")
            };

            return Task.FromResult(sensorType);
        }
    }
    #endregion

    #region Interface IAsyncInitializable
    /// <summary>
    /// Interface for sensors that require async initialization
    /// </summary>
    public interface IAsyncInitializable
    {
        /// <summary>
        /// Initializes the sensor asynchronously
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>A task that represents the initialization operation</returns>
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
    #endregion

    #region Sensor Exceptions
    /// <summary>
    /// Exception class for sensor factory errors
    /// </summary>
    public class FactoryException : Exception
    {
        public FactoryException(string message) : base(message) { }

        public FactoryException(string message, Exception innerException) : base(message, innerException) { }
    }
    #endregion
}