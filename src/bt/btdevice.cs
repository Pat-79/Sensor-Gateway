using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HashtagChris.DotNetBlueZ;

namespace GatewaySensor.Sensors
{
    #region BTDevice
    /// <summary>
    /// Represents a Bluetooth Low Energy (BLE) device abstraction for sensor communication.
    /// This class provides a unified interface for working with different types of BLE sensor devices,
    /// particularly BT510 temperature sensors from Laird Connectivity.
    /// Implements IDisposable to ensure proper cleanup of resources.
    /// </summary>
    public class BTDevice : IDisposable
    {
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
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Asynchronously creates a BTDevice instance from a generic device object.
        /// This method acts as a dispatcher to type-specific factory methods.
        /// Currently supports BlueZ Device objects from HashtagChris.DotNetBlueZ library.
        /// </summary>
        /// <param name="device">The source device object (must be a BlueZ Device)</param>
        /// <returns>A fully initialized BTDevice instance</returns>
        /// <exception cref="ArgumentNullException">Thrown when device is null</exception>
        /// <exception cref="ArgumentException">Thrown when device type is not supported</exception>
        public static async Task<BTDevice> FromObjectAsync(object device)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device), "Device cannot be null");
            }

            if (device is Device blueZDevice)
            {
                return await FromBlueZDeviceAsync(blueZDevice);
            }

            throw new ArgumentException("Invalid device type. Expected a BlueZ Device object.", nameof(device));
        }

        /// <summary>
        /// Synchronously creates a BTDevice instance from a generic device object.
        /// This is a blocking wrapper around FromObjectAsync() for synchronous contexts.
        /// Use the async version when possible to avoid thread pool blocking.
        /// </summary>
        /// <param name="device">The source device object</param>
        /// <returns>A fully initialized BTDevice instance</returns>
        public static BTDevice FromObject(object device)
        {
            return FromObjectAsync(device).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Synchronously creates a BTDevice instance from a BlueZ Device object.
        /// This is a blocking wrapper around FromBlueZDeviceAsync() for synchronous contexts.
        /// Prefer the async version to avoid blocking the thread pool.
        /// </summary>
        /// <param name="device">The BlueZ Device object</param>
        /// <returns>A fully initialized BTDevice instance</returns>
        public static BTDevice FromBlueZDevice(Device device)
        {
            return FromBlueZDeviceAsync(device).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously creates a BTDevice instance from a BlueZ Device object.
        /// This method efficiently batches multiple async operations to minimize context switching
        /// and provides comprehensive device property extraction including manufacturer data processing.
        /// </summary>
        /// <param name="device">The BlueZ Device object from which to create the BTDevice</param>
        /// <returns>A fully initialized BTDevice instance with all available properties populated</returns>
        /// <exception cref="ArgumentNullException">Thrown when the device parameter is null</exception>
        public static async Task<BTDevice> FromBlueZDeviceAsync(Device device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device), "Device cannot be null");

            // Batch async operations to reduce context switching and improve performance
            // This groups related device property retrieval operations together
            var deviceInfoTask = Task.Run(async () => new
            {
                Name = await device.GetNameAsync() ?? "Unknown Device",
                Address = await device.GetAddressAsync() ?? "",
                UUIDs = await device.GetUUIDsAsync(),
                ManufacturerData = await device.GetManufacturerDataAsync()
            });

            // Run RSSI retrieval concurrently as it may fail on some devices
            var rssiTask = GetRSSISafeAsync(device);

            // Await both operations concurrently for optimal performance
            var deviceInfo = await deviceInfoTask;
            var rssi = await rssiTask;

            // Create BTAddress and determine device properties from gathered data
            var btAddress = new BTAddress(deviceInfo.Address);
            var deviceId = deviceInfo.UUIDs?.FirstOrDefault() ?? Guid.NewGuid().ToString();

            // Analyze manufacturer data to determine device type, sensor type, and company ID
            var (deviceType, sensorType, companyId) = DetermineDeviceProperties(deviceInfo.ManufacturerData);

            // Create BTDevice instance with determined properties
            var btDevice = new BTDevice(deviceType, deviceInfo.Name, btAddress, sensorType, deviceId)
            {
                RSSI = rssi,
                CompanyId = companyId
            };

            // Process manufacturer data more efficiently with pre-allocated dictionary
            if (deviceInfo.ManufacturerData?.Count > 0)
            {
                // Pre-allocate dictionary capacity for better memory performance
                btDevice.AdvertisementData = new Dictionary<ushort, byte[]>(deviceInfo.ManufacturerData.Count);

                // Extract and convert manufacturer data to byte arrays
                foreach (var (key, value) in deviceInfo.ManufacturerData)
                {
                    btDevice.AdvertisementData[key] = value as byte[] ?? Array.Empty<byte>();
                }
            }

            return btDevice;
        }

        #endregion

        #region Helper Methods
        /// <summary>
        /// Safely retrieves the RSSI (Received Signal Strength Indicator) from a BlueZ device.
        /// Some devices may not support RSSI retrieval or may fail intermittently,
        /// so this method provides graceful error handling with a reasonable default value.
        /// </summary>
        /// <param name="device">The BlueZ device to get RSSI from</param>
        /// <returns>The device's RSSI in dBm, or -50 dBm as a default if unavailable</returns>
        private static async Task<short> GetRSSISafeAsync(Device device)
        {
            try
            {
                return await device.GetRSSIAsync();
            }
            catch
            {
                // RSSI might not be available for all devices or connection states
                return -50; // Default reasonable RSSI value (moderate signal strength)
            }
        }

        /// <summary>
        /// Determines device and sensor types along with company ID from manufacturer data.
        /// This method analyzes the manufacturer data dictionary to identify the device
        /// based on known company IDs and returns appropriate device configuration.
        /// </summary>
        /// <param name="manufacturerData">Dictionary of company ID to manufacturer data</param>
        /// <returns>Tuple containing device type, sensor type, and company ID</returns>
        private static (DeviceType deviceType, SensorType sensorType, ushort companyId) DetermineDeviceProperties(
            IDictionary<ushort, object>? manufacturerData)
        {
            if (manufacturerData?.Count > 0)
            {
                // Get the first company ID from manufacturer data
                var firstCompanyId = manufacturerData.Keys.FirstOrDefault();

                // Match known company IDs to device types
                return firstCompanyId switch
                {
                    0x0077 => (DeviceType.BT510, SensorType.BT510, 0x0077),  // Laird Connectivity BT510 sensors
                    0x0000 => (DeviceType.Dummy, SensorType.Dummy, 0x0000),  // Dummy device for testing
                    //0x004C => (DeviceType.BT510, SensorType.BT510, 0x004C),  // Apple (for testing purposes)
                    _ => (DeviceType.BT510, SensorType.BT510, firstCompanyId) // Default to BT510 for unknown companies
                };
            }

            // Default values when no manufacturer data is available
            return (DeviceType.Dummy, SensorType.Dummy, 0x0000);
        }

        /// <summary>
        /// Determines the device type from a generic device object.
        /// This method provides device type detection without full device instantiation,
        /// useful for filtering and categorizing devices during discovery.
        /// </summary>
        /// <param name="device">The device object to analyze</param>
        /// <returns>The determined device type</returns>
        /// <exception cref="ArgumentNullException">Thrown when device is null</exception>
        public static DeviceType DetermineDeviceType(object device)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device), "Device cannot be null");
            }

            if (device is Device)
            {
                return DeviceType.BT510;
            }

            // Here we could add more sophisticated logic to determine device types
            // based on device properties, manufacturer data, service UUIDs, etc.
            // For now, assume dummy type for non-BlueZ devices
            return DeviceType.Dummy;
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
            _disposed = true;
        }

        #endregion
    }
    #endregion

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
}