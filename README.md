# Sensor Gateway

A Linux Bluetooth Low Energy (BLE) sensor gateway for acquiring sensor data from BLE-enabled sensors. The gateway supports multiple sensor types and implements efficient data collection strategies to minimize battery drain on sensor devices.

## Overview

The Sensor Gateway is designed to continuously scan for BLE sensors, collect measurement data, and forward it to configured endpoints. It supports both advertisement-based data collection (passive) and active connection-based log downloading, with intelligent scheduling to minimize battery impact on sensor devices.

## Key Features

- **Multi-Sensor Support**: Extensible architecture supporting various sensor types (BT510, dummy sensors for testing)
- **Dual Data Collection**: 
  - Advertisement data parsing (passive, battery-efficient)
  - Active log downloading via BLE connections when needed
- **Thread-Safe Operations**: All communication methods use SemaphoreSlim for thread safety
- **Dual API Design**: Every async method has a synchronous counterpart for compatibility
- **Robust Connection Management**: Token-based RAII system with automatic retry logic (3 attempts)
- **Intelligent Scheduling**: Devices are marked after processing to prevent unnecessary reconnections
- **Bluetooth Stack Protection**: Token-based RAII system to prevent overwhelming the Bluetooth stack
- **Background Scanning**: Continuous scanning thread with configurable intervals and device filters
- **Worker Process Architecture**: Each discovered device spawns an independent worker process
- **Comprehensive Error Handling**: Detailed validation and meaningful exception messages
- **Resource Management**: Proper cleanup and disposal patterns with automatic connection timeouts

## Architecture

### Core Components

#### Scanner ([`Scanner`](src/scanner.cs))
- Singleton device scanner that continuously scans for BLE devices
- Manages automatic and manual scanning operations
- Spawns worker processes for discovered devices
- Thread-safe device tracking to prevent duplicate processing

#### Bluetooth Manager ([`BTManager`](src/bt/btmanager.cs))
- Token-based resource management system
- Prevents Bluetooth stack overload through controlled concurrency
- RAII implementation ensures proper resource cleanup

#### Device Abstraction
- **Main Device Class** ([`BTDevice`](src/bt/btdevice.cs)): Core device properties, events, and main interface
- **Connection Management** ([`BTDevice.Connection`](src/bt/btdevice.connection.cs)): 
  - Adapter initialization with power state management
  - Device discovery and connection with automatic retry logic
  - Token acquisition and connection lifecycle management
- **Service Discovery** ([`BTDevice.Services`](src/bt/btdevice.services.cs)):
  - Service and characteristic enumeration with validation
  - UUID normalization and service selection
  - Comprehensive service availability checking
- **Communication Module** ([`BTDevice.Communication`](src/bt/btdevice.communication.cs)): 
  - Dual async/sync API design for all operations
  - Notification setup and event handling
  - Command characteristic configuration
  - Write operations with optional response waiting
- **Buffer Management** ([`BTDevice.Buffer`](src/bt/btdevice.buffer.cs)):
  - Thread-safe buffer operations with SemaphoreSlim protection
  - Efficient memory stream management
  - Asynchronous buffer access with proper disposal
- **Resource Cleanup** ([`BTDevice.Disposal`](src/bt/btdevice.disposal.cs)):
  - Comprehensive resource disposal patterns
  - Event handler cleanup and memory management
- **Configuration Constants** ([`BTDevice.Constants`](src/bt/btdevice.constants.cs)):
  - Centralized timeout and retry configuration
  - Well-defined connection parameters
  - Performance optimization settings
- Handles device property extraction and manufacturer data processing
- Supports both BT510 sensors and dummy devices for testing

#### Sensor Framework
- **Base Sensor Class** ([`Sensor`](src/sensor/sensor.cs)): Abstract base providing common sensor functionality
- **BT510 Sensor** ([`BT510Sensor`](src/sensor/sensor_bt510.cs)): JSON-RPC communication with BT510 devices
- **Dummy Sensor** ([`DummySensor`](src/sensor/sensor_dummy.cs)): Testing and development sensor implementation

## BTDevice Communication API

The BTDevice class provides comprehensive Bluetooth communication capabilities:

### Connection Management
```csharp
// Asynchronous connection with automatic retry (3 attempts)
Task<bool> ConnectAsync()
bool Connect()

// Graceful disconnection with resource cleanup
Task DisconnectAsync() 
void Disconnect()

// Connection status checking
Task<bool> IsConnectedAsync()
bool IsConnected()
```

