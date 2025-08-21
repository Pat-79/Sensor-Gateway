using System;

namespace SensorGateway.Configuration
{
    /// <summary>
    /// Configuration for the Bluetooth Agent
    /// </summary>
    public class AgentConfig
    {
        /// <summary>
        /// Fixed PIN code for device authentication
        /// </summary>
        public string FixedPin { get; set; } = "123456";

        /// <summary>
        /// D-Bus agent object path
        /// </summary>
        public string AgentPath { get; set; } = "/org/sensorgateway/agent";

        /// <summary>
        /// Agent capability (KeyboardDisplay, DisplayOnly, etc.)
        /// </summary>
        public string AgentCapability { get; set; } = "KeyboardDisplay";

        /// <summary>
        /// Automatically authorize devices and services
        /// </summary>
        public bool AutoAuthorizeDevices { get; set; } = true;

        /// <summary>
        /// Interval between agent registration checks (seconds)
        /// </summary>
        public int ReregistrationIntervalSeconds { get; set; } = 30;
    }
}