using System.Collections.Generic;

namespace GatewaySensor.Configuration
{
    /// <summary>
    /// Bluetooth scanning and connection configuration.
    /// </summary>
    public class BluetoothConfig
    {
        /// <summary>
        /// Bluetooth adapter name to use (empty for default)
        /// </summary>
        public string AdapterName { get; set; } = "";

        /// <summary>
        /// Device discovery timeout in seconds
        /// </summary>
        public int DiscoveryTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Connection timeout in seconds
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maximum connection retry attempts
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts in milliseconds
        /// </summary>
        public int RetryDelayMs { get; set; } = 2000;

        /// <summary>
        /// Device name prefixes to scan for
        /// </summary>
        public List<string> DeviceNameFilters { get; set; } = new List<string> { "DTT-", "BT510-" };

        /// <summary>
        /// Service UUIDs to filter devices by
        /// </summary>
        public List<string> ServiceUuidFilters { get; set; } = new List<string> { "569a1101-b87f-490c-92cb-11ba5ea5167c" };

        /// <summary>
        /// Minimum RSSI threshold for device discovery (dBm)
        /// </summary>
        public short MinRssiThreshold { get; set; } = -90;
    }
}