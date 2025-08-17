namespace SensorGateway.Configuration
{
    /// <summary>
    /// Application logging configuration.
    /// </summary>
    public class LoggingConfig
    {
        /// <summary>
        /// Logging level: Debug, Info, Warning, Error
        /// </summary>
        public string LogLevel { get; set; } = "Info";

        /// <summary>
        /// Enable console logging
        /// </summary>
        public bool EnableConsoleLogging { get; set; } = true;

        /// <summary>
        /// Enable file logging
        /// </summary>
        public bool EnableFileLogging { get; set; } = false;

        /// <summary>
        /// Log file path (empty for default)
        /// </summary>
        public string LogFilePath { get; set; } = "";

        /// <summary>
        /// Maximum log file size in MB
        /// </summary>
        public int MaxLogFileSizeMB { get; set; } = 10;

        /// <summary>
        /// Number of log files to keep
        /// </summary>
        public int MaxLogFiles { get; set; } = 5;
    }
}