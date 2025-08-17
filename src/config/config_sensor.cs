namespace SensorGateway.Configuration
{
    /// <summary>
    /// Sensor-specific configuration settings.
    /// </summary>
    public class SensorConfig
    {
        /// <summary>
        /// Default sensor data collection interval in seconds
        /// </summary>
        public int DefaultCollectionIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Maximum number of log entries to read per request
        /// </summary>
        public int MaxLogEntriesPerRequest { get; set; } = 128;

        /// <summary>
        /// Sensor data polling timeout in seconds
        /// </summary>
        public int DataPollingTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Enable dummy sensor for testing
        /// </summary>
        public bool EnableDummySensor { get; set; } = false;

        /// <summary>
        /// BT510-specific configuration
        /// </summary>
        public BT510Config BT510 { get; set; } = new BT510Config();
    }

    /// <summary>
    /// BT510 sensor specific configuration.
    /// </summary>
    public class BT510Config
    {
        /// <summary>
        /// JSON-RPC command timeout in seconds
        /// </summary>
        public int JsonRpcTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maximum number of command retries
        /// </summary>
        public int MaxCommandRetries { get; set; } = 5;

        /// <summary>
        /// MTU (Maximum Transmission Unit) size for BLE communication
        /// </summary>
        public int MtuSize { get; set; } = 244;

        /// <summary>
        /// Command retry delay in milliseconds
        /// </summary>
        public int CommandRetryDelayMs { get; set; } = 300;
    }
}