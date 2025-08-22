using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SensorGateway.Sensors;

namespace SensorGateway.Bluetooth
{
    /// <summary>
    /// Interface for Bluetooth Low Energy (BLE) device abstraction for sensor communication.
    /// This interface provides a unified contract for working with different types of BLE sensor devices,
    /// allowing for easy testing, mocking, and implementation swapping.
    /// </summary>
    public interface IBTDevice : IDisposable
    {
        #region Events
        /// <summary>
        /// Event triggered when new notification data is received from the device.
        /// </summary>
        event BTDevice.NotificationDataReceivedHandler? NotificationDataReceived;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the type of device (BT510, Dummy, etc.).
        /// </summary>
        DeviceType DeviceType { get; set; }

        /// <summary>
        /// Gets or sets the human-readable name of the device as advertised via BLE.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the Bluetooth MAC address of the device.
        /// </summary>
        BTAddress? Address { get; set; }

        /// <summary>
        /// Gets or sets the sensor type, which determines data interpretation methods.
        /// </summary>
        SensorType Type { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for this device instance.
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Gets or sets the Bluetooth company identifier from manufacturer data.
        /// </summary>
        ushort CompanyId { get; set; }

        /// <summary>
        /// Gets or sets the raw manufacturer-specific advertisement data.
        /// </summary>
        Dictionary<ushort, byte[]> AdvertisementData { get; set; }

        /// <summary>
        /// Gets or sets the Received Signal Strength Indicator (RSSI) in dBm.
        /// </summary>
        short RSSI { get; set; }
        #endregion

        #region Buffer Management
        /// <summary>
        /// Gets the current size of the data buffer in bytes.
        /// </summary>
        long BufferSize { get; }

        /// <summary>
        /// Asynchronously retrieves the current buffer contents as a byte array.
        /// </summary>
        Task<byte[]> GetBufferDataAsync();

        /// <summary>
        /// Synchronously retrieves the current buffer contents as a byte array.
        /// </summary>
        byte[] GetBufferData();

        /// <summary>
        /// Asynchronously gets the current size of the data buffer in bytes.
        /// </summary>
        Task<long> GetBufferSizeAsync();

        /// <summary>
        /// Asynchronously clears all data from the internal buffer and resets its position.
        /// </summary>
        Task ClearBufferAsync();

        /// <summary>
        /// Synchronously clears all data from the internal buffer.
        /// </summary>
        void ClearBuffer();

        /// <summary>
        /// Asynchronously retrieves buffer contents using memory pooling for high-performance scenarios.
        /// </summary>
        Task<BTMemoryPool.PooledMemoryHandle> GetBufferDataPooledAsync();

        /// <summary>
        /// Synchronously retrieves buffer contents using memory pooling.
        /// </summary>
        BTMemoryPool.PooledMemoryHandle GetBufferDataPooled();
        #endregion

        #region Service Management
        /// <summary>
        /// Gets the currently selected service.
        /// </summary>
        HashtagChris.DotNetBlueZ.IGattService1? CurrentService { get; }

        /// <summary>
        /// Sets the service for the device, allowing it to communicate with the specified service.
        /// </summary>
        void SetService(string serviceUuid);

        /// <summary>
        /// Asynchronously sets the service for the device.
        /// </summary>
        Task SetServiceAsync(string serviceUuid);

        /// <summary>
        /// Asynchronously retrieves a list of all services available on the connected device.
        /// </summary>
        Task<IReadOnlyList<string>> GetServicesAsync();

        /// <summary>
        /// Synchronously retrieves a list of all services available on the connected device.
        /// </summary>
        IReadOnlyList<string> GetServices();

        /// <summary>
        /// Asynchronously checks if the device has a specific service.
        /// </summary>
        Task<bool> HasServiceAsync(string serviceUuid);

        /// <summary>
        /// Synchronously checks if the device has a specific service.
        /// </summary>
        bool HasService(string serviceUuid);

        /// <summary>
        /// Asynchronously retrieves a specific service by its UUID.
        /// </summary>
        Task<HashtagChris.DotNetBlueZ.IGattService1?> GetServiceAsync(string serviceUuid);

        /// <summary>
        /// Synchronously retrieves a specific service by its UUID.
        /// </summary>
        HashtagChris.DotNetBlueZ.IGattService1? GetService(string serviceUuid);

        /// <summary>
        /// Asynchronously retrieves characteristics within a service.
        /// </summary>
        Task<IReadOnlyList<string>> GetCharacteristicsAsync(string service);

        /// <summary>
        /// Synchronously retrieves characteristics within a service.
        /// </summary>
        IReadOnlyList<string> GetCharacteristics(string service);

        /// <summary>
        /// Asynchronously retrieves a specific characteristic.
        /// </summary>
        Task<HashtagChris.DotNetBlueZ.GattCharacteristic?> GetCharacteristicAsync(HashtagChris.DotNetBlueZ.IGattService1 service, string characteristicUuid);

        /// <summary>
        /// Synchronously retrieves a specific characteristic.
        /// </summary>
        HashtagChris.DotNetBlueZ.GattCharacteristic? GetCharacteristic(HashtagChris.DotNetBlueZ.IGattService1 service, string characteristicUuid);

        /// <summary>
        /// Asynchronously checks if the device has a specific characteristic.
        /// </summary>
        Task<bool> HasCharacteristicAsync(HashtagChris.DotNetBlueZ.IGattService1 service, string characteristicUuid);

        /// <summary>
        /// Synchronously checks if the device has a specific characteristic.
        /// </summary>
        bool HasCharacteristic(HashtagChris.DotNetBlueZ.IGattService1 service, string characteristicUuid);
        #endregion

        #region Connection Management
        /// <summary>
        /// Gets the current Bluetooth device.
        /// </summary>
        HashtagChris.DotNetBlueZ.Device? CurrentDevice { get; }

        /// <summary>
        /// Gets the current Bluetooth adapter.
        /// </summary>
        HashtagChris.DotNetBlueZ.Adapter? CurrentAdapter { get; }

        /// <summary>
        /// Gets the current BT token.
        /// </summary>
        BTToken? CurrentToken { get; }

        /// <summary>
        /// Initializes the Bluetooth device.
        /// </summary>
        Task InitializeDeviceAsync();

        /// <summary>
        /// Asynchronously establishes a connection to the Bluetooth device.
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// Synchronously establishes a connection to the Bluetooth device.
        /// </summary>
        bool Connect();

        /// <summary>
        /// Asynchronously disconnects from the Bluetooth device.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Synchronously disconnects from the Bluetooth device.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Asynchronously checks the current connection status.
        /// </summary>
        Task<bool> IsConnectedAsync();

        /// <summary>
        /// Synchronously checks the current connection status.
        /// </summary>
        bool IsConnected();
        #endregion

        #region Communication Management
        /// <summary>
        /// Gets whether communication is currently in progress.
        /// </summary>
        bool CommunicationInProgress { get; }

        /// <summary>
        /// Gets the current command characteristic.
        /// </summary>
        HashtagChris.DotNetBlueZ.GattCharacteristic? CommandCharacteristic { get; }

        /// <summary>
        /// Gets the current response characteristic.
        /// </summary>
        HashtagChris.DotNetBlueZ.GattCharacteristic? ResponseCharacteristic { get; }

        /// <summary>
        /// Sets up BLE notifications for the response characteristic.
        /// </summary>
        Task SetNotificationsAsync(string characteristicUuid);

        /// <summary>
        /// Sets up BLE notifications for the response characteristic.
        /// </summary>
        void SetNotifications(string characteristicUuid);

        /// <summary>
        /// Sets the command characteristic for the device.
        /// </summary>
        Task SetCommandCharacteristicAsync(string characteristicUuid);

        /// <summary>
        /// Sets the command characteristic for the device.
        /// </summary>
        void SetCommandCharacteristic(string characteristicUuid);

        /// <summary>
        /// Writes data to the device without expecting a response.
        /// </summary>
        Task WriteWithoutResponseAsync(byte[] data, bool waitForNotificationDataReceived = false);

        /// <summary>
        /// Writes data to the device without expecting a response.
        /// </summary>
        void WriteWithoutResponse(byte[] data, bool waitForNotificationDataReceived = false);

        /// <summary>
        /// Stops any ongoing communication.
        /// </summary>
        void StopCommunication();

        /// <summary>
        /// Asynchronously starts communication.
        /// </summary>
        Task StartCommunicationAsync();

        /// <summary>
        /// Synchronously starts communication.
        /// </summary>
        void StartCommunication();
        #endregion
    }
}