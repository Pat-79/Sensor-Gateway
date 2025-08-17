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
    #region BTDevice Class

    public partial class BTDevice
    {
        #region private fields
        const int WAIT_LOOP_DELAY = 100;
        Device? _device = null;
        Adapter? _adapter = null;

        private readonly MemoryStream _dataBuffer = new MemoryStream();
        private readonly SemaphoreSlim _bufferSemaphore = new SemaphoreSlim(1, 1);
        IGattService1? _service = null;
        GattCharacteristic? _commandChar = null;
        GattCharacteristic? _responseChar = null;
        BTToken? _token = null;
        private bool _communicationInProgress = false;
        #endregion

        #region public properties
        public bool CommunicationInProgress => _communicationInProgress;


        /// <summary>
        /// Gets the current size of the data buffer in bytes.
        /// </summary>
        /// <value>The current buffer size in bytes.</value>
        public long BufferSize
        {
            get
            {
                return GetBufferSizeAsync().GetAwaiter().GetResult();
            }
        }
        #endregion

        #region private methods
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
                await _adapter.SetPoweredAsync(true);

                // Poll for power state instead of fixed delay
                var timeout = TimeSpan.FromSeconds(5);
                var start = DateTime.UtcNow;

                while (!await _adapter.GetPoweredAsync() && DateTime.UtcNow - start < timeout)
                {
                    await Task.Delay(WAIT_LOOP_DELAY);
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
        private async Task InitializeDeviceAsync()
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
            _device = Address != null
                ? await _adapter.GetDeviceAsync(Address.ToString())
                : throw new InvalidOperationException("Device address is not set.");

            if (_device == null)
            {
                throw new InvalidOperationException($"Bluetooth device with address '{Address}' not found.");
            }
        }

        /// <summary>
        /// Asynchronously retrieves the current buffer contents as a byte array.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// a copy of the current buffer data as a byte array.
        /// </returns>
        public async Task<byte[]> GetBufferDataAsync()
        {
            await _bufferSemaphore.WaitAsync();
            try
            {
                return _dataBuffer.ToArray();
            }
            finally
            {
                _bufferSemaphore.Release();
            }
        }

        /// <summary>
        /// Synchronously retrieves the current buffer contents as a byte array.
        /// </summary>
        /// <returns>A copy of the current buffer data as a byte array.</returns>
        public byte[] GetBufferData()
        {
            return GetBufferDataAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously gets the current size of the data buffer in bytes.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// the current buffer size in bytes.
        /// </returns>
        public async Task<long> GetBufferSizeAsync()
        {
            await _bufferSemaphore.WaitAsync();
            try
            {
                return _dataBuffer.Length;
            }
            finally
            {
                _bufferSemaphore.Release();
            }
        }

        /// <summary>
        /// Asynchronously clears all data from the internal buffer and resets its position.
        /// This method provides thread-safe buffer cleanup functionality.
        /// </summary>
        /// <returns>A task that represents the asynchronous buffer clearing operation.</returns>
        public async Task ClearBufferAsync()
        {
            await _bufferSemaphore.WaitAsync();
            try
            {
                _dataBuffer.SetLength(0);
                _dataBuffer.Position = 0;
            }
            finally
            {
                _bufferSemaphore.Release();
            }
        }

        /// <summary>
        /// Synchronously clears all data from the internal buffer.
        /// </summary>
        public void ClearBuffer()
        {
            ClearBufferAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Efficiently appends byte data to the internal buffer in a thread-safe manner.
        /// </summary>
        /// <param name="data">The byte array to append to the buffer. Null or empty arrays are ignored.</param>
        /// <returns>A task that represents the asynchronous append operation.</returns>
        private async Task AppendToBufferAsync(byte[] data)
        {
            if (data?.Length > 0)
            {
                await _bufferSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    await _dataBuffer.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                }
                finally
                {
                    _bufferSemaphore.Release();
                }
            }
        }

        /// <summary>
        /// Handles incoming notification data from BLE characteristics in a thread-safe manner.
        /// This method processes characteristic value changes, appends data to the internal buffer,
        /// and triggers the NotificationDataReceived event for subscribers.
        /// </summary>
        /// <param name="characteristic">The BLE characteristic that sent the notification.</param>
        /// <param name="e">Event arguments containing the notification data and metadata.</param>
        /// <returns>A task that represents the asynchronous notification processing operation.</returns>
        private async Task ReceiveNotificationData(GattCharacteristic characteristic, GattCharacteristicValueEventArgs e)
        {
            try
            {
                // Instead of direct buffer access, use your helper method
                await AppendToBufferAsync(e.Value);

                // Trigger the notification event
                OnNotificationDataReceived(e.Value);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in ReceiveNotificationData: {ex}");
            }
        }

        #endregion

        #region public methods

        #region Device Management Methods

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

            const int maxAttempts = 3;
            const int stabilizationDelay = 2000;
            const int retryDelay = 1000;

            // Try to connect to the device with retry logic
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await _device.ConnectAsync();
                    await Task.Delay(stabilizationDelay);

                    if (await IsConnectedAsync())
                    {
                        _token = await BTManager.Instance.GetTokenAsync(TimeSpan.FromSeconds(120));
                        return _token != null;
                    }
                }
                //catch (Exception ex) when (attempt < maxAttempts)
                catch when (attempt < maxAttempts)
                {
                    // Log exception for debugging (optional)
                    // Console.WriteLine($"Connection attempt {attempt} failed: {ex.Message}");
                    await Task.Delay(retryDelay);
                    continue;
                }
                // Let the final attempt throw the exception naturally
            }

            throw new InvalidOperationException($"Failed to connect to device '{Address}' after {maxAttempts} attempts.");
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

            await ClearBufferAsync();
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
            if (Address == null)
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

        #region Serive and Characteristic Management
        /// <summary>
        /// Asynchronously retrieves a list of all services available on the connected Bluetooth device.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is a read-only list of
        /// service UUIDs available on the device.
        /// </returns>
        public async Task<IReadOnlyList<string>> GetServicesAsync()
        {
            if (!await IsConnectedAsync())
            {
                await ConnectAsync();
            }

            var services = await _device.GetServicesAsync();
            var serviceUuids = await Task.WhenAll(services.Select(s => s.GetUUIDAsync()));
            return serviceUuids.ToList().AsReadOnly();
        }

        /// <summary>
        /// Synchronously retrieves a list of all services available on the connected Bluetooth device.
        /// </summary>
        /// <returns>
        /// A read-only list of service UUIDs available on the device.
        /// </returns>
        public IReadOnlyList<string> GetServices()
        {
            return GetServicesAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously checks if the device has a specific service identified by the provided UUID.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to check.</param>
        public async Task<bool> HasServiceAsync(string serviceUuid)
        {
            var uuid = BlueZManager.NormalizeUUID(serviceUuid);
            var service = await GetServiceAsync(uuid);
            return service != null; // Return true if the service is found, false otherwise
        }

        /// <summary>
        /// Synchronously checks if the device has a specific service identified by the provided UUID.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to check.</param>
        public bool HasService(string serviceUuid)
        {
            // Use the asynchronous method to check for service existence
            return HasServiceAsync(serviceUuid).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously retrieves a specific service by its UUID from the connected device.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to retrieve.</param>
        /// <returns>
        /// An instance of <see cref="IGattService1"/> representing the service, or null if the service is not found.
        /// </returns>
        public async Task<IGattService1?> GetServiceAsync(string serviceUuid)
        {
            var uuid = BlueZManager.NormalizeUUID(serviceUuid);
            if (string.IsNullOrEmpty(uuid))
            {
                return null;
            }

            if (!await IsConnectedAsync())
            {
                await ConnectAsync();
            }

            return await _device.GetServiceAsync(uuid);
        }

        /// <summary>
        /// Synchronously retrieves a specific service by its UUID from the connected device.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to retrieve.</param>
        /// <returns>
        /// An instance of <see cref="IGattService1"/> representing the service, or null if the service is not found.
        /// </returns>
        public IGattService1? GetService(string serviceUuid)
        {
            var uuid = BlueZManager.NormalizeUUID(serviceUuid);
            return GetServiceAsync(uuid).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Checks if the device has a specific characteristic within a service identified by the provided UUIDs.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to check</param>
        /// <param name="characteristicUuid">The UUID of the characteristic to check</param>
        /// <returns>True if the characteristic exists within the service, otherwise false</returns>
        /// <exception cref="NotImplementedException">This method is not yet implemented.</exception>
        public async Task<bool> HasCharacteristicAsync(IGattService1 service, string characteristicUuid)
        {
            var uuid = BlueZManager.NormalizeUUID(characteristicUuid);
            var characteristic = await service.GetCharacteristicAsync(uuid);
            return characteristic != null; // Return true if the characteristic is found, false otherwise
        }

        /// <summary>
        /// Synchronously checks if the device has a specific characteristic within a service identified by the provided
        /// UUIDs.
        /// </summary>
        /// <param name="service">The service to check for the characteristic</param>
        /// <param name="characteristicUuid">The UUID of the characteristic to check</param>
        /// <returns>
        /// True if the characteristic exists within the service, otherwise false
        /// </returns>
        public bool HasCharacteristic(IGattService1 service, string characteristicUuid)
        {
            var uuid = BlueZManager.NormalizeUUID(characteristicUuid);
            return HasCharacteristicAsync(service, uuid).GetAwaiter().GetResult();
        }


        /// <summary>
        /// Asynchronously retrieves a list of all characteristics within a service identified by the provided UUID.
        /// </summary>
        /// <param name="service">The service from which to retrieve characteristics</param>
        /// <returns>
        /// A list of GUIDs representing the characteristics within the specified service
        /// </returns>
        public async Task<IReadOnlyList<string>> GetCharacteristicsAsync(string service)
        {
            var uuid = BlueZManager.NormalizeUUID(service);
            if (string.IsNullOrEmpty(uuid))
            {
                return new List<string>().AsReadOnly();
            }

            if (!await IsConnectedAsync())
            {
                await ConnectAsync();
            }

            var gattService = await GetServiceAsync(uuid);
            if (gattService == null)
            {
                return new List<string>().AsReadOnly();
            }

            // Retrieve all characteristics from the service
            var characteristics = await gattService.GetCharacteristicsAsync();

            // Extract and return their UUIDs
            var characteristicUuids = await Task.WhenAll(
                characteristics.Select(c => c.GetUUIDAsync())
            );

            return characteristicUuids.ToList().AsReadOnly();
        }

        /// <summary>
        /// Synchronously retrieves a list of all characteristics within a service identified by the provided UUID.
        /// </summary>
        /// <param name="service">The service from which to retrieve characteristics</param>
        /// <returns>
        /// A list of GUIDs representing the characteristics within the specified service
        /// </returns>
        public IReadOnlyList<string> GetCharacteristics(string service)
        {
            return GetCharacteristicsAsync(service).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously retrieves a specific characteristic by its UUID from a service on the connected device.
        /// </summary>
        /// <param name="service">The service from which to retrieve the characteristic</param>
        /// <param name="characteristicUuid">The UUID of the characteristic to retrieve</param>
        public async Task<GattCharacteristic?> GetCharacteristicAsync(IGattService1 service, string characteristicUuid)
        {
            var uuid = BlueZManager.NormalizeUUID(characteristicUuid);
            if (string.IsNullOrEmpty(uuid))
            {
                return null;
            }

            if (!await IsConnectedAsync())
            {
                await ConnectAsync();
            }

            return await service.GetCharacteristicAsync(uuid);
        }

        /// <summary>
        /// Synchronously retrieves a specific characteristic by its UUID from a service on the connected device.
        /// </summary>
        /// <param name="service">The service from which to retrieve the characteristic</param>
        /// <param name="characteristicUuid">The UUID of the characteristic to retrieve</param>
        public GattCharacteristic? GetCharacteristic(IGattService1 service, string characteristicUuid)
        {
            var uuid = BlueZManager.NormalizeUUID(characteristicUuid);
            return service.GetCharacteristicAsync(uuid).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sets up BLE notifications for the response characteristic and handles initial data retrieval.
        /// This method subscribes to characteristic value changes and attempts to retrieve any existing data.
        /// Initial data retrieval failure is handled gracefully and won't block the setup process.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the response characteristic is not initialized before calling this method.
        /// </exception>
        /// <returns>A task that represents the asynchronous notification setup operation.</returns>
        public async Task SetNotificationsAsync(string serviceUuid, string characteristicUuid)
        {
            // Ensure that we're not interfering with another operation
            if (CommunicationInProgress)
            {
                throw new InvalidOperationException("Communication is already in progress. Please wait for the current operation to complete.");
            }

            // Connect to service and characteristic
            _service = await GetServiceAsync(serviceUuid);
            if (_service == null)
            {
                throw new InvalidOperationException($"Service '{serviceUuid}' not found on device '{Address}'.");
            }

            _responseChar = await GetCharacteristicAsync(_service, characteristicUuid);
            if (_responseChar == null)
            {
                throw new InvalidOperationException("Response characteristic not initialized");
            }

            // Clear the data buffer before starting notifications
            await ClearBufferAsync();

            // Subscribe to notifications
            _responseChar.Value += ReceiveNotificationData;

            // Try to get initial value, but don't let failure block setup
            try
            {
                var initialData = await _responseChar.GetValueAsync();
                if (initialData?.Length > 0)
                {
                    // Use ConfigureAwait(false) for better performance in library code
                    await AppendToBufferAsync(initialData).ConfigureAwait(false);
                }
            }
            catch //(Exception ex)
            {
                // Log the exception but don't fail setup - initial data is optional
            }
        }

        /// <summary>
        /// Sets up BLE notifications for the response characteristic and handles initial data retrieval.
        /// This method subscribes to characteristic value changes and attempts to retrieve any existing data.
        /// Initial data retrieval failure is handled gracefully and won't block the setup process.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the response characteristic is not initialized before calling this method.
        /// </exception>
        /// <returns>A task that represents the asynchronous notification setup operation.</returns>
        public void SetNotifications(string serviceUuid, string characteristicUuid)
        {
            SetNotificationsAsync(serviceUuid, characteristicUuid).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sets the command characteristic for the device, allowing it to send commands to the device.
        /// </summary>
        /// <param name="characteristicUuid">The UUID of the command characteristic to set</param>
        public async Task SetCommandCharacteristicAsync(string characteristicUuid)
        {
            // Ensure that we're not interfering with another operation
            if (CommunicationInProgress)
            {
                throw new InvalidOperationException("Communication is already in progress. Please wait for the current operation to complete.");
            }

            // Connect to service and characteristic
            if (_service == null)
            {
                throw new InvalidOperationException($"Service not initialized. Call SetNotificationsAsync first to initialize the service connection.");
            }

            characteristicUuid = BlueZManager.NormalizeUUID(characteristicUuid);
            _commandChar = await GetCharacteristicAsync(_service, characteristicUuid);
            if (_commandChar == null)
            {
                throw new InvalidOperationException($"Command characteristic '{characteristicUuid}' not found in service.");
            }
        }

        /// <summary>
        /// Sets the command characteristic for the device, allowing it to send commands to the device.
        /// </summary>
        /// <param name="characteristicUuid">The UUID of the command characteristic to set</param>
        public void SetCommandCharacteristic(string characteristicUuid)
        {
            SetCommandCharacteristicAsync(characteristicUuid).GetAwaiter().GetResult();
        }

        /// <summary>
        /// WriteWithoutResponse replaces the characteristic value with a new value. The
        /// call will return before all data has been written. A limited number of such
        /// writes can be in flight at any given time. This call is also known as a
        /// "write command" (as opposed to a write request).
        /// </summary>
        /// <param name="data">The data to write to the characteristic</param>
        /// <param name="stopCommunication">If true, stops communication after writing</param>
        /// <exception cref="ArgumentException">Thrown when data is null or empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the device is not connected or another communication operation is in progress.
        /// </exception>
        /// <exception cref="NotImplementedException">This method is not yet implemented.</exception>
        public async Task WriteWithoutResponseAsync(byte[] data, bool stopCommunication = true)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Data cannot be null or empty", nameof(data));
            }

            // Ensure the command characteristic is set before writing
            if (_commandChar == null)
            {
                throw new InvalidOperationException("Command characteristic is not initialized. Please set the command characteristic before writing data.");
            }

            // Ensure the device is connected before writing
            if (!await IsConnectedAsync())
            {
                await ConnectAsync();
            }

            // Check if communication is already in progress
            if (CommunicationInProgress)
            {
                throw new InvalidOperationException("Communication is already in progress. Please wait for the current operation to complete.");
            }

            await StartCommunicationAsync();

            await _commandChar.WriteValueAsync(data, new Dictionary<string, object>())
                .ContinueWith(t =>
                {
                    if (stopCommunication)
                    {
                        StopCommunication();
                    }
                    if (t.IsFaulted)
                    {
                        // Handle any exceptions that occurred during the write operation
                        Console.Error.WriteLine($"Error writing data: {t.Exception?.GetBaseException().Message}");
                    }
                });
        }

        public void StopCommunication()
        {
            // Stop any ongoing communication by resetting the communication flag
            _communicationInProgress = false;
        }

        /// <summary>
        /// Asynchronously starts communication by clearing the buffer and setting the communication flag.
        /// </summary>
        /// <returns>A task that represents the asynchronous communication start operation.</returns>
        public async Task StartCommunicationAsync()
        {
            await ClearBufferAsync();
            _communicationInProgress = true;
        }

        /// <summary>
        /// Synchronously starts communication (compatibility method).
        /// </summary>
        /// <remarks>
        /// This method blocks the calling thread. Consider using StartCommunicationAsync for better performance.
        /// </remarks>
        public void StartCommunication()
        {
            StartCommunicationAsync().GetAwaiter().GetResult();
        }

        #endregion
        #endregion
        #endregion

        #region Event Handlers
        /// <summary>
        /// Handles notification data received from the device and raises the NotificationDataReceived event.
        /// This method is typically called when new data arrives from the device's notifications,
        /// allowing subscribers to process the incoming data asynchronously.
        /// </summary>
        /// <param name="data">The raw notification data received from the device</param>
        protected virtual void OnNotificationDataReceived(byte[] data)
        {
            NotificationDataReceived?.Invoke(this, data);
        }
        #endregion

        // DisposeCommunication method that is called from the Dispose method
        private void DisposeCommunications()
        {
            if (_responseChar != null)
            {
                _responseChar.Value -= ReceiveNotificationData;
            }

            _bufferSemaphore?.Dispose();
            _dataBuffer?.Dispose();
        }
    }
    #endregion

}