### Service & Characteristic Discovery
```csharp
// Service operations
Task<IReadOnlyList<string>> GetServicesAsync()
Task<bool> HasServiceAsync(string serviceUuid)
Task<IGattService1?> GetServiceAsync(string serviceUuid)

// Characteristic operations  
Task<IReadOnlyList<string>> GetCharacteristicsAsync(string serviceUuid)
Task<bool> HasCharacteristicAsync(IGattService1 service, string characteristicUuid)
Task<GattCharacteristic?> GetCharacteristicAsync(IGattService1 service, string characteristicUuid)
```

### Communication Setup & Data Transfer
```csharp
// Setup notification handling and command channels
Task SetNotificationsAsync(string serviceUuid, string characteristicUuid)
Task SetCommandCharacteristicAsync(string characteristicUuid)

// Communication session management
Task StartCommunicationAsync()
void StopCommunication()

// Data transmission (fire-and-forget with optional session cleanup)
Task WriteWithoutResponseAsync(byte[] data, bool stopCommunication = true)
```

### Buffer Management
```csharp
// Thread-safe buffer operations
Task<byte[]> GetBufferDataAsync()
Task<long> GetBufferSizeAsync() 
Task ClearBufferAsync()

// Properties
bool CommunicationInProgress { get; }
long BufferSize { get; }

// Events
event EventHandler<byte[]> NotificationDataReceived;
```

## Object Diagram

```mermaid
classDiagram
    class Scanner {
        -_instance: Lazy~Scanner~
        -_discoveredDevices: ConcurrentDictionary
        -_devicesInProcess: ConcurrentDictionary
        -_scanSemaphore: SemaphoreSlim
        +ScanForDevicesAsync()
        +StartAutoScan()
        +StopAutoScan()
        -BackgroundScanningLoop()
        -ProcessDeviceAsync()
    }

    class BTManager {
        -_instance: Lazy~BTManager~
        -_availableTokens: ConcurrentBag~BTToken~
        -_tokenSemaphore: SemaphoreSlim
        +GetTokenAsync(): BTToken
        +ReturnToken(token: BTToken)
        -BackgroundCleanupLoop()
    }

    class BTToken {
        +Id: uint
        +IsValid: bool
        +IsReturned: bool
        +AcquiredAt: DateTime?
        +MarkAsAcquired()
        +MarkAsReturned()
        +Dispose()
    }

    class BTDevice {
        +DeviceType: DeviceType
        +Name: string
        +Address: BTAddress
        +Type: SensorType
        +CompanyId: ushort
        +AdvertisementData: Dictionary~ushort, byte[]~
        +RSSI: short
        +FromBlueZDeviceAsync(): BTDevice
        +DetermineDeviceType(): DeviceType
    }

    class BTDeviceConnection {
        -_device: Device
        -_adapter: Adapter
        -_token: BTToken
        +ConnectAsync(): bool
        +DisconnectAsync()
        +IsConnectedAsync(): bool
        +InitializeAdapterAsync()
        +InitializeDeviceAsync()
    }

    class BTDeviceServices {
        -_service: IGattService1
        +GetServicesAsync(): IReadOnlyList~string~
        +GetServiceAsync(): IGattService1
        +GetCharacteristicsAsync(): IReadOnlyList~string~
        +GetCharacteristicAsync(): GattCharacteristic
        +SetServiceAsync()
    }

    class BTDeviceCommunication {
        -_commandChar: GattCharacteristic
        -_responseChar: GattCharacteristic
        -_communicationInProgress: bool
        -_notificationReceived: ManualResetEventSlim
        +SetNotificationsAsync()
        +SetCommandCharacteristicAsync()
        +WriteWithoutResponseAsync()
        +StartCommunicationAsync()
        +StopCommunication()
    }

    class BTDeviceBuffer {
        -_dataBuffer: MemoryStream
        -_bufferSemaphore: SemaphoreSlim
        +GetBufferDataAsync(): byte[]
        +ClearBufferAsync()
        +AppendToBufferAsync()
        +BufferSize: long
    }

    class Sensor {
        <<abstract>>
        +SensorType: SensorType
        +Name: string
        +Address: BTAddress
        +Device: BTDevice
        +DownloadLogAsync(): IEnumerable~Measurement~
        +ProcessLogAsync(): IEnumerable~Measurement~
        +ParseAdvertisementAsync(): IEnumerable~Measurement~
        +GetMeasurementsAsync(): IEnumerable~Measurement~
    }

    class BT510Sensor {
        +InitializeAsync()
        +DownloadLogAsync(): IEnumerable~Measurement~
        +ProcessLogAsync(): IEnumerable~Measurement~
        +ParseAdvertisementAsync(): IEnumerable~Measurement~
        +GetConfigurationAsync(): Dictionary~string, object~
        +UpdateConfigurationAsync(): bool
    }

    class DummySensor {
        -_random: Random
        +DownloadLogAsync(): IEnumerable~Measurement~
        +ProcessLogAsync(): IEnumerable~Measurement~
        +ParseAdvertisementAsync(): IEnumerable~Measurement~
        +GetConfigurationAsync(): Dictionary~string, object~
        +UpdateConfigurationAsync(): bool
    }

    Scanner --> BTManager : uses tokens
    Scanner --> BTDevice : creates from BlueZ devices
    BTManager --> BTToken : manages pool
    BTDevice --> DeviceType : determines type
    BTDevice --> SensorType : maps to sensor
    BTDevice --> BTDeviceConnection : connection management
    BTDevice --> BTDeviceServices : service discovery
    BTDevice --> BTDeviceCommunication : data transfer
    BTDevice --> BTDeviceBuffer : buffer operations
    Sensor --> BTDevice : wraps device
    BT510Sensor --|> Sensor : implements
    DummySensor --|> Sensor : implements
    BT510Sensor --> BTManager : requires tokens
    Scanner --> BluetoothConfig : uses configuration
```

