using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HashtagChris.DotNetBlueZ;
using SensorGateway.Sensors;

namespace SensorGateway.Bluetooth
{
    /// <summary>
    /// Factory class for creating BTDevice instances from various device sources.
    /// Follows Single Responsibility Principle by focusing solely on device creation.
    /// </summary>
    public static class BTDeviceFactory
    {
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
        #endregion
    }
}
