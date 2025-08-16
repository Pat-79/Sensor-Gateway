namespace GatewaySensor.Configuration
{
    /// <summary>
    /// Data storage and persistence configuration.
    /// </summary>
    public class StorageConfig
    {
        /// <summary>
        /// Data storage directory path
        /// </summary>
        public string DataDirectory { get; set; } = "./data";

        /// <summary>
        /// Database connection string (if using database storage)
        /// </summary>
        public string DatabaseConnectionString { get; set; } = "";

        /// <summary>
        /// Storage type: File, Database, Memory
        /// </summary>
        public string StorageType { get; set; } = "File";

        /// <summary>
        /// Data retention period in days (0 = keep forever)
        /// </summary>
        public int DataRetentionDays { get; set; } = 30;

        /// <summary>
        /// Enable data compression
        /// </summary>
        public bool EnableCompression { get; set; } = true;
    }
}