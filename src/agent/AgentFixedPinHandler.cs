using System;
using System.Threading.Tasks;
using SensorGateway.Configuration;

namespace SensorGateway.Bluetooth.Agent
{
    /// <summary>
    /// Simple authentication handler that returns a fixed PIN for all devices
    /// </summary>
    public class FixedPinAuthenticationHandler : IBluetoothAuthenticationHandler
    {
        private readonly string _fixedPin;
        private readonly bool _autoAuthorize;

        /// <summary>
        /// Initializes with AgentConfig
        /// </summary>
        public FixedPinAuthenticationHandler(AgentConfig? config = null)
        {
            var agentConfig = config ?? new AgentConfig();
            _fixedPin = agentConfig.FixedPin;
            _autoAuthorize = agentConfig.AutoAuthorizeDevices;
            
            Console.WriteLine($"🔐 Fixed PIN authentication handler initialized with PIN: {_fixedPin}");
        }

        /// <summary>
        /// Initializes with BluetoothConfig (extracts nested AgentConfig)
        /// </summary>
        public FixedPinAuthenticationHandler(BluetoothConfig bluetoothConfig) 
            : this(bluetoothConfig?.Agent)
        {
            // Constructor chaining - uses the AgentConfig constructor above
        }

        /// <summary>
        /// Returns the fixed PIN for all devices
        /// </summary>
        public async Task<string> RequestPinCodeAsync(string deviceAddress, string? deviceName = null)
        {
            Console.WriteLine($"🔐 PIN requested for device: {deviceName ?? "Unknown"} ({deviceAddress}) - Providing: {_fixedPin}");
            await Task.Delay(1); // Make it properly async
            return _fixedPin;
        }

        /// <summary>
        /// Returns the fixed PIN as passkey (if numeric)
        /// </summary>
        public async Task<uint> RequestPasskeyAsync(string deviceAddress, string? deviceName = null)
        {
            Console.WriteLine($"🔐 Passkey requested for device: {deviceName ?? "Unknown"} ({deviceAddress}) - Providing: {_fixedPin}");
            
            if (uint.TryParse(_fixedPin, out var passkey))
            {
                await Task.Delay(1);
                return passkey;
            }

            Console.WriteLine($"⚠️ Fixed PIN '{_fixedPin}' is not numeric, rejecting passkey request");
            throw new InvalidOperationException($"Fixed PIN '{_fixedPin}' cannot be used as numeric passkey");
        }

        /// <summary>
        /// Auto-confirms passkey if configured to do so
        /// </summary>
        public async Task<bool> RequestConfirmationAsync(string deviceAddress, uint passkey, string? deviceName = null)
        {
            Console.WriteLine($"🔐 Confirmation requested for device: {deviceName ?? "Unknown"} ({deviceAddress}) with passkey: {passkey:D6}");
            
            if (_autoAuthorize)
            {
                Console.WriteLine("✅ Auto-confirming passkey");
                await Task.Delay(1);
                return true;
            }

            // Could implement more logic here (device whitelist, etc.)
            Console.WriteLine("❌ Auto-confirmation disabled, rejecting");
            await Task.Delay(1);
            return false;
        }

        /// <summary>
        /// Auto-authorizes device if configured to do so
        /// </summary>
        public async Task<bool> RequestAuthorizationAsync(string deviceAddress, string? deviceName = null)
        {
            Console.WriteLine($"🔐 Authorization requested for device: {deviceName ?? "Unknown"} ({deviceAddress})");
            
            if (_autoAuthorize)
            {
                Console.WriteLine("✅ Auto-authorizing device");
                await Task.Delay(1);
                return true;
            }

            Console.WriteLine("❌ Auto-authorization disabled, rejecting");
            await Task.Delay(1);
            return false;
        }

        /// <summary>
        /// Auto-authorizes service if configured to do so
        /// </summary>
        public async Task<bool> AuthorizeServiceAsync(string deviceAddress, string serviceUuid, string? deviceName = null)
        {
            Console.WriteLine($"🔐 Service authorization requested for device: {deviceName ?? "Unknown"} ({deviceAddress}), service: {serviceUuid}");
            
            if (_autoAuthorize)
            {
                Console.WriteLine("✅ Auto-authorizing service");
                await Task.Delay(1);
                return true;
            }

            Console.WriteLine("❌ Service auto-authorization disabled, rejecting");
            await Task.Delay(1);
            return false;
        }
    }
}