## Data Flow

1. **Scanner Thread**: Continuously scans for BLE devices matching configured name prefixes
2. **Device Discovery**: When a matching device is found, check if it needs processing
3. **Worker Spawning**: Spawn independent worker process for each device requiring processing
4. **Advertisement Processing**: Parse advertisement data for immediate sensor readings
5. **Log Processing Decision**: Determine if device log download is required
6. **Token Acquisition**: Acquire Bluetooth token for active connections
7. **Device Connection**: Connect to sensor using retry logic with automatic timeout management
8. **Service Discovery**: Discover and validate required services and characteristics
9. **Communication Setup**: Configure notifications and command channels
10. **Data Transfer**: Send commands and receive responses using thread-safe buffer operations
11. **Data Processing**: Convert raw data to structured measurements
12. **Resource Cleanup**: Automatic disconnection and resource disposal
13. **Data Forwarding**: Send processed data to configured endpoints
14. **Device Marking**: Mark device as processed to prevent immediate re-processing

## Token Usage Policy

The Bluetooth token system ensures controlled access to the Bluetooth stack:

- **Advertisement Processing**: No token required (local data parsing)
- **Log Download/Processing**: Token required (active BT communication)
- **Configuration Operations**: Token required (active BT communication)  
- **Service Discovery**: Token required (active BT communication)
- **Mixed Operations**: Token required if any component needs active communication
- **Connection Management**: Automatic token timeout after 120 seconds

## Configuration

### Timeout Configuration
The system uses the following built-in timeouts (configurable via constants):

```csharp
// Connection timeouts
ADAPTER_POWER_TIMEOUT_SECONDS = 5
MAX_CONNECTION_ATTEMPTS = 3
CONNECTION_STABILIZATION_DELAY = 2000ms
CONNECTION_RETRY_DELAY = 1000ms

// Communication timeouts  
TOKEN_TIMEOUT_SECONDS = 120
NOTIFICATION_WAIT_TIMEOUT_SECONDS = 30
```

### Bluetooth Adapter Requirements
- Linux with BlueZ stack (tested with BlueZ 5.50+)
- Bluetooth 4.0+ adapter with BLE support
- Root privileges may be required for some operations

Configuration is managed through the [`BluetoothConfig`](src/config/config_bluetooth.cs) class:

```csharp
public class BluetoothConfig
{
    public string AdapterName { get; set; } = "";
    public int DiscoveryTimeoutSeconds { get; set; } = 10;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public List<string> DeviceNameFilters { get; set; } = { "S12345-", "BT510-" };
    public List<string> ServiceUuidFilters { get; set; } = { "569a1101-b87f-490c-92cb-11ba5ea5167c" };
    public short MinRssiThreshold { get; set; } = -90;
}
```

## Supported Sensor Types

### BT510 Sensors
- **Manufacturer**: Laird Connectivity
- **Communication**: JSON-RPC over BLE
- **Features**: Temperature logging, configurable sampling rates
- **Company ID**: 0x0077
- **Services**: Custom Laird service (569a1101-b87f-490c-92cb-11ba5ea5167c)
- **Characteristics**: Command (569a2001-...) and Response (569a2000-...)

