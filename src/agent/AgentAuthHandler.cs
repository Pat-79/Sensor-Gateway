using System;
using System.Threading.Tasks;

namespace SensorGateway.Bluetooth.Agent
{
    /// <summary>
    /// Interface for handling authentication requests from Bluetooth devices
    /// </summary>
    public interface IBluetoothAuthenticationHandler
    {
        /// <summary>
        /// Provides a PIN code for device pairing
        /// </summary>
        /// <param name="deviceAddress">The device requesting authentication</param>
        /// <param name="deviceName">The name of the device (if available)</param>
        /// <returns>PIN code as string</returns>
        Task<string> RequestPinCodeAsync(string deviceAddress, string? deviceName = null);

        /// <summary>
        /// Provides a passkey for device pairing (numeric)
        /// </summary>
        /// <param name="deviceAddress">The device requesting authentication</param>
        /// <param name="deviceName">The name of the device (if available)</param>
        /// <returns>Passkey as uint</returns>
        Task<uint> RequestPasskeyAsync(string deviceAddress, string? deviceName = null);

        /// <summary>
        /// Confirms a passkey for numeric comparison pairing
        /// </summary>
        /// <param name="deviceAddress">The device requesting confirmation</param>
        /// <param name="passkey">The passkey to confirm</param>
        /// <param name="deviceName">The name of the device (if available)</param>
        /// <returns>True to confirm, false to reject</returns>
        Task<bool> RequestConfirmationAsync(string deviceAddress, uint passkey, string? deviceName = null);

        /// <summary>
        /// Authorizes a device connection
        /// </summary>
        /// <param name="deviceAddress">The device requesting authorization</param>
        /// <param name="deviceName">The name of the device (if available)</param>
        /// <returns>True to authorize, false to reject</returns>
        Task<bool> RequestAuthorizationAsync(string deviceAddress, string? deviceName = null);

        /// <summary>
        /// Authorizes a service connection
        /// </summary>
        /// <param name="deviceAddress">The device requesting service authorization</param>
        /// <param name="serviceUuid">The UUID of the service</param>
        /// <param name="deviceName">The name of the device (if available)</param>
        /// <returns>True to authorize, false to reject</returns>
        Task<bool> AuthorizeServiceAsync(string deviceAddress, string serviceUuid, string? deviceName = null);
    }
}