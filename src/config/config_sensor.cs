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

        /// <summary>
        /// BLE service UUID for Laird custom service
        /// </summary>
        public string CustomServiceUuid { get; set; } = "569a1101-b87f-490c-92cb-11ba5ea5167c";

        /// <summary>
        /// BLE characteristic UUID for JSON-RPC responses
        /// </summary>
        public string JsonRpcResponseCharUuid { get; set; } = "569a2000-b87f-490c-92cb-11ba5ea5167c";

        /// <summary>
        /// BLE characteristic UUID for JSON-RPC commands
        /// </summary>
        public string JsonRpcCommandCharUuid { get; set; } = "569a2001-b87f-490c-92cb-11ba5ea5167c";

        /// <summary>
        /// Size of each log entry in bytes (BT510 uses 8-byte events)
        /// </summary>
        public int LogEntrySize { get; set; } = 8;

        /// <summary>
        /// Communication setup delay in milliseconds
        /// </summary>
        public int CommunicationSetupDelayMs { get; set; } = 100;
    }
}