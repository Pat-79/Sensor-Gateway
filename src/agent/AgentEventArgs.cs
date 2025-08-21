using System;

namespace SensorGateway.Bluetooth.Agent
{
    /// <summary>
    /// Event arguments for agent registration
    /// </summary>
    public class AgentRegisteredEventArgs : EventArgs
    {
        public string AgentPath { get; }
        public string Capability { get; }
        public DateTime RegisteredAt { get; }

        public AgentRegisteredEventArgs(string agentPath, string capability)
        {
            AgentPath = agentPath;
            Capability = capability;
            RegisteredAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Event arguments for agent unregistration
    /// </summary>
    public class AgentUnregisteredEventArgs : EventArgs
    {
        public string AgentPath { get; }
        public bool WasExternal { get; }
        public DateTime UnregisteredAt { get; }

        public AgentUnregisteredEventArgs(string agentPath, bool wasExternal = false)
        {
            AgentPath = agentPath;
            WasExternal = wasExternal;
            UnregisteredAt = DateTime.Now;
        }
    }
}