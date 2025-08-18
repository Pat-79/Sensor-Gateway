using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensorGateway.Configuration;
using HashtagChris.DotNetBlueZ;
using HashtagChris.DotNetBlueZ.Extensions;

namespace SensorGateway.Bluetooth
{
    /// <summary>
    /// Handles Bluetooth device connection management and adapter initialization.
    /// Follows Single Responsibility Principle by focusing solely on connection operations.
    /// </summary>
    public class BTDeviceConnection
    {
        #region Private Connection Fields
        private Device? _device = null;
        private Adapter? _adapter = null;
        private BTToken? _token = null;
        private readonly Func<BTAddress?> _getAddress;
        private readonly Func<Task> _clearBufferAsync;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the current Bluetooth device.
        /// </summary>
        public Device? CurrentDevice => _device;

        /// <summary>
        /// Gets the current Bluetooth adapter.
        /// </summary>
        public Adapter? CurrentAdapter => _adapter;

        /// <summary>
        /// Gets the current BT token.
        /// </summary>
        public BTToken? CurrentToken => _token;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of BTDeviceConnection with necessary dependencies.
        /// </summary>
        /// <param name="getAddress">Function to get the device address</param>
        /// <param name="clearBufferAsync">Function to clear the device buffer</param>
        public BTDeviceConnection(
            Func<BTAddress?> getAddress,
            Func<Task> clearBufferAsync)
        {
            _getAddress = getAddress;
            _clearBufferAsync = clearBufferAsync;
        }
        #endregion

        #region Connection Management Methods

        /// <summary>
        /// Initializes the Bluetooth adapter by setting it up and ensuring it's powered on.
        /// This method handles adapter discovery, power state verification, and timeout management
        /// for reliable adapter initialization.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the specified adapter is not found or fails to power on within the timeout period.
        /// </exception>
        /// <returns>A task that represents the asynchronous adapter initialization operation.</returns>
        private async Task InitializeAdapterAsync()
        {
            _adapter = await BlueZManager.GetAdapterAsync(AppConfig.Bluetooth.AdapterName);
            if (_adapter == null)
            {
                throw new InvalidOperationException($"Bluetooth adapter '{AppConfig.Bluetooth.AdapterName}' not found.");
            }

            // Check and set power state in one operation if needed
            if (!await _adapter.GetPoweredAsync())
            {
                await _adapter.SetPoweredAsync(true).ConfigureAwait(false);

                // Poll for power state instead of fixed delay
                var timeout = TimeSpan.FromSeconds(BTDeviceConstants.ADAPTER_POWER_TIMEOUT_SECONDS);
                var start = DateTime.UtcNow;

                while (!await _adapter.GetPoweredAsync() && DateTime.UtcNow - start < timeout)
                {
                    await Task.Delay(BTDeviceConstants.WAIT_LOOP_DELAY).ConfigureAwait(false);
                }

                if (!await _adapter.GetPoweredAsync())
                {
                    throw new InvalidOperationException($"Failed to power on adapter '{AppConfig.Bluetooth.AdapterName}' within timeout.");
                }
            }
        }

        /// <summary>
        /// Initializes the Bluetooth device by setting up the adapter and preparing the device for communication.
        /// This method ensures the adapter is initialized, validates the device address, and resets communication objects.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the adapter fails to initialize, device address is not set, or the device is not found.
        /// </exception>
        /// <returns>A task that represents the asynchronous device initialization operation.</returns>
        public async Task InitializeDeviceAsync()
        {
            // Ensure the adapter is initialized
            if (_adapter == null)
            {
                await InitializeAdapterAsync();
                if (_adapter == null)
                {
                    throw new InvalidOperationException("Bluetooth adapter is not initialized.");
                }
            }

            // Validate address and get device in one operation
            var address = _getAddress();
            _device = address != null
                ? await _adapter.GetDeviceAsync(address.ToString())
                : throw new InvalidOperationException("Device address is not set.");

            if (_device == null)
            {
                throw new InvalidOperationException($"Bluetooth device with address '{address}' not found.");
            }
        }

