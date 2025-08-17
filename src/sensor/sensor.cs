using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensorGateway.Bluetooth;
using SensorGateway.Gateway;

namespace SensorGateway.Sensors
{
    #region Interface ISensor
    /// <summary>
    /// Represents a generic sensor interface for BLE-based sensor devices
    /// </summary>
    public interface ISensor
    {
        /// <summary>
        /// Downloads the complete log data from the sensor device
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of measurements from the sensor log</returns>
        Task<IEnumerable<Measurement>> DownloadLogAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads the complete log data from the sensor device
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of measurements from the sensor log</returns>
        IEnumerable<Measurement> DownloadLog();


        /// <summary>
        /// Processes raw log data and converts it into structured measurements
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of parsed measurements</returns>
        Task<IEnumerable<Measurement>> ProcessLogAsync(CancellationToken cancellationToken = default);
        /// <summary>
        /// Processes raw log data and converts it into structured measurements
        /// </summary>
        /// <returns>Collection of parsed measurements</returns>
        IEnumerable<Measurement> ProcessLog();

        /// <summary>
        /// Parses BLE advertisement data from the sensor
        /// </summary>
        /// <param name="advertisementData">Raw advertisement data</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of measurements from advertisement (typically 0-1 items)</returns>
        Task<IEnumerable<Measurement>> ParseAdvertisementAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses BLE advertisement data from the sensor
        /// </summary>
        /// <param name="advertisementData">Raw advertisement data</param>
        /// <returns>Collection of measurements from advertisement (typically 0-1 items)</returns>
        IEnumerable<Measurement> ParseAdvertisement();

        /// <summary>
        /// Processes advertisement data and extracts real-time measurements
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of current measurements (typically 0-1 items)</returns>
        Task<IEnumerable<Measurement>> ProcessAdvertisementAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes advertisement data and extracts real-time measurements
        /// </summary>
        /// <returns>Collection of current measurements (typically 0-1 items)</returns>
        IEnumerable<Measurement> ProcessAdvertisement();

        /// <summary>
        /// Retrieves the latest measurements from the sensor
        /// </summary>
        /// <param name="source">Source of measurements (log or advertisement)</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of latest measurements</returns>
        Task<IEnumerable<Measurement>> GetMeasurementsAsync(MeasurementSource source = MeasurementSource.Both, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current configuration from the sensor
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Dictionary containing configuration key-value pairs</returns>
        Task<Dictionary<string, object>> GetConfigurationAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current configuration from the sensor
        /// </summary>
        /// <returns>Dictionary containing configuration key-value pairs</returns>
        Dictionary<string, object> GetConfiguration();

        /// <summary>
        /// Updates the sensor configuration with the provided settings
        /// </summary>
        /// <param name="configuration">Dictionary containing configuration key-value pairs to update</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>True if configuration was successfully updated, false otherwise</returns>
        Task<bool> UpdateConfigurationAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the sensor configuration with the provided settings
        /// </summary>
        /// <param name="configuration">Dictionary containing configuration key-value pairs to update</param>
        /// <returns>True if configuration was successfully updated, false otherwise</returns>
        bool UpdateConfiguration(Dictionary<string, object> configuration);
    }
    #endregion

    #region Sensor Class
    public abstract class Sensor : ISensor, IDisposable
    {
        private bool _disposed = false;
        
        /// <summary>
        /// The type of sensor (e.g., BT510, Dummy)
        /// </summary>
        public SensorType SensorType { get; private set; }

        /// <summary>
        /// The name of the sensor
        /// </summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>
        /// The address of the sensor device
        /// </summary>
        public BTAddress? Address { get; private set; }

        public BTDevice? Device { get; private set; }

        protected Sensor(BTDevice? device, SensorType sensorType)
        {
            SensorType = sensorType;
            Device = device ?? throw new ArgumentNullException(nameof(device), "Device cannot be null");
            Name = Device.Name ?? "Unknown Sensor";
            Address = Device.Address;
        }

        #region ISensor Implementation - Abstract Methods
        
