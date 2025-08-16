# Sensor Gateway

A Linux Bluetooth Low Energy (BLE) sensor gateway for acquiring sensor data from BLE-enabled sensors. The gateway supports multiple sensor types and implements efficient data collection strategies to minimize battery drain on sensor devices.

## Overview

The Sensor Gateway is designed to continuously scan for BLE sensors, collect measurement data, and forward it to configured endpoints. It supports both advertisement-based data collection (passive) and active connection-based log downloading, with intelligent scheduling to minimize battery impact on sensor devices.

## Key Features

- **Multi-Sensor Support**: Extensible architecture supporting various sensor types (BT510, dummy sensors for testing)
- **Dual Data Collection**: 
  - Advertisement data parsing (passive, battery-efficient)
  - Active log downloading via BLE connections when needed
- **Intelligent Scheduling**: Devices are marked after processing to prevent unnecessary reconnections
- **Bluetooth Stack Protection**: Token-based RAII system to prevent overwhelming the Bluetooth stack
- **Background Scanning**: Continuous scanning thread with configurable intervals and device filters
- **Worker Process Architecture**: Each discovered device spawns an independent worker process

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

#### Device Abstraction ([`BTDevice`](src/bt/btdevice.cs))
- Unified interface for different BLE device types
- Handles device property extraction and manufacturer data processing
- Supports both BT510 sensors and dummy devices for testing

#### Sensor Framework
- **Base Sensor Class** ([`Sensor`](src/sensor/sensor.cs)): Abstract base providing common sensor functionality
- **BT510 Sensor** ([`BT510Sensor`](src/sensor/sensor_bt510.cs)): JSON-RPC communication with BT510 devices
- **Dummy Sensor** ([`DummySensor`](src/sensor/sensor_dummy.cs)): Testing and development sensor implementation

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

    class DeviceType {
        <<enumeration>>
        BT510
        Dummy
    }

    class SensorType {
        <<enumeration>>
        BT510
        Dummy
    }

    class BluetoothConfig {
        +AdapterName: string
        +DiscoveryTimeoutSeconds: int
        +ConnectionTimeoutSeconds: int
        +DeviceNameFilters: List~string~
        +ServiceUuidFilters: List~string~
        +MinRssiThreshold: short
    }

    Scanner --> BTManager : uses tokens
    Scanner --> BTDevice : creates from BlueZ devices
    BTManager --> BTToken : manages pool
    BTDevice --> DeviceType : determines type
    BTDevice --> SensorType : maps to sensor
    Sensor --> BTDevice : wraps device
    BT510Sensor --|> Sensor : implements
    DummySensor --|> Sensor : implements
    BT510Sensor --> BTManager : requires tokens
    Scanner --> BluetoothConfig : uses configuration
```
