using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SensorGateway.Sensors;

namespace SensorGateway.Bluetooth
{

    #region DeviceType Enumeration    
    /// <summary>
    /// Enumeration of supported device types for sensor communication.
    /// Used to determine the appropriate communication protocols and data parsing methods.
    /// </summary>
    public enum DeviceType
    {
        /// <summary>
        /// Laird Connectivity BT510 temperature sensor device.
        /// Supports JSON-RPC communication protocol for configuration and data retrieval.
        /// </summary>
        BT510,

        /// <summary>
        /// Dummy sensor device for testing and development purposes.
        /// Provides simulated data without requiring actual hardware.
        /// </summary>
        Dummy
    }
    #endregion

    #region BTAddress Class
    /// <summary>
    /// Represents a Bluetooth Low Energy (BLE) device abstraction for sensor communication.
    /// This class provides a unified interface for working with different types of BLE sensor devices,
    /// particularly BT510 temperature sensors from Laird Connectivity.
    /// Implements IDisposable to ensure proper cleanup of resources.
    /// </summary>
    public partial class BTDevice : IBTDevice
    {
        #region Events
        public delegate void NotificationDataReceivedHandler(object sender, byte[]? data, string uuid);


        /// <summary>
        /// Event triggered when new notification data is received from the device.
        /// This event allows subscribers to handle incoming data asynchronously.
        /// </summary>
        public event NotificationDataReceivedHandler? NotificationDataReceived;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the type of device (BT510, Dummy, etc.).
        /// Used to determine the appropriate communication protocol and data parsing methods.
        /// </summary>
        public DeviceType DeviceType { get; set; } = DeviceType.Dummy;

        /// <summary>
        /// Gets or sets the human-readable name of the device as advertised via BLE.
        /// Examples: "DTT-34179", "BT510-12345", "Unknown Device"
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Bluetooth MAC address of the device.
        /// Used for device identification and connection establishment.
        /// </summary>
        public BTAddress? Address { get; set; }