        /// <summary>
        /// Downloads the complete log data from the sensor device
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of measurements from the sensor log</returns>
        public abstract Task<IEnumerable<Measurement>> DownloadLogAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Downloads the complete log data from the sensor device
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of measurements from the sensor log</returns>
        public virtual IEnumerable<Measurement> DownloadLog()
        {
            return DownloadLogAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Processes raw log data and converts it into structured measurements
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of parsed measurements</returns>
        public abstract Task<IEnumerable<Measurement>> ProcessLogAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Processes raw log data and converts it into structured measurements
        /// </summary>
        /// <returns>Collection of parsed measurements</returns>
        public virtual IEnumerable<Measurement> ProcessLog()
        {
            return ProcessLogAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Parses BLE advertisement data from the sensor
        /// </summary>
        /// <param name="advertisementData">Raw advertisement data</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of measurements from advertisement (typically 0-1 items)</returns>
        public abstract Task<IEnumerable<Measurement>> ParseAdvertisementAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Parses BLE advertisement data from the sensor
        /// </summary>
        /// <param name="advertisementData">Raw advertisement data</param>
        /// <returns>Collection of measurements from advertisement (typically 0-1 items)</returns>
        public virtual IEnumerable<Measurement> ParseAdvertisement()
        {
            return ParseAdvertisementAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Processes advertisement data and extracts real-time measurements
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of current measurements (typically 0-1 items)</returns>
        public abstract Task<IEnumerable<Measurement>> ProcessAdvertisementAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Processes advertisement data and extracts real-time measurements
        /// </summary>
        /// <returns>Collection of current measurements (typically 0-1 items)</returns>
        public virtual IEnumerable<Measurement> ProcessAdvertisement()
        {
            return ProcessAdvertisementAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Retrieves the latest measurements from the sensor
        /// </summary>
        /// <param name="source">Source of measurements (log or advertisement)</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Collection of latest measurements</returns>
        public abstract Task<IEnumerable<Measurement>> GetMeasurementsAsync(MeasurementSource source = MeasurementSource.Both, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current configuration from the sensor
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Dictionary containing configuration key-value pairs</returns>
        public abstract Task<Dictionary<string, object>> GetConfigurationAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets the current configuration from the sensor
        /// </summary>
        /// <returns>Dictionary containing configuration key-value pairs</returns>
        public virtual Dictionary<string, object> GetConfiguration()
        {
            return GetConfigurationAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Updates the sensor configuration with the provided settings
        /// </summary>
        /// <param name="configuration">Dictionary containing configuration key-value pairs to update</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>True if configuration was successfully updated, false otherwise</returns>
        public abstract Task<bool> UpdateConfigurationAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Updates the sensor configuration with the provided settings
        /// </summary>
        /// <param name="configuration">Dictionary containing configuration key-value pairs to update</param>
        /// <returns>True if configuration was successfully updated, false otherwise</returns>
        public virtual bool UpdateConfiguration(Dictionary<string, object> configuration)
        {
            return UpdateConfigurationAsync(configuration).GetAwaiter().GetResult();
        }

        #endregion

        #region IDisposable Implementation
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                Device?.Dispose();
                Address?.Dispose();
            }

            Device = null;
            Address = null;
            Name = string.Empty;
            
            _disposed = true;
        }

        #endregion
    }
    #endregion

    #region Sensor Type Enum
    /// <summary>
    /// Supported sensor types
    /// </summary>
    public enum SensorType
    {
        /// <summary>
        /// BT510 Bluetooth sensor device
        /// </summary>
        BT510,

        /// <summary>
        /// Dummy sensor for testing and development
        /// </summary>
        Dummy
    }
    #endregion

    #region Sensor Exceptions
    /// <summary>
    /// Base exception for sensor-related errors
    /// </summary>
    public class SensorException : Exception
    {
        public SensorException(string message) : base(message) { }
        public SensorException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when sensor communication fails
    /// </summary>
    public class SensorCommunicationException : SensorException
    {
        public SensorCommunicationException(string message) : base(message) { }
        public SensorCommunicationException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when sensor data parsing fails
    /// </summary>
    public class SensorDataException : SensorException
    {
        public SensorDataException(string message) : base(message) { }
        public SensorDataException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when sensor configuration operations fail
    /// </summary>
    public class SensorConfigurationException : SensorException
    {
        public SensorConfigurationException(string message) : base(message) { }
        public SensorConfigurationException(string message, Exception innerException) : base(message, innerException) { }
    }
    #endregion
}