### Dummy Sensors
- **Purpose**: Testing and development
- **Features**: Simulated temperature data, no hardware required
- **Company ID**: 0x0000

## Building and Running

**Target Platform**: Linux (requires BlueZ Bluetooth stack)

```bash
# Build the project
dotnet build

# Run the application
dotnet run
```

## Dependencies

- .NET 8.0
- HashtagChris.DotNetBlueZ (Linux Bluetooth LE support)
- System.Threading.Tasks
- System.Collections.Concurrent

## Project Structure

```
src/
├── bt/                          # Bluetooth abstraction layer
│   ├── btdevice.cs             # Main device class and properties
│   ├── btdevice.constants.cs   # Configuration constants and timeouts
│   ├── btdevice.connection.cs  # Connection management and adapter initialization
│   ├── btdevice.services.cs    # Service and characteristic discovery
│   ├── btdevice.communication.cs # Data transfer and notification handling
│   ├── btdevice.buffer.cs      # Thread-safe buffer management
│   ├── btdevice.disposal.cs    # Resource cleanup and disposal
│   ├── btaddress.cs            # Device (MAC) address
│   └── btmanager.cs            # Resource management
├── sensor/                      # Sensor implementations
│   ├── sensor.cs               # Base sensor class
│   ├── sensor_bt510.cs         # BT510 implementation
│   └── sensor_dummy.cs         # Dummy implementation
├── config/                      # Configuration management
│   └── config_bluetooth.cs
├── scanner.cs                   # Device scanning logic
├── measurement.cs               # Data structures
└── Program.cs                   # Application entry point
```

## Code Organization

### Modular Partial Class Design
The BTDevice class uses a modular partial class structure for improved maintainability:

- **btdevice.cs**: Core class definition, properties, events, and main interface
- **btdevice.constants.cs**: All configuration constants and timeout values in one location
- **btdevice.connection.cs**: Connection lifecycle, adapter management, and device initialization  
- **btdevice.services.cs**: Service discovery, characteristic enumeration, and service selection
- **btdevice.communication.cs**: Data transfer, notifications, and command handling
- **btdevice.buffer.cs**: Thread-safe buffer operations and memory management
- **btdevice.disposal.cs**: Resource cleanup and disposal patterns

### Benefits of This Structure
- **Single Responsibility**: Each file focuses on one specific aspect of device functionality
- **Easy Navigation**: Developers can quickly locate connection vs communication vs buffer logic
- **Maintainability**: Changes to buffer logic don't affect connection or service code
- **Team Development**: Multiple developers can work on different aspects simultaneously
- **Testing**: Easier to create focused unit tests for specific functionality areas

## Future Enhancements

- MQTT data forwarding
- REST API endpoints
- Database persistence
- Web-based configuration interface
- Additional sensor type support
- Real-time monitoring dashboard

## Performance Optimizations

### ConfigureAwait Usage
The library implements `ConfigureAwait(false)` throughout all internal async operations:

```csharp
await _bufferSemaphore.WaitAsync().ConfigureAwait(false);
await _dataBuffer.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
await _adapter.SetPoweredAsync(true).ConfigureAwait(false);
```

**Benefits:**
- Prevents thread pool starvation in library consumers
- Eliminates potential deadlocks in mixed sync/async scenarios
- Improves overall throughput by allowing continuations on any thread pool thread
- Reduces context switching overhead

### Connection Management
- **Token-based RAII**: Prevents Bluetooth stack overload through controlled concurrency
- **Intelligent retry logic**: 3 attempts with exponential backoff (1s, 2s delays)
- **Automatic timeout handling**: All operations have configurable timeouts
- **Resource cleanup**: Guaranteed cleanup even in failure scenarios

### Performance & Resource Management
- **Optimized async patterns**: ConfigureAwait(false) used throughout for library performance
- **Configurable timeouts**: All operations have well-defined timeout constants
- **Enhanced retry logic**: Exponential backoff with 3 connection attempts
- **Memory efficient**: Proper disposal patterns prevent resource leaks
- **Thread-safe buffer operations**: SemaphoreSlim protection with timeout handling

## Usage Examples

