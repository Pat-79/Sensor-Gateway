namespace GatewaySensor.Configuration
{
    /// <summary>
    /// Network and communication configuration.
    /// </summary>
    public class NetworkConfig
    {
        /// <summary>
        /// Enable HTTP API server
        /// </summary>
        public bool EnableApiServer { get; set; } = false;

        /// <summary>
        /// API server port
        /// </summary>
        public int ApiServerPort { get; set; } = 8080;

        /// <summary>
        /// API server bind address
        /// </summary>
        public string ApiServerAddress { get; set; } = "localhost";

        /// <summary>
        /// Enable MQTT publishing
        /// </summary>
        public bool EnableMqtt { get; set; } = false;

        /// <summary>
        /// MQTT broker configuration
        /// </summary>
        public MqttConfig Mqtt { get; set; } = new MqttConfig();
    }

    /// <summary>
    /// MQTT broker configuration.
    /// </summary>
    public class MqttConfig
    {
        /// <summary>
        /// MQTT broker hostname or IP address
        /// </summary>
        public string BrokerHost { get; set; } = "localhost";

        /// <summary>
        /// MQTT broker port
        /// </summary>
        public int BrokerPort { get; set; } = 1883;

        /// <summary>
        /// MQTT username (empty if not required)
        /// </summary>
        public string Username { get; set; } = "";

        /// <summary>
        /// MQTT password (empty if not required)
        /// </summary>
        public string Password { get; set; } = "";

        /// <summary>
        /// MQTT client ID
        /// </summary>
        public string ClientId { get; set; } = "GatewaySensor";

        /// <summary>
        /// Base topic for publishing sensor data
        /// </summary>
        public string BaseTopic { get; set; } = "sensors";

        /// <summary>
        /// Enable TLS/SSL connection
        /// </summary>
        public bool EnableTls { get; set; } = false;
    }
}