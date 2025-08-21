using System;
using System.Linq;
using System.Threading.Tasks;
using Tmds.DBus;
using SensorGateway.Configuration;

namespace SensorGateway.Bluetooth.Agent
{
    /// <summary>
    /// D-Bus object that implements the BlueZ Agent1 interface
    /// </summary>
    [DBusInterface("org.bluez.Agent1")]
    internal class BluetoothAgentObject : IDBusObject
    {
        private readonly BluetoothAgent _agent;
        private readonly IBluetoothAuthenticationHandler _authHandler;

        public ObjectPath ObjectPath { get; }

        public BluetoothAgentObject(BluetoothAgent agent, IBluetoothAuthenticationHandler authHandler)
        {
            _agent = agent;
            _authHandler = authHandler;
            ObjectPath = new ObjectPath(AppConfig.Bluetooth.Agent.AgentPath);
        }

        /// <summary>
        /// Called when the agent is released by BlueZ
        /// </summary>
        public Task ReleaseAsync()
        {
            Console.WriteLine("🔄 Agent Release called - Agent is being unregistered");
            _agent.OnAgentReleased();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Requests a PIN code for device pairing
        /// </summary>
        public async Task<string> RequestPinCodeAsync(ObjectPath device)
        {
            var deviceAddress = ExtractDeviceAddress(device);
            var deviceName = GetDeviceName(device); // Remove await
            
            Console.WriteLine($"🔐 RequestPinCode called for device {deviceName} ({deviceAddress})");
            
            return await _authHandler.RequestPinCodeAsync(deviceAddress, deviceName);
        }

        /// <summary>
        /// Displays a PIN code (for headless operation, just log it)
        /// </summary>
        public Task DisplayPinCodeAsync(ObjectPath device, string pincode)
        {
            var deviceAddress = ExtractDeviceAddress(device);
            Console.WriteLine($"🔐 DisplayPinCode called for device {deviceAddress}, PIN: {pincode}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Requests a passkey for device pairing
        /// </summary>
        public async Task<uint> RequestPasskeyAsync(ObjectPath device)
        {
            var deviceAddress = ExtractDeviceAddress(device);
            var deviceName = GetDeviceName(device); // Remove await
            
            Console.WriteLine($"🔐 RequestPasskey called for device {deviceName} ({deviceAddress})");
            
            return await _authHandler.RequestPasskeyAsync(deviceAddress, deviceName);
        }

        /// <summary>
        /// Displays a passkey
        /// </summary>
        public Task DisplayPasskeyAsync(ObjectPath device, uint passkey, ushort entered)
        {
            var deviceAddress = ExtractDeviceAddress(device);
            Console.WriteLine($"🔐 DisplayPasskey called for device {deviceAddress}, Passkey: {passkey:D6}, Entered: {entered}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Requests confirmation of a passkey
        /// </summary>
        public async Task RequestConfirmationAsync(ObjectPath device, uint passkey)
        {
            var deviceAddress = ExtractDeviceAddress(device);
            var deviceName = GetDeviceName(device); // Remove await
            
            Console.WriteLine($"🔐 RequestConfirmation called for device {deviceName} ({deviceAddress}), Passkey: {passkey:D6}");
            
            var confirmed = await _authHandler.RequestConfirmationAsync(deviceAddress, passkey, deviceName);
            
            if (!confirmed)
            {
                throw new DBusException("org.bluez.Error.Rejected", "Passkey confirmation rejected");
            }
        }

        /// <summary>
        /// Requests authorization for a device
        /// </summary>
        public async Task RequestAuthorizationAsync(ObjectPath device)
        {
            var deviceAddress = ExtractDeviceAddress(device);
            var deviceName = GetDeviceName(device); // Remove await
            
            Console.WriteLine($"🔐 RequestAuthorization called for device {deviceName} ({deviceAddress})");
            
            var authorized = await _authHandler.RequestAuthorizationAsync(deviceAddress, deviceName);
            
            if (!authorized)
            {
                throw new DBusException("org.bluez.Error.Rejected", "Device authorization rejected");
            }
        }

        /// <summary>
        /// Authorizes a service connection
        /// </summary>
        public async Task AuthorizeServiceAsync(ObjectPath device, string uuid)
        {
            var deviceAddress = ExtractDeviceAddress(device);
            var deviceName = GetDeviceName(device); // Remove await
            
            Console.WriteLine($"🔐 AuthorizeService called for device {deviceName} ({deviceAddress}) with UUID {uuid}");
            
            var authorized = await _authHandler.AuthorizeServiceAsync(deviceAddress, uuid, deviceName);
            
            if (!authorized)
            {
                throw new DBusException("org.bluez.Error.Rejected", "Service authorization rejected");
            }
        }

        /// <summary>
        /// Called when an agent request is cancelled
        /// </summary>
        public Task CancelAsync()
        {
            Console.WriteLine("🔄 Agent Cancel called - Request was cancelled or failed");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Extracts device address from D-Bus object path
        /// </summary>
        private static string ExtractDeviceAddress(ObjectPath devicePath)
        {
            // D-Bus path format: /org/bluez/hci0/dev_XX_XX_XX_XX_XX_XX
            var path = devicePath.ToString();
            var lastSegment = path.Split('/').LastOrDefault() ?? "unknown";
            
            if (lastSegment.StartsWith("dev_"))
            {
                return lastSegment.Substring(4).Replace('_', ':');
            }
            
            return lastSegment;
        }

        /// <summary>
        /// Gets device name from D-Bus object path (if possible)
        /// </summary>
        private static string? GetDeviceName(ObjectPath devicePath)
        {
            try
            {
                // This would require D-Bus calls to get device properties
                // For now, return null - the address will be used instead
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}