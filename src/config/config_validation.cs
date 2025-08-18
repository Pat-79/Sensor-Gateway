using System;
using System.Collections.Generic;
using System.Linq;

namespace SensorGateway.Configuration
{
    /// <summary>
    /// Provides validation for configuration values to ensure they are within acceptable ranges.
    /// </summary>
    public static class ConfigurationValidator
    {
        #region Validation Results
        
        /// <summary>
        /// Represents a configuration validation result.
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
            public List<string> Warnings { get; set; } = new List<string>();
        }
        
        #endregion

        #region Public Validation Methods

        /// <summary>
        /// Validates all configuration sections and returns a comprehensive result.
        /// </summary>
        /// <returns>ValidationResult containing all validation errors and warnings</returns>
        public static ValidationResult ValidateConfiguration()
        {
            var result = new ValidationResult { IsValid = true };

            // Validate each configuration section
            ValidateBluetoothConfig(AppConfig.Bluetooth, result);
            ValidateSensorConfig(AppConfig.Sensors, result);
            ValidateLoggingConfig(AppConfig.Logging, result);
            ValidateStorageConfig(AppConfig.Storage, result);
            ValidateNetworkConfig(AppConfig.Network, result);

            result.IsValid = !result.Errors.Any();
            return result;
        }

        #endregion

        #region Section-Specific Validation

        /// <summary>
        /// Validates Bluetooth configuration parameters.
        /// </summary>
        private static void ValidateBluetoothConfig(BluetoothConfig config, ValidationResult result)
        {
            // Connection timeout validation
            if (config.ConnectionTimeoutSeconds <= 0)
            {
                result.Errors.Add("Bluetooth.ConnectionTimeoutSeconds must be greater than 0");
            }
            else if (config.ConnectionTimeoutSeconds > 300)
            {
                result.Warnings.Add("Bluetooth.ConnectionTimeoutSeconds > 300 seconds may cause excessive wait times");
            }

            // Discovery timeout validation
            if (config.DiscoveryTimeoutSeconds <= 0)
            {
                result.Errors.Add("Bluetooth.DiscoveryTimeoutSeconds must be greater than 0");
            }
            else if (config.DiscoveryTimeoutSeconds > 60)
            {
                result.Warnings.Add("Bluetooth.DiscoveryTimeoutSeconds > 60 seconds may impact performance");
            }

            // Retry attempts validation
            if (config.MaxRetryAttempts < 0)
            {
                result.Errors.Add("Bluetooth.MaxRetryAttempts cannot be negative");
            }
            else if (config.MaxRetryAttempts > 10)
            {
                result.Warnings.Add("Bluetooth.MaxRetryAttempts > 10 may cause excessive delays");
            }

            // Retry delay validation
            if (config.RetryDelayMs < 0)
            {
                result.Errors.Add("Bluetooth.RetryDelayMs cannot be negative");
            }
            else if (config.RetryDelayMs > 30000)
            {
                result.Warnings.Add("Bluetooth.RetryDelayMs > 30 seconds may impact user experience");
            }

            // RSSI threshold validation
            if (config.MinRssiThreshold > 0)
            {
                result.Errors.Add("Bluetooth.MinRssiThreshold should be negative (dBm values are negative)");
            }
            else if (config.MinRssiThreshold < -120)
            {
                result.Warnings.Add("Bluetooth.MinRssiThreshold < -120 dBm may include too many weak signals");
            }

            // Device filters validation
            if (config.DeviceNameFilters?.Count == 0)
            {
                result.Warnings.Add("Bluetooth.DeviceNameFilters is empty - may discover unwanted devices");
            }

            // Service UUID validation
            if (config.ServiceUuidFilters?.Any() == true)
            {
                foreach (var uuid in config.ServiceUuidFilters)
                {
                    if (!IsValidUuid(uuid))
                    {
                        result.Errors.Add($"Invalid UUID format in ServiceUuidFilters: {uuid}");
                    }
                }
            }
        }

        /// <summary>
        /// Validates sensor configuration parameters.
        /// </summary>
        private static void ValidateSensorConfig(SensorConfig config, ValidationResult result)
        {
            // Add sensor-specific validation here when SensorConfig is implemented
            // Example: Temperature ranges, measurement intervals, etc.
        }

        /// <summary>
        /// Validates logging configuration parameters.
        /// </summary>
        private static void ValidateLoggingConfig(LoggingConfig config, ValidationResult result)
        {
            // Add logging-specific validation here when LoggingConfig is implemented
            // Example: Log levels, file paths, rotation settings, etc.
        }

        /// <summary>
        /// Validates storage configuration parameters.
        /// </summary>
        private static void ValidateStorageConfig(StorageConfig config, ValidationResult result)
        {
            // Add storage-specific validation here when StorageConfig is implemented
            // Example: Database connections, file paths, retention policies, etc.
        }

        /// <summary>
        /// Validates network configuration parameters.
        /// </summary>
        private static void ValidateNetworkConfig(NetworkConfig config, ValidationResult result)
        {
            // Add network-specific validation here when NetworkConfig is implemented
            // Example: Ports, URLs, timeout values, etc.
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Validates if a string is a properly formatted UUID.
        /// </summary>
        /// <param name="uuid">The UUID string to validate</param>
        /// <returns>True if the UUID is valid, false otherwise</returns>
        private static bool IsValidUuid(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
                return false;

            return Guid.TryParse(uuid, out _);
        }

        #endregion
    }
}
