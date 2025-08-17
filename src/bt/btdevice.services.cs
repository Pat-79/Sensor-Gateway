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
    public partial class BTDevice
    {
        #region Private Service Fields
        IGattService1? _service = null;
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
            characteristicUuid = BlueZManager.NormalizeUUID(characteristicUuid);
            return GetCharacteristicAsync(service, characteristicUuid).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sets the service for the device, allowing it to communicate with the specified service.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to set</param>
        public async Task SetServiceAsync(string serviceUuid)
        {
            // Ensure that we're not interfering with another operation
            if (CommunicationInProgress)
            {
                throw new InvalidOperationException("Communication is already in progress. Please wait for the current operation to complete.");
            }

            // Connect to service
            serviceUuid = BlueZManager.NormalizeUUID(serviceUuid);
            _service = await GetServiceAsync(serviceUuid);
            if (_service == null)
            {
                throw new InvalidOperationException($"Service '{serviceUuid}' not found on device '{Address}'.");
            }
        }

        /// <summary>
        /// Sets the service for the device, allowing it to communicate with the specified service.
        /// </summary>
        /// <param name="serviceUuid">The UUID of the service to set</param>
        public void SetService(string serviceUuid)
        {
            serviceUuid = BlueZManager.NormalizeUUID(serviceUuid);
            SetServiceAsync(serviceUuid).GetAwaiter().GetResult();
        }

        #endregion
    }
}