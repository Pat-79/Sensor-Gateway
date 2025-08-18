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
    /// Handles Bluetooth GATT service and characteristic management.
    /// Follows Single Responsibility Principle by focusing solely on service discovery and access.
    /// </summary>
    public class BTDeviceServices
    {
        #region Private Service Fields
        private IGattService1? _service = null;
        private readonly Func<Task<bool>> _isConnectedAsync;
        private readonly Func<Task> _connectAsync;
        private readonly Func<bool> _getCommunicationInProgress;
        private readonly Func<Task<Device?>> _getDevice;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the currently selected service.
        /// </summary>
        public IGattService1? CurrentService => _service;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of BTDeviceServices with necessary dependencies.
        /// </summary>
        /// <param name="isConnectedAsync">Function to check if device is connected</param>
        /// <param name="connectAsync">Function to connect to device</param>
        /// <param name="getCommunicationInProgress">Function to check if communication is in progress</param>
        /// <param name="getDevice">Function to get the BlueZ device</param>
        public BTDeviceServices(
            Func<Task<bool>> isConnectedAsync,
            Func<Task> connectAsync,
            Func<bool> getCommunicationInProgress,
            Func<Task<Device?>> getDevice)
        {
            _isConnectedAsync = isConnectedAsync;
            _connectAsync = connectAsync;
            _getCommunicationInProgress = getCommunicationInProgress;
            _getDevice = getDevice;
        }
        #endregion

        #region Service and Characteristic Management

        /// <summary>
        /// Asynchronously retrieves a list of all services available on the connected Bluetooth device.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is a read-only list of
        /// service UUIDs available on the device.
        /// </returns>
        public async Task<IReadOnlyList<string>> GetServicesAsync()
        {
            if (!await _isConnectedAsync())
            {
                await _connectAsync();
            }

            var device = await _getDevice();
            if (device == null)
            {
                throw new InvalidOperationException("Device not initialized");
            }

            var services = await device.GetServicesAsync();
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
            return service != null;
        }

        /// <summary>
        /// Synchronously checks if the device has a specific service identified by the provided UUID.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to check.</param>
        /// <returns>True if the service exists, false otherwise.</returns>
        public bool HasService(string serviceUuid)
        {
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

            if (!await _isConnectedAsync())
            {
                await _connectAsync();
            }

            var device = await _getDevice();
            if (device == null)
            {
                throw new InvalidOperationException("Device not initialized");
            }

            return await device.GetServiceAsync(uuid);
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
            return GetServiceAsync(serviceUuid).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sets the service for the device, allowing it to communicate with the specified service.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to set</param>
        public async Task SetServiceAsync(string serviceUuid)
        {
            // Ensure that we're not interfering with another operation
            if (_getCommunicationInProgress())
            {
                throw new InvalidOperationException("Communication is already in progress. Please wait for the current operation to complete.");
            }

            // Connect to service
            serviceUuid = BlueZManager.NormalizeUUID(serviceUuid);
            _service = await GetServiceAsync(serviceUuid);
            if (_service == null)
            {
                throw new InvalidOperationException($"Service '{serviceUuid}' not found on device.");
            }
        }

        /// <summary>
        /// Sets the service for the device, allowing it to communicate with the specified service.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to set</param>
        public void SetService(string serviceUuid)
        {
            SetServiceAsync(serviceUuid).GetAwaiter().GetResult();
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

            if (!await _isConnectedAsync())
            {
                await _connectAsync();
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

            if (!await _isConnectedAsync())
            {
                await _connectAsync();
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
            return GetCharacteristicAsync(service, characteristicUuid).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Checks if the device has a specific characteristic within a service identified by the provided UUIDs.
        /// </summary>
        /// <param name="service">The service to check for the characteristic</param>
        /// <param name="characteristicUuid">The UUID of the characteristic to check</param>
        /// <returns>True if the characteristic exists within the service, otherwise false</returns>
        public async Task<bool> HasCharacteristicAsync(IGattService1 service, string characteristicUuid)
        {
            var uuid = BlueZManager.NormalizeUUID(characteristicUuid);
            var characteristic = await service.GetCharacteristicAsync(uuid);
            return characteristic != null;
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
            return HasCharacteristicAsync(service, characteristicUuid).GetAwaiter().GetResult();
        }

        #endregion
    }
}
