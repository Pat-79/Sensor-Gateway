using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SensorGateway.Bluetooth;
using SensorGateway.Sensors;

namespace SensorGateway.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IBTDevice for testing purposes.
    /// This mock bypasses all Bluetooth/D-Bus dependencies and provides
    /// controllable behavior for unit testing BT510 sensors and other components.
    /// </summary>
    public class MockBTDevice : IBTDevice
    {
        #region Events
        public event BTDevice.NotificationDataReceivedHandler? NotificationDataReceived;
        #endregion

        #region Properties
        public DeviceType DeviceType { get; set; } = DeviceType.BT510;
        public string Name { get; set; } = "MockBT510";
        public BTAddress? Address { get; set; } = new BTAddress("00:11:22:33:44:55");
        public SensorType Type { get; set; } = SensorType.BT510;
        public string Id { get; set; } = "mock-bt510-id";
        public ushort CompanyId { get; set; } = 0x0077; // Laird Connectivity company ID
        public Dictionary<ushort, byte[]> AdvertisementData { get; set; } = new();
        public short RSSI { get; set; } = -50;
        public long BufferSize => _mockBuffer.Count;
        public HashtagChris.DotNetBlueZ.IGattService1? CurrentService => null;
        public HashtagChris.DotNetBlueZ.Device? CurrentDevice => null;
        public HashtagChris.DotNetBlueZ.Adapter? CurrentAdapter => null;
        public BTToken? CurrentToken => null;
        public bool CommunicationInProgress { get; private set; } = false;
        public HashtagChris.DotNetBlueZ.GattCharacteristic? CommandCharacteristic => null;
        public HashtagChris.DotNetBlueZ.GattCharacteristic? ResponseCharacteristic => null;
        #endregion

        #region Private Fields
        private readonly List<byte> _mockBuffer = new();
        private bool _isConnected = false;
        private bool _disposed = false;
        #endregion

        #region Mock Configuration Properties
        /// <summary>
        /// Controls whether connection attempts should succeed or fail
        /// </summary>
        public bool ShouldConnectSucceed { get; set; } = true;

        /// <summary>
        /// Tracks whether ConnectAsync was called
        /// </summary>
        public bool ConnectAsyncCalled { get; private set; } = false;

        /// <summary>
        /// Tracks whether DisconnectAsync was called
        /// </summary>
        public bool DisconnectAsyncCalled { get; private set; } = false;

        /// <summary>
        /// Tracks whether Dispose was called
        /// </summary>
        public bool DisposeWasCalled { get; private set; } = false;

        /// <summary>
        /// Controls whether the device should report as connected
        /// </summary>
        public bool MockConnectedState { get; set; } = true;

        /// <summary>
        /// Mock data that will be returned when buffer is requested
        /// </summary>
        public byte[] MockBufferData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Controls whether service operations should succeed
        /// </summary>
        public bool ShouldServiceOperationsSucceed { get; set; } = true;

        /// <summary>
        /// Tracks whether SetServiceAsync was called
        /// </summary>
        public bool SetServiceAsyncCalled { get; private set; } = false;

        /// <summary>
        /// Tracks whether SetNotificationsAsync was called
        /// </summary>
        public bool SetNotificationsAsyncCalled { get; private set; } = false;

        /// <summary>
        /// Tracks whether SetCommandCharacteristicAsync was called
        /// </summary>
        public bool SetCommandCharacteristicAsyncCalled { get; private set; } = false;

        /// <summary>
        /// Tracks whether WriteWithoutResponseAsync was called
        /// </summary>
        public bool WriteWithoutResponseAsyncCalled { get; private set; } = false;

        /// <summary>
        /// Tracks whether ClearBufferAsync was called
        /// </summary>
        public bool ClearBufferAsyncCalled { get; private set; } = false;

        /// <summary>
        /// Tracks whether GetBufferDataAsync was called
        /// </summary>
        public bool GetBufferDataAsyncCalled { get; private set; } = false;
        #endregion

        #region Reset Methods
        /// <summary>
        /// Resets all call tracking properties
        /// </summary>
        public void ResetCallTracking()
        {
            ConnectAsyncCalled = false;
            DisconnectAsyncCalled = false;
            SetServiceAsyncCalled = false;
            SetNotificationsAsyncCalled = false;
            SetCommandCharacteristicAsyncCalled = false;
            WriteWithoutResponseAsyncCalled = false;
            ClearBufferAsyncCalled = false;
            GetBufferDataAsyncCalled = false;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a mock BT510 device with default settings
        /// </summary>
        public MockBTDevice()
        {
            // Set up default BT510 advertisement data
            AdvertisementData[CompanyId] = new byte[] { 0x01, 0x02, 0x03, 0x04 }; // Mock manufacturer data
        }

        /// <summary>
        /// Creates a mock device with specific configuration
        /// </summary>
        public MockBTDevice(DeviceType deviceType, string name, string address, SensorType sensorType)
        {
            DeviceType = deviceType;
            Name = name;
            Address = new BTAddress(address);
            Type = sensorType;
            Id = $"mock-{deviceType.ToString().ToLower()}-{Guid.NewGuid():N}";
        }
        #endregion

        #region Buffer Management
        public Task<byte[]> GetBufferDataAsync()
        {
            GetBufferDataAsyncCalled = true;
            return Task.FromResult(MockBufferData.Length > 0 ? MockBufferData : _mockBuffer.ToArray());
        }

        public byte[] GetBufferData()
        {
            return MockBufferData.Length > 0 ? MockBufferData : _mockBuffer.ToArray();
        }

        public Task<long> GetBufferSizeAsync()
        {
            return Task.FromResult((long)BufferSize);
        }

        public Task ClearBufferAsync()
        {
            ClearBufferAsyncCalled = true;
            _mockBuffer.Clear();
            return Task.CompletedTask;
        }

        public void ClearBuffer()
        {
            _mockBuffer.Clear();
        }

        public Task<BTMemoryPool.PooledMemoryHandle> GetBufferDataPooledAsync()
        {
            throw new NotSupportedException("Memory pooling not needed for unit tests");
        }

        public BTMemoryPool.PooledMemoryHandle GetBufferDataPooled()
        {
            throw new NotSupportedException("Memory pooling not needed for unit tests");
        }

        /// <summary>
        /// Adds data to the mock buffer (for testing buffer operations)
        /// </summary>
        public void AddToBuffer(byte[] data)
        {
            _mockBuffer.AddRange(data);
        }

        /// <summary>
        /// Adds individual bytes to the mock buffer (for testing buffer operations)
        /// </summary>
        public void AddBytesToBuffer(params byte[] data)
        {
            _mockBuffer.AddRange(data);
        }
        #endregion

        #region Service Management
        public void SetService(string serviceUuid)
        {
            if (!ShouldServiceOperationsSucceed)
                throw new InvalidOperationException("Mock configured to fail service operations");
        }

        public Task SetServiceAsync(string serviceUuid)
        {
            SetServiceAsyncCalled = true;
            if (!ShouldServiceOperationsSucceed)
                return Task.FromException(new InvalidOperationException("Mock configured to fail service operations"));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetServicesAsync()
        {
            var services = ShouldServiceOperationsSucceed 
                ? new List<string> { "6e400001-b5a3-f393-e0a9-e50e24dcca9e" } // Mock BT510 service UUID
                : new List<string>();
            return Task.FromResult<IReadOnlyList<string>>(services);
        }

        public IReadOnlyList<string> GetServices()
        {
            return ShouldServiceOperationsSucceed 
                ? new List<string> { "6e400001-b5a3-f393-e0a9-e50e24dcca9e" }
                : new List<string>();
        }

        public Task<bool> HasServiceAsync(string serviceUuid)
        {
            return Task.FromResult(ShouldServiceOperationsSucceed && 
                serviceUuid == "6e400001-b5a3-f393-e0a9-e50e24dcca9e");
        }

        public bool HasService(string serviceUuid)
        {
            return ShouldServiceOperationsSucceed && 
                serviceUuid == "6e400001-b5a3-f393-e0a9-e50e24dcca9e";
        }

        public Task<HashtagChris.DotNetBlueZ.IGattService1?> GetServiceAsync(string serviceUuid)
        {
            return Task.FromResult<HashtagChris.DotNetBlueZ.IGattService1?>(null);
        }

        public HashtagChris.DotNetBlueZ.IGattService1? GetService(string serviceUuid)
        {
            return null;
        }

        public Task<IReadOnlyList<string>> GetCharacteristicsAsync(string service)
        {
            var characteristics = ShouldServiceOperationsSucceed 
                ? new List<string> { "6e400002-b5a3-f393-e0a9-e50e24dcca9e", "6e400003-b5a3-f393-e0a9-e50e24dcca9e" }
                : new List<string>();
            return Task.FromResult<IReadOnlyList<string>>(characteristics);
        }

        public IReadOnlyList<string> GetCharacteristics(string service)
        {
            return ShouldServiceOperationsSucceed 
                ? new List<string> { "6e400002-b5a3-f393-e0a9-e50e24dcca9e", "6e400003-b5a3-f393-e0a9-e50e24dcca9e" }
                : new List<string>();
        }

        public Task<HashtagChris.DotNetBlueZ.GattCharacteristic?> GetCharacteristicAsync(HashtagChris.DotNetBlueZ.IGattService1 service, string characteristicUuid)
        {
            return Task.FromResult<HashtagChris.DotNetBlueZ.GattCharacteristic?>(null);
        }

        public HashtagChris.DotNetBlueZ.GattCharacteristic? GetCharacteristic(HashtagChris.DotNetBlueZ.IGattService1 service, string characteristicUuid)
        {
            return null;
        }

        public Task<bool> HasCharacteristicAsync(HashtagChris.DotNetBlueZ.IGattService1 service, string characteristicUuid)
        {
            return Task.FromResult(ShouldServiceOperationsSucceed);
        }

        public bool HasCharacteristic(HashtagChris.DotNetBlueZ.IGattService1 service, string characteristicUuid)
        {
            return ShouldServiceOperationsSucceed;
        }
        #endregion

        #region Connection Management
        public Task InitializeDeviceAsync()
        {
            return Task.CompletedTask;
        }

        public Task<bool> ConnectAsync()
        {
            ConnectAsyncCalled = true;
            if (ShouldConnectSucceed)
            {
                _isConnected = true;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public bool Connect()
        {
            if (ShouldConnectSucceed)
            {
                _isConnected = true;
                return true;
            }
            return false;
        }

        public Task DisconnectAsync()
        {
            DisconnectAsyncCalled = true;
            _isConnected = false;
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            _isConnected = false;
        }

        public Task<bool> IsConnectedAsync()
        {
            return Task.FromResult(_isConnected && MockConnectedState);
        }

        public bool IsConnected()
        {
            return _isConnected && MockConnectedState;
        }
        #endregion

        #region Communication Management
        public Task SetNotificationsAsync(string characteristicUuid)
        {
            SetNotificationsAsyncCalled = true;
            return Task.CompletedTask;
        }

        public void SetNotifications(string characteristicUuid)
        {
            // No-op for mock
        }

        public Task SetCommandCharacteristicAsync(string characteristicUuid)
        {
            SetCommandCharacteristicAsyncCalled = true;
            return Task.CompletedTask;
        }

        public void SetCommandCharacteristic(string characteristicUuid)
        {
            // No-op for mock
        }

        public Task WriteWithoutResponseAsync(byte[] data, bool waitForNotificationDataReceived = false)
        {
            WriteWithoutResponseAsyncCalled = true;
            // For testing, we can simulate response data being received
            if (waitForNotificationDataReceived)
            {
                // Simulate async notification after a short delay
                _ = Task.Run(async () =>
                {
                    await Task.Delay(10); // Small delay to simulate real BLE behavior
                    TriggerNotificationReceived(data);
                });
            }
            return Task.CompletedTask;
        }

        public void WriteWithoutResponse(byte[] data, bool waitForNotificationDataReceived = false)
        {
            if (waitForNotificationDataReceived)
            {
                TriggerNotificationReceived(data);
            }
        }

        public void StopCommunication()
        {
            CommunicationInProgress = false;
        }

        public Task StartCommunicationAsync()
        {
            CommunicationInProgress = true;
            return Task.CompletedTask;
        }

        public void StartCommunication()
        {
            CommunicationInProgress = true;
        }
        #endregion

        #region Mock Helper Methods
        /// <summary>
        /// Triggers the NotificationDataReceived event for testing
        /// </summary>
        public void TriggerNotificationReceived(byte[] data, string uuid = "6e400003-b5a3-f393-e0a9-e50e24dcca9e")
        {
            NotificationDataReceived?.Invoke(this, data, uuid);
        }

        /// <summary>
        /// Simulates receiving BT510 log data
        /// </summary>
        public void SimulateBT510LogData(byte[] logData)
        {
            AddToBuffer(logData);
            TriggerNotificationReceived(logData);
        }

        /// <summary>
        /// Creates sample BT510 temperature log entry for testing
        /// </summary>
        public static byte[] CreateMockTemperatureLogEntry(uint timestamp = 1000, short temperature = 2500)
        {
            var entry = new byte[8];
            
            // Timestamp (4 bytes, little endian)
            entry[0] = (byte)(timestamp & 0xFF);
            entry[1] = (byte)((timestamp >> 8) & 0xFF);
            entry[2] = (byte)((timestamp >> 16) & 0xFF);
            entry[3] = (byte)((timestamp >> 24) & 0xFF);
            
            // Temperature data (2 bytes, little endian) - 25.00°C = 2500
            entry[4] = (byte)(temperature & 0xFF);
            entry[5] = (byte)((temperature >> 8) & 0xFF);
            
            // Event type (1 byte) - Temperature event
            entry[6] = 1;
            
            // Salt/padding (1 byte)
            entry[7] = 0x00;
            
            return entry;
        }

        /// <summary>
        /// Creates sample BT510 battery log entry for testing
        /// </summary>
        public static byte[] CreateMockBatteryLogEntry(uint timestamp = 1000, ushort voltage = 3300)
        {
            var entry = new byte[8];
            
            // Timestamp (4 bytes, little endian)
            entry[0] = (byte)(timestamp & 0xFF);
            entry[1] = (byte)((timestamp >> 8) & 0xFF);
            entry[2] = (byte)((timestamp >> 16) & 0xFF);
            entry[3] = (byte)((timestamp >> 24) & 0xFF);
            
            // Battery voltage (2 bytes, little endian) - 3.3V = 3300mV
            entry[4] = (byte)(voltage & 0xFF);
            entry[5] = (byte)((voltage >> 8) & 0xFF);
            
            // Event type (1 byte) - Battery event
            entry[6] = 12;
            
            // Salt/padding (1 byte)
            entry[7] = 0x00;
            
            return entry;
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            DisposeWasCalled = true;
            if (!_disposed)
            {
                _mockBuffer.Clear();
                _isConnected = false;
                CommunicationInProgress = false;
                _disposed = true;
            }
        }
        #endregion
    }
}