        /// <summary>
        /// Asynchronously establishes a connection to the Bluetooth device with retry logic.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous connection operation. The task result is
        /// true if the connection was successful, false otherwise.
        /// </returns>
        public async Task<bool> ConnectAsync()
        {
            // Check if the device is already connected
            if (await IsConnectedAsync())
            {
                return true;
            }

            if (_device == null)
            {
                // Initialize the device if not already done
                await InitializeDeviceAsync();
                if (_device == null)
                {
                    throw new InvalidOperationException("Bluetooth device is not initialized.");
                }
            }

            // Try to connect to the device with retry logic
            for (int attempt = 1; attempt <= BTDeviceConstants.MAX_CONNECTION_ATTEMPTS; attempt++)
            {
                try
                {
                    await _device.ConnectAsync();
                    await Task.Delay(BTDeviceConstants.CONNECTION_STABILIZATION_DELAY);

                    if (await IsConnectedAsync())
                    {
                        _token = await BTManager.Instance.GetTokenAsync(TimeSpan.FromSeconds(BTDeviceConstants.TOKEN_TIMEOUT_SECONDS));
                        return _token != null;
                    }
                }
                //catch (Exception ex) when (attempt < maxAttempts)
                catch when (attempt < BTDeviceConstants.MAX_CONNECTION_ATTEMPTS)
                {
                    // Log exception for debugging (optional)
                    // Console.WriteLine($"Connection attempt {attempt} failed: {ex.Message}");
                    await Task.Delay(BTDeviceConstants.CONNECTION_RETRY_DELAY);
                    continue;
                }
                // Let the final attempt throw the exception naturally
            }

            var address = _getAddress();
            throw new InvalidOperationException($"Failed to connect to device '{address}' after {BTDeviceConstants.MAX_CONNECTION_ATTEMPTS} attempts.");
        }

        /// <summary>
        /// Synchronously establishes a connection to the Bluetooth device.
        /// </summary>
        /// <returns>True if the connection was successful, false otherwise.</returns>
        public bool Connect()
        {
            return ConnectAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously disconnects from the Bluetooth device and performs cleanup operations.
        /// </summary>
        /// <returns>A task that represents the asynchronous disconnection operation.</returns>
        public async Task DisconnectAsync()
        {
            // Early return if already disconnected
            if (!await IsConnectedAsync())
            {
                return;
            }

            try
            {
                if (_device == null)
                {
                    return;
                }
                await _device.DisconnectAsync();
            }
            catch
            {
                // Ignore disconnection errors - we still need to cleanup
            }

            // Always perform cleanup regardless of disconnection success
            if (_token != null)
            {
                await BTManager.Instance.ReturnTokenAsync(_token);
                _token = null;
            }

            await _clearBufferAsync();
        }

        /// <summary>
        /// Synchronously disconnects from the Bluetooth device.
        /// </summary>
        public void Disconnect()
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously checks the current connection status of the Bluetooth device.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous connection status check. The task result is
        /// true if the device is connected, false otherwise.
        /// </returns>
        public async Task<bool> IsConnectedAsync()
        {
            // Check if we have a valid address
            var address = _getAddress();
            if (address == null)
            {
                return false; // No address set, cannot be connected
            }

            // Ensure the adapter and device are initialized
            if (_adapter == null)
            {
                await InitializeDeviceAsync();
            }

            // If the device is already initialized, check its connection status
            if (_device == null)
            {
                await InitializeDeviceAsync();
            }

            // Check if the device is connected
            return await _device.GetConnectedAsync();
        }

        /// <summary>
        /// Synchronously checks the current connection status of the Bluetooth device.
        /// </summary>
        /// <returns>True if the device is connected, false otherwise.</returns>
        public bool IsConnected()
        {
            return IsConnectedAsync().GetAwaiter().GetResult();
        }

        #endregion

        #region IDisposable Support
        /// <summary>
        /// Disposes of the connection resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Disconnect();
            }
            catch
            {
                // Ignore errors during disposal
            }
        }
        #endregion
    }
}