### Enhanced Error Handling with Timeouts
```csharp
try
{
    // Connection with automatic retry and timeout
    bool connected = await device.ConnectAsync();
    
    // Setup with built-in timeout protection
    await device.SetNotificationsAsync(serviceUuid, responseCharUuid);
    await device.SetCommandCharacteristicAsync(commandCharUuid);
    
    // Write with notification timeout handling
    await device.WriteWithoutResponseAsync(commandData, waitForResponse: true);
}
catch (TimeoutException ex)
{
    Console.WriteLine($"Operation timed out: {ex.Message}");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("Communication is already in progress"))
{
    Console.WriteLine("Previous operation still in progress - wait before retrying");
}
finally
{
    await device.DisconnectAsync(); // Guaranteed cleanup
}
```

### Basic Sensor Data Processing
```csharp
// Assuming device is already connected
var measurements = await sensor.GetMeasurementsAsync();

// Process and display measurements
foreach (var measurement in measurements)
{
    Console.WriteLine($"Timestamp: {measurement.Timestamp}, Temperature: {measurement.Temperature}");
}
```

### Advertisement Data Parsing
```csharp
// Directly from scanner (no device connection required)
var parser = new AdvertisementParser();
var result = parser.Parse(advertisementData);

// Check for specific manufacturer data
if (result.ManufacturerData.ContainsKey(0x0077))
{
    Console.WriteLine("Laird BT510 advertisement detected");
}
```

### Log Download and Processing
```csharp
// Download and process sensor log
var logData = await sensor.DownloadLogAsync();
var processedData = sensor.ProcessLogAsync(logData);

// Save to file
File.WriteAllText("sensor_log.json", JsonConvert.SerializeObject(processedData));
```

### Configuration Update Example
```csharp
// Update BT510 sensor configuration
var config = new Dictionary<string, object>
{
    { "SamplingRate", 10 },
    { "LogInterval", 60 }
};

bool success = await sensor.UpdateConfigurationAsync(config);
Console.WriteLine(success ? "Configuration updated successfully" : "Configuration update failed");
```

### Dummy Sensor Data Generation
```csharp
// For testing without hardware
var dummySensor = new DummySensor();
var testData = await dummySensor.DownloadLogAsync();

// Validate generated data
foreach (var dataPoint in testData)
{
    Debug.Assert(dataPoint.Temperature >= -40 && dataPoint.Temperature <= 125);
}
```

### Comprehensive Exception Handling
```csharp
try
{
    await device.ConnectAsync();
    // Other operations...
}
catch (Exception ex) when (ex is TimeoutException || ex is InvalidOperationException)
{
    // Handle specific exceptions
    Console.WriteLine($"Error: {ex.Message}");
}
catch (Exception ex)
{
    // Handle unexpected exceptions
    Console.WriteLine($"Unexpected error: {ex}");
    throw; // Rethrow if necessary
}
finally
{
    // Cleanup code
    await device.DisconnectAsync();
}
```

### Advanced Scenarios
```csharp
// Parallel processing of multiple devices
var tasks = devices.Select(device => ProcessDeviceAsync(device));
await Task.WhenAll(tasks);

// Batch characteristic discovery
var services = await device.GetServicesAsync();
var characteristicsTasks = services.Select(service => device.GetCharacteristicsAsync(service));
var allCharacteristics = await Task.WhenAll(characteristicsTasks);
```

### Performance Testing
```csharp
// Measure advertisement processing time
var stopwatch = Stopwatch.StartNew();
var results = parser.Parse(advertisementData);
stopwatch.Stop();
Console.WriteLine($"Advertisement parsed in {stopwatch.ElapsedMilliseconds} ms");

// Connection stability under load
var connectionTasks = Enumerable.Range(0, 10).Select(_ => device.ConnectAsync());
await Task.WhenAll(connectionTasks);
```

### Resource Leak Prevention
```csharp
// Ensure all tasks complete and resources are released
try
{
    await Task.WhenAll(activeTasks);
}
catch
{
    // Log and suppress exceptions
}
finally
{
    // Force cleanup
    foreach (var device in activeDevices)
    {
        device.Disconnect();
    }
}
```

### Graceful Degradation
```csharp
// Fallback to dummy sensor if BT510 not available
ISensor activeSensor;

try
{
    activeSensor = new BT510Sensor();
    await activeSensor.InitializeAsync();
}
catch
{
    Console.WriteLine("BT510 sensor initialization failed, falling back to DummySensor");
    activeSensor = new DummySensor();
}

// Use activeSensor for data processing
```

## License

This project is licensed under the terms specified in the [`LICENSE`](LICENSE)