        /// <summary>
        /// Gets or sets the sensor type, which determines data interpretation methods.
        /// Currently supports BT510 temperature sensors and dummy sensors for testing.
        /// </summary>
        public SensorType Type { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for this device instance.
        /// Typically derived from the first service UUID or generated as a GUID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the Bluetooth company identifier from manufacturer data.
        /// Examples: 0x0077 (Laird Connectivity), 0x004C (Apple)
        /// Used for device identification and protocol selection.
        /// </summary>
        public ushort CompanyId { get; set; } = 0;

        /// <summary>
        /// Gets or sets the raw manufacturer-specific advertisement data.
        /// Key: Company ID, Value: Raw advertisement payload bytes
        /// Contains sensor readings, device status, and configuration information.
        /// </summary>
        public Dictionary<ushort, byte[]> AdvertisementData { get; set; } = new Dictionary<ushort, byte[]>();

        /// <summary>
        /// Gets or sets the Received Signal Strength Indicator (RSSI) in dBm.
        /// Indicates the signal strength of the device's BLE advertisements.
        /// Typical range: -100 dBm (very weak) to -30 dBm (very strong).
        /// </summary>
        public short RSSI { get; set; } = 8;

        /// <summary>
        /// Gets the currently selected service for internal communication usage.
        /// </summary>
        internal HashtagChris.DotNetBlueZ.IGattService1? _service => _services.CurrentService;

        /// <summary>
        /// Gets the current device for internal communication usage.
        /// </summary>
        internal HashtagChris.DotNetBlueZ.Device? _device => _connection.CurrentDevice;
        #endregion

        #region Private Fields
        private bool _disposed = false;
        private readonly BTDeviceBuffer _buffer;
        private readonly BTDeviceServices _services;
        private readonly BTDeviceConnection _connection;
        private readonly BTDeviceCommunication _communication;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the BTDevice class with specified parameters.
        /// This constructor is used when creating device instances with known properties.
        /// </summary>
        /// <param name="deviceType">The type of device (BT510, Dummy, etc.)</param>
        /// <param name="name">Human-readable device name</param>
        /// <param name="address">Bluetooth MAC address of the device</param>
        /// <param name="type">Sensor type for data interpretation</param>
        /// <param name="id">Unique identifier for the device instance</param>
        public BTDevice(DeviceType deviceType, string name, BTAddress? address, SensorType type, string id)
        {
            DeviceType = deviceType;
            Name = name ?? "Unknown Device";
            Address = address;
            Type = type;
            Id = id ?? Guid.NewGuid().ToString();

            // Initialize SOLID components
            _buffer = new BTDeviceBuffer();
            _connection = new BTDeviceConnection(
                () => Address,
                () => _buffer.ClearBufferAsync()
            );

            // Initialize communication first to avoid circular dependency
            _communication = new BTDeviceCommunication(
                () => _services?.CurrentService,
                (service, uuid) => _services?.GetCharacteristicAsync(service, uuid) ?? Task.FromResult<HashtagChris.DotNetBlueZ.GattCharacteristic?>(null),
                () => _buffer.ClearBufferAsync(),
                (data) => _buffer.AppendToBufferAsync(data),
                () => _connection.IsConnectedAsync(),
                () => _connection.ConnectAsync(),
                (uuid, data) => OnNotificationDataReceived(uuid, data)
            );

            _services = new BTDeviceServices(
                () => _connection.IsConnectedAsync(),
                () => _connection.ConnectAsync(),
                () => _communication.CommunicationInProgress,
                () => Task.FromResult(_connection.CurrentDevice)
            );

            // Forward communication events
            _communication.NotificationDataReceived += (sender, data, uuid) => NotificationDataReceived?.Invoke(this, data, uuid);

            // Initialize device (sync call in constructor)
            _ = Task.Run(async () => await InitializeDeviceAsync());
        }
        #endregion

        #region Delegated Methods - Buffer Management

        /// <summary>
        /// Gets the current size of the data buffer in bytes.
        /// </summary>
        public long BufferSize => _buffer.BufferSize;

        /// <summary>
        /// Asynchronously retrieves the current buffer contents as a byte array.
        /// </summary>
        public async Task<byte[]> GetBufferDataAsync() => await _buffer.GetBufferDataAsync();

        /// <summary>
        /// Synchronously retrieves the current buffer contents as a byte array.
        /// </summary>
        public byte[] GetBufferData() => _buffer.GetBufferData();

        /// <summary>
        /// Asynchronously gets the current size of the data buffer in bytes.
        /// </summary>
        public async Task<long> GetBufferSizeAsync() => await _buffer.GetBufferSizeAsync();

        /// <summary>
        /// Asynchronously clears all data from the internal buffer and resets its position.
        /// </summary>
        public async Task ClearBufferAsync() => await _buffer.ClearBufferAsync();

        /// <summary>
        /// Synchronously clears all data from the internal buffer.
        /// </summary>
        public void ClearBuffer() => _buffer.ClearBuffer();

        /// <summary>
        /// Efficiently appends byte data to the internal buffer in a thread-safe manner.
        /// </summary>
        /// <param name="data">The byte array to append to the buffer. Null or empty arrays are ignored.</param>
        /// <returns>A task that represents the asynchronous append operation.</returns>
        internal async Task AppendToBufferAsync(byte[] data) => await _buffer.AppendToBufferAsync(data);

        /// <summary>
        /// Asynchronously retrieves buffer contents using memory pooling for high-performance scenarios.
        /// This method is optimized for frequent buffer access during data processing bursts.
        /// </summary>
        /// <returns>A pooled memory handle that must be disposed after use</returns>
        public async Task<BTMemoryPool.PooledMemoryHandle> GetBufferDataPooledAsync() => await _buffer.GetBufferDataPooledAsync();

        /// <summary>
        /// Synchronously retrieves buffer contents using memory pooling.
        /// Use this when already on a background thread to avoid async overhead.
        /// </summary>
        /// <returns>A pooled memory handle that must be disposed after use</returns>
        public BTMemoryPool.PooledMemoryHandle GetBufferDataPooled() => _buffer.GetBufferDataPooled();

        /// <summary>
        /// Gets current memory pool usage statistics for performance monitoring.
        /// </summary>
        /// <returns>Memory pool statistics including rentals, returns, and size distribution</returns>
        public static BTMemoryPool.PoolStatistics GetMemoryPoolStatistics() => BTMemoryPool.GetStatistics();

        #endregion

        #region Delegated Methods - Service Management

        /// <summary>
        /// Gets the currently selected service.
        /// </summary>
        public HashtagChris.DotNetBlueZ.IGattService1? CurrentService => _services.CurrentService;

        /// <summary>
        /// Sets the service for the device, allowing it to communicate with the specified service.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to set</param>
        public void SetService(string serviceUuid) => _services.SetService(serviceUuid);

        /// <summary>
        /// Asynchronously sets the service for the device, allowing it to communicate with the specified service.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to set</param>
        public async Task SetServiceAsync(string serviceUuid) => await _services.SetServiceAsync(serviceUuid);

        /// <summary>
        /// Asynchronously retrieves a list of all services available on the connected Bluetooth device.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is a read-only list of
        /// service UUIDs available on the device.
        /// </returns>
        public async Task<IReadOnlyList<string>> GetServicesAsync() => await _services.GetServicesAsync();

        /// <summary>
        /// Synchronously retrieves a list of all services available on the connected Bluetooth device.
        /// </summary>
        /// <returns>
        /// A read-only list of service UUIDs available on the device.
        /// </returns>
        public IReadOnlyList<string> GetServices() => _services.GetServices();

        /// <summary>
        /// Asynchronously checks if the device has a specific service identified by the provided UUID.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to check.</param>
        public async Task<bool> HasServiceAsync(string serviceUuid) => await _services.HasServiceAsync(serviceUuid);

        /// <summary>
        /// Synchronously checks if the device has a specific service identified by the provided UUID.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to check.</param>
        /// <returns>True if the service exists, false otherwise.</returns>
        public bool HasService(string serviceUuid) => _services.HasService(serviceUuid);

        /// <summary>
        /// Asynchronously retrieves a specific service by its UUID from the connected device.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to retrieve.</param>
        /// <returns>
        /// An instance of <see cref="HashtagChris.DotNetBlueZ.IGattService1"/> representing the service, or null if the service is not found.
        /// </returns>
        public async Task<HashtagChris.DotNetBlueZ.IGattService1?> GetServiceAsync(string serviceUuid) => await _services.GetServiceAsync(serviceUuid);

        /// <summary>
        /// Synchronously retrieves a specific service by its UUID from the connected device.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to retrieve.</param>
        /// <returns>
        /// An instance of <see cref="HashtagChris.DotNetBlueZ.IGattService1"/> representing the service, or null if the service is not found.
        /// </returns>
        public HashtagChris.DotNetBlueZ.IGattService1? GetService(string serviceUuid) => _services.GetService(serviceUuid);

        /// <summary>
        /// Asynchronously retrieves a list of all characteristics within a service identified by the provided UUID.
        /// </summary>
        /// <param name="service">The service from which to retrieve characteristics</param>
        /// <returns>
        /// A list of GUIDs representing the characteristics within the specified service
        /// </returns>
        public async Task<IReadOnlyList<string>> GetCharacteristicsAsync(string service) => await _services.GetCharacteristicsAsync(service);

        /// <summary>
        /// Synchronously retrieves a list of all characteristics within a service identified by the provided UUID.
        /// </summary>
        /// <param name="service">The service from which to retrieve characteristics</param>
        /// <returns>
        /// A list of GUIDs representing the characteristics within the specified service
        /// </returns>
        public IReadOnlyList<string> GetCharacteristics(string service) => _services.GetCharacteristics(service);

        /// <summary>
        /// Asynchronously retrieves a specific characteristic by its UUID from a service on the connected device.
        /// </summary>
        /// <param name="service">The service from which to retrieve the characteristic</param>
        /// <param name="characteristicUuid">The UUID of the characteristic to retrieve</param>
        public async Task<HashtagChris.DotNetBlueZ.GattCharacteristic?> GetCharacteristicAsync(HashtagChris.DotNetBlueZ.IGattService1 service, string characteristicUuid) => await _services.GetCharacteristicAsync(service, characteristicUuid);

        /// <summary>
        /// Synchronously retrieves a specific characteristic by its UUID from a service on the connected device.
        /// </summary>
        /// <param name="service">The service from which to retrieve the characteristic</param>
        /// <param name="characteristicUuid">The UUID of the characteristic to retrieve</param>
        public HashtagChris.DotNetBlueZ.GattCharacteristic? GetCharacteristic(HashtagChris.DotNetBlueZ.IGattService1 service, string characteristicUuid) => _services.GetCharacteristic(service, characteristicUuid);

        /// <summary>
        /// Checks if the device has a specific characteristic within a service identified by the provided UUIDs.
        /// </summary>
        /// <param name="service">The service to check for the characteristic</param>
        /// <param name="characteristicUuid">The UUID of the characteristic to check</param>
        /// <returns>True if the characteristic exists within the service, otherwise false</returns>
        public async Task<bool> HasCharacteristicAsync(HashtagChris.DotNetBlueZ.IGattService1 service, string characteristicUuid) => await _services.HasCharacteristicAsync(service, characteristicUuid);

        /// <summary>
        /// Synchronously checks if the device has a specific characteristic within a service identified by the provided
        /// UUIDs.
        /// </summary>
        /// <param name="service">The service to check for the characteristic</param>
        /// <param name="characteristicUuid">The UUID of the characteristic to check</param>
        /// <returns>
        /// True if the characteristic exists within the service, otherwise false
        /// </returns>
        public bool HasCharacteristic(HashtagChris.DotNetBlueZ.IGattService1 service, string characteristicUuid) => _services.HasCharacteristic(service, characteristicUuid);

        #endregion

        #region Delegated Methods - Connection Management

        /// <summary>
        /// Gets the current Bluetooth device.
        /// </summary>
        public HashtagChris.DotNetBlueZ.Device? CurrentDevice => _connection.CurrentDevice;

        /// <summary>
        /// Gets the current Bluetooth adapter.
        /// </summary>
        public HashtagChris.DotNetBlueZ.Adapter? CurrentAdapter => _connection.CurrentAdapter;

        /// <summary>
        /// Gets the current BT token.
        /// </summary>
        public BTToken? CurrentToken => _connection.CurrentToken;

        /// <summary>
        /// Initializes the Bluetooth device by setting up the adapter and preparing the device for communication.
        /// </summary>
        /// <returns>A task that represents the asynchronous device initialization operation.</returns>
        public async Task InitializeDeviceAsync() => await _connection.InitializeDeviceAsync();

        /// <summary>
        /// Asynchronously establishes a connection to the Bluetooth device with retry logic.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous connection operation. The task result is
        /// true if the connection was successful, false otherwise.
        /// </returns>
        public async Task<bool> ConnectAsync() => await _connection.ConnectAsync();

        /// <summary>
        /// Synchronously establishes a connection to the Bluetooth device.
        /// </summary>
        /// <returns>True if the connection was successful, false otherwise.</returns>
        public bool Connect() => _connection.Connect();

        /// <summary>
        /// Asynchronously disconnects from the Bluetooth device and performs cleanup operations.
        /// </summary>
        /// <returns>A task that represents the asynchronous disconnection operation.</returns>
        public async Task DisconnectAsync() => await _connection.DisconnectAsync();

        /// <summary>
        /// Synchronously disconnects from the Bluetooth device.
        /// </summary>
        public void Disconnect() => _connection.Disconnect();

        /// <summary>
        /// Asynchronously checks the current connection status of the Bluetooth device.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous connection status check. The task result is
        /// true if the device is connected, false otherwise.
        /// </returns>
        public async Task<bool> IsConnectedAsync() => await _connection.IsConnectedAsync();

        /// <summary>
        /// Synchronously checks the current connection status of the Bluetooth device.
        /// </summary>
        /// <returns>True if the device is connected, false otherwise.</returns>
        public bool IsConnected() => _connection.IsConnected();

        #endregion

        #region Delegated Methods - Communication Management

        /// <summary>
        /// Gets whether communication is currently in progress.
        /// </summary>
        public bool CommunicationInProgress => _communication.CommunicationInProgress;

        /// <summary>
        /// Gets the current command characteristic.
        /// </summary>
        public HashtagChris.DotNetBlueZ.GattCharacteristic? CommandCharacteristic => _communication.CommandCharacteristic;

        /// <summary>
        /// Gets the current response characteristic.
        /// </summary>
        public HashtagChris.DotNetBlueZ.GattCharacteristic? ResponseCharacteristic => _communication.ResponseCharacteristic;

        /// <summary>
        /// Sets up BLE notifications for the response characteristic and handles initial data retrieval.
        /// </summary>
        /// <param name="characteristicUuid">The UUID of the response characteristic to set up notifications for</param>
        public async Task SetNotificationsAsync(string characteristicUuid) => await _communication.SetNotificationsAsync(characteristicUuid);

        /// <summary>
        /// Sets up BLE notifications for the response characteristic and handles initial data retrieval.
        /// </summary>
        /// <param name="characteristicUuid">The UUID of the response characteristic to set up notifications for</param>
        public void SetNotifications(string characteristicUuid) => _communication.SetNotifications(characteristicUuid);

        /// <summary>
        /// Sets the command characteristic for the device, allowing it to send commands to the device.
        /// </summary>
        /// <param name="characteristicUuid">The UUID of the command characteristic to set</param>
        public async Task SetCommandCharacteristicAsync(string characteristicUuid) => await _communication.SetCommandCharacteristicAsync(characteristicUuid);

        /// <summary>
        /// Sets the command characteristic for the device, allowing it to send commands to the device.
        /// </summary>
        /// <param name="characteristicUuid">The UUID of the command characteristic to set</param>
        public void SetCommandCharacteristic(string characteristicUuid) => _communication.SetCommandCharacteristic(characteristicUuid);

        /// <summary>
        /// WriteWithoutResponse is used to write data to the device, without a response from the device.
        /// </summary>
        /// <param name="data">The data to write to the characteristic</param>
        /// <param name="waitForNotificationDataReceived">If true, waits for notification data to be received before returning</param>
        public async Task WriteWithoutResponseAsync(byte[] data, bool waitForNotificationDataReceived = false) => await _communication.WriteWithoutResponseAsync(data, waitForNotificationDataReceived);

        /// <summary>
        /// WriteWithoutResponse is used to write data to the device, without a response from the device.
        /// </summary>
        /// <param name="data">The data to write to the characteristic</param>
        /// <param name="waitForNotificationDataReceived">If true, waits for notification data to be received before returning</param>
        public void WriteWithoutResponse(byte[] data, bool waitForNotificationDataReceived = false) => _communication.WriteWithoutResponse(data, waitForNotificationDataReceived);

        /// <summary>
        /// Stops any ongoing communication by resetting the communication flag.
        /// </summary>
        public void StopCommunication() => _communication.StopCommunication();

        /// <summary>
        /// Asynchronously starts communication by clearing the buffer and setting the communication flag.
        /// </summary>
        public async Task StartCommunicationAsync() => await _communication.StartCommunicationAsync();

        /// <summary>
        /// Synchronously starts communication (compatibility method).
        /// </summary>
        public void StartCommunication() => _communication.StartCommunication();

        /// <summary>
        /// Handles notification data received from the device and raises the NotificationDataReceived event.
        /// </summary>
        /// <param name="characteristicUuid">The UUID of the characteristic that sent the notification</param>
        /// <param name="data">The raw notification data received from the device</param>
        protected virtual void OnNotificationDataReceived(string characteristicUuid, byte[] data)
        {
            NotificationDataReceived?.Invoke(this, data, characteristicUuid);
        }

        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Disposes of the BTDevice instance and cleans up associated resources.
        /// This method clears advertisement data, disposes of the BTAddress object,
        /// and resets all properties to default values to prevent memory leaks.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            // Clear advertisement data dictionary to free memory
            AdvertisementData?.Clear();
            if (Address != null)
            {
                Address = null;
            }

            // Reset string properties to empty to aid garbage collection
            Name = string.Empty;
            Id = string.Empty;

            // Reset to default sensor type
            Type = SensorType.Dummy;

            // Dispose SOLID components
            _buffer?.Dispose();
            _connection?.Dispose();
            _communication?.Dispose();

            _disposed = true;
        }
        #endregion
    }
    #endregion
}