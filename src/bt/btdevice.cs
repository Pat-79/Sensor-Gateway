using System;
using System.Collections.Generic;
using SensorGateway.Sensors;

namespace SensorGateway.Bluetooth
{

    #region DeviceType Enumeration    
    /// <summary>
    /// Enumeration of supported device types for sensor communication.
    /// Used to determine the appropriate communication protocols and data parsing methods.
    /// </summary>
    public enum DeviceType
    {
        /// <summary>
        /// Laird Connectivity BT510 temperature sensor device.
        /// Supports JSON-RPC communication protocol for configuration and data retrieval.
        /// </summary>
        BT510,

        /// <summary>
        /// Dummy sensor device for testing and development purposes.
        /// Provides simulated data without requiring actual hardware.
        /// </summary>
        Dummy
    }
    #endregion

    #region BTAddress Class
    /// <summary>
    /// Represents a Bluetooth Low Energy (BLE) device abstraction for sensor communication.
    /// This class provides a unified interface for working with different types of BLE sensor devices,
    /// particularly BT510 temperature sensors from Laird Connectivity.
    /// Implements IDisposable to ensure proper cleanup of resources.
    /// </summary>
    public partial class BTDevice : IDisposable
    {
        #region Events
        public delegate void NotificationDataReceivedHandler(object sender, byte[]? data, string uuid);


        /// <summary>
        /// Event triggered when new notification data is received from the device.
        /// This event allows subscribers to handle incoming data asynchronously.
        /// </summary>
        public event NotificationDataReceivedHandler? NotificationDataReceived; // ✅ Add ? for nullable
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the type of device (BT510, Dummy, etc.).
        /// Used to determine the appropriate communication protocol and data parsing methods.
        /// </summary>
        public DeviceType DeviceType { get; set; } = DeviceType.Dummy;

        /// <summary>
        /// Gets or sets the human-readable name of the device as advertised via BLE.
        /// Examples: "DTT-34179", "BT510-12345", "Unknown Device"
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Bluetooth MAC address of the device.
        /// Used for device identification and connection establishment.
        /// </summary>
        public BTAddress? Address { get; set; }

        /// <summary>
        /// Gets or sets the sensor type, which determines data interpretation methods.
        /// Currently supports BT510 temperature sensors and dummy sensors for testing.
        /// </summary>
        public SensorType Type { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for this device instance.
        /// Typically derived from the first service UUID or generated as a GUID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the Bluetooth company identifier from manufacturer data.
        /// Examples: 0x0077 (Laird Connectivity), 0x004C (Apple)
        /// Used for device identification and protocol selection.
        /// </summary>
        public ushort CompanyId { get; set; } = 0;

        /// <summary>
        /// Gets or sets the raw manufacturer-specific advertisement data.
        /// Key: Company ID, Value: Raw advertisement payload bytes
        /// Contains sensor readings, device status, and configuration information.
        /// </summary>
        public Dictionary<ushort, byte[]> AdvertisementData { get; set; } = new Dictionary<ushort, byte[]>();

        /// <summary>
        /// Gets or sets the Received Signal Strength Indicator (RSSI) in dBm.
        /// Indicates the signal strength of the device's BLE advertisements.
        /// Typical range: -100 dBm (very weak) to -30 dBm (very strong).
        /// </summary>
        public short RSSI { get; set; } = 8;
        #endregion

        #region Private Fields
        private bool _disposed = false;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the BTDevice class with specified parameters.
        /// This constructor is used when creating device instances with known properties.
        /// </summary>
        /// <param name="deviceType">The type of device (BT510, Dummy, etc.)</param>
        /// <param name="name">Human-readable device name</param>
        /// <param name="address">Bluetooth MAC address of the device</param>
        /// <param name="type">Sensor type for data interpretation</param>
        /// <param name="id">Unique identifier for the device instance</param>
        public BTDevice(DeviceType deviceType, string name, BTAddress? address, SensorType type, string id)
        {
            DeviceType = deviceType;
            Name = name ?? "Unknown Device";
            Address = address;
            Type = type;
            Id = id ?? Guid.NewGuid().ToString();

            // Initialize device
            InitializeDeviceAsync().GetAwaiter().GetResult();
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Disposes of the BTDevice instance and cleans up associated resources.
        /// This method clears advertisement data, disposes of the BTAddress object,
        /// and resets all properties to default values to prevent memory leaks.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            // Clear advertisement data dictionary to free memory
            AdvertisementData?.Clear();
            if (Address != null)
            {
                Address.Dispose();
                Address = null;
            }

            // Reset string properties to empty to aid garbage collection
            Name = string.Empty;
            Id = string.Empty;

            // Reset to default sensor type
            Type = SensorType.Dummy;

            DisposeCommunications();
            _disposed = true;
        }
        #endregion
    }
    #endregion
}