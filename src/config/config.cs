using System;
using System.IO;
using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GatewaySensor.Configuration
{
    /// <summary>
    /// Static configuration manager that loads application settings from a YAML file.
    /// This class is initialized at application startup and provides global access
    /// to configuration settings throughout the application lifecycle.
    /// </summary>
    public static class AppConfig
    {
        #region Private Fields

        private static readonly object _lockObject = new object();
        private static bool _isInitialized = false;
        private const string DEFAULT_CONFIG_FILE = "appsettings.yml";

        #endregion

        #region Configuration Properties

        public static BluetoothConfig Bluetooth { get; private set; } = new BluetoothConfig();
        public static SensorConfig Sensors { get; private set; } = new SensorConfig();
        public static LoggingConfig Logging { get; private set; } = new LoggingConfig();
        public static StorageConfig Storage { get; private set; } = new StorageConfig();
        public static NetworkConfig Network { get; private set; } = new NetworkConfig();

        #endregion

        #region Initialization Methods

        static AppConfig()
        {
            Initialize();
        }

        public static void Initialize()
        {
            Initialize(DEFAULT_CONFIG_FILE);
        }

        public static void Initialize(string configFilePath, bool forceReload = false)
        {
            lock (_lockObject)
            {
                if (_isInitialized && !forceReload)
                    return;

                try
                {
                    LoadConfiguration(configFilePath);
                    _isInitialized = true;
                    Console.WriteLine($"✅ Configuration loaded successfully from: {configFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to load configuration from {configFilePath}: {ex.Message}");
                    Console.WriteLine("🔄 Using default configuration values");
                    LoadDefaultConfiguration();
                    _isInitialized = true;
                }
            }
        }

        public static void Reload() => Initialize(DEFAULT_CONFIG_FILE, forceReload: true);
        public static void Reload(string configFilePath) => Initialize(configFilePath, forceReload: true);

        #endregion

        #region Private Helper Methods

        private static void LoadConfiguration(string configFilePath)
        {
            string fullPath = FindConfigurationFile(configFilePath);
            
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Configuration file not found: {fullPath}");

            string yamlContent = File.ReadAllText(fullPath);
            
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var configData = deserializer.Deserialize<ConfigurationData>(yamlContent);
            ApplyConfiguration(configData);
        }

        private static string FindConfigurationFile(string fileName)
        {
            string[] searchPaths = {
                Path.Combine(Directory.GetCurrentDirectory(), fileName),
                Path.Combine(AppContext.BaseDirectory, fileName),
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", fileName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GatewaySensor", fileName),
                Path.Combine("/etc/gatewaysensor/", fileName),
                fileName
            };

            foreach (string path in searchPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return searchPaths[0];
        }

        private static void ApplyConfiguration(ConfigurationData? configData)
        {
            if (configData == null)
            {
                LoadDefaultConfiguration();
                return;
            }

            Bluetooth = configData.Bluetooth ?? new BluetoothConfig();
            Sensors = configData.Sensors ?? new SensorConfig();
            Logging = configData.Logging ?? new LoggingConfig();
            Storage = configData.Storage ?? new StorageConfig();
            Network = configData.Network ?? new NetworkConfig();
        }

        private static void LoadDefaultConfiguration()
        {
            Bluetooth = new BluetoothConfig();
            Sensors = new SensorConfig();
            Logging = new LoggingConfig();
            Storage = new StorageConfig();
            Network = new NetworkConfig();
        }

        #endregion

        #region Utility Methods

        public static void CreateSampleConfigFile(string filePath = DEFAULT_CONFIG_FILE)
        {
            var sampleConfig = new ConfigurationData
            {
                Bluetooth = new BluetoothConfig(),
                Sensors = new SensorConfig(),
                Logging = new LoggingConfig(),
                Storage = new StorageConfig(),
                Network = new NetworkConfig()
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            string yamlContent = serializer.Serialize(sampleConfig);
            File.WriteAllText(filePath, yamlContent);
            Console.WriteLine($"✅ Sample configuration file created: {filePath}");
        }

        public static string ToYaml()
        {
            var currentConfig = new ConfigurationData
            {
                Bluetooth = Bluetooth,
                Sensors = Sensors,
                Logging = Logging,
                Storage = Storage,
                Network = Network
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            return serializer.Serialize(currentConfig);
        }

        #endregion
    }

    /// <summary>
    /// Root configuration data structure for YAML deserialization.
    /// </summary>
    internal class ConfigurationData
    {
        public BluetoothConfig? Bluetooth { get; set; }
        public SensorConfig? Sensors { get; set; }
        public LoggingConfig? Logging { get; set; }
        public StorageConfig? Storage { get; set; }
        public NetworkConfig? Network { get; set; }
    }
}