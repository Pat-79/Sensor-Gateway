using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SensorGateway.Bluetooth;

namespace SensorGateway.Sensors.bt510
{
    #region JSON-RPC Data Structures

    /// <summary>
    /// Simplified JSON-RPC request structure
    /// </summary>
    public class JsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("method")]
        public string Method { get; set; } = "";

        [JsonPropertyName("params")]
        public object? Params { get; set; }

        [JsonPropertyName("id")]
        public uint Id { get; set; }
    }

    /// <summary>
    /// Simplified JSON-RPC response structure
    /// </summary>
    public class JsonRpcResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "";

        [JsonPropertyName("result")]
        public object? Result { get; set; }

        [JsonPropertyName("error")]
        public JsonRpcError? Error { get; set; }

        [JsonPropertyName("id")]
        public uint Id { get; set; }

        public bool HasError => Error != null;
        public T? GetResult<T>() => Result != null ? JsonSerializer.Deserialize<T>(Result.ToString()!) : default;
    }

    public class JsonRpcError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("data")]
        public object? Data { get; set; }
    }

    #endregion

    #region BT510 Communication Implementation

    public partial class BT510Sensor
    {
        #region Private Fields
        private uint _nextId = 1;
        private bool _isInitialized = false;
        private bool _eventHandlerRegistered = false;
        #endregion

        #region ID Management
        /// <summary>
        /// Thread-safe increment of the JSON-RPC ID counter
        /// </summary>
        private uint GetNextId()
        {
            return (uint)Interlocked.Increment(ref Unsafe.As<uint, int>(ref _nextId));
        }
        #endregion

        #region JSON-RPC Core Communication
        /// <summary>
        /// Initializes the BT510 communication setup asynchronously
        /// </summary>
        public async Task InitializeCommunicationAsync()
        {
            if (Device == null)
                throw new InvalidOperationException("Device is not initialized");

            // Configure service and characteristics only once
            if (!_isInitialized)
            {
                await Device.SetServiceAsync(_bt510Config.CustomServiceUuid);
                await Device.SetNotificationsAsync(_bt510Config.JsonRpcResponseCharUuid);
                await Device.SetCommandCharacteristicAsync(_bt510Config.JsonRpcCommandCharUuid);
                _isInitialized = true;
                Console.WriteLine("BT510 communication services initialized");
            }

            // Register event handler only once
            if (!_eventHandlerRegistered)
            {
                Device.NotificationDataReceived += OnNotificationDataReceived;
                _eventHandlerRegistered = true;
                Console.WriteLine("BT510 event handler registered");
            }
        }

        /// <summary>
        /// Simplified JSON-RPC request sender based on TestProgram.cs pattern
        /// </summary>
        private async Task<JsonRpcResponse?> SendRequestAsync(string method, object? parameters = null)
        {
            // Check if device is initialized
            if (Device == null)
                throw new InvalidOperationException("Device is not initialized");

            // Prepare request json
            var request = new JsonRpcRequest
            {
                Method = method,
                Params = parameters,
                Id = GetNextId()
            };

            // Serialize & create a byte array from the request
            string requestJson = JsonSerializer.Serialize(request);
            byte[] requestBytes = System.Text.Encoding.UTF8.GetBytes(requestJson);

            // Reset buffer & Write the command and wait for data to be received with a notification
            await Device.ClearBufferAsync();
            await Device.WriteWithoutResponseAsync(requestBytes, waitForNotificationDataReceived: true);

            // Get the response data
            var responseData = await Device.GetBufferDataAsync();
            if (responseData.Length == 0)
            {
                return null;
            }

            // Deserialize the response data
            var responseStr = System.Text.Encoding.UTF8.GetString(responseData);
            Console.WriteLine($"Received response: {responseStr}");
            var responseJson = JsonSerializer.Deserialize<JsonRpcResponse>(responseStr);

            // Validate the ID of the response, must be the same ID as the request
            var responseId = responseJson?.Id;
            if (responseId == null || responseId != request.Id)
            {
                Console.WriteLine($"Response ID mismatch: expected {request.Id}, got {responseId}");
                throw new InvalidOperationException($"Response ID mismatch: expected {request.Id}, got {responseId}");
            }

            // Return the response object
            Console.WriteLine($"Received response: {responseStr}");
            return JsonSerializer.Deserialize<JsonRpcResponse>(responseStr);
        }
        #endregion

        #region BT510 JSON-RPC Methods

        /// <summary>
        /// Get attribute values from the BT510 sensor
        /// </summary>
        public async Task<Dictionary<string, object>?> GetAsync(params string[] properties)
        {
            var response = await SendRequestAsync("get", properties);
            return response?.GetResult<Dictionary<string, object>>();
        }

        /// <summary>
        /// Set attributes on the BT510 sensor
        /// </summary>
        public async Task<bool> SetAsync(Dictionary<string, object> properties)
        {
            var response = await SendRequestAsync("set", properties);
            return response != null && !response.HasError;
        }

        /// <summary>
        /// Read all attributes from the sensor
        /// </summary>
        public async Task<Dictionary<string, object>?> DumpAsync(int? mode = null)
        {
            var response = await SendRequestAsync("dump", mode);
            return response?.GetResult<Dictionary<string, object>>();
        }

        /// <summary>
        /// Restart the BT510 sensor
        /// </summary>
        public async Task<bool> RebootAsync(int bootloaderMode = 0)
        {
            object? parameters = bootloaderMode == 0 ? null : bootloaderMode;
            var response = await SendRequestAsync("reboot", parameters);
            return response != null && !response.HasError;
        }

        /*
            The Factory Reset method is not implemented, because
            it's a dangerous method. Mistakenly calling it could
            lead to loss of all configuration and data on the sensor.
            Uncomment the following lines when factory reset needs
            to be implemented:
        */
        /*
        /// <summary>
        /// Reset to factory settings
        /// </summary>
        public async Task<bool> FactoryResetAsync()
        {
            throw new NotImplementedException("Factory reset is not implemented for safety reasons.");

            var response = await SendRequestAsync("factoryReset");
            return response != null && !response.HasError;
        }
        */

        /// <summary>
        /// Prepare sensor log for reading
        /// </summary>
        public async Task<uint?> PrepareLogAsync(int mode = 0)
        {
            // TestProgram.cs shows params as array: "params": [0]
            var response = await SendRequestAsync("prepareLog", new[] { mode });
            return response?.GetResult<uint>();
        }

        /// <summary>
        /// Read log entries from the sensor
        /// </summary>
        public async Task<byte[]?> ReadLogAsync(int numberOfEvents)
        {
            // TestProgram.cs shows params as array: "params": [500]
            var response = await SendRequestAsync("readLog", new[] { numberOfEvents });

            if (response?.Result == null)
                return null;

            // Parse the result array [numberOfEntries, base64EncodedData]
            var resultArray = JsonSerializer.Deserialize<object[]>(response.Result.ToString()!);
            if (resultArray == null || resultArray.Length < 2)
                return null;

            // The second element contains the base64 encoded data
            var base64Data = resultArray[1]?.ToString();
            if (string.IsNullOrEmpty(base64Data))
                return null;

            try
            {
                return Convert.FromBase64String(base64Data);
            }
            catch (FormatException)
            {
                // Invalid base64 data
                return null;
            }
        }

        /// <summary>
        /// Acknowledge and invalidate log entries
        /// </summary>
        public async Task<uint?> AckLogAsync(int numberOfEvents)
        {
            var response = await SendRequestAsync("ackLog", new[] { numberOfEvents });
            return response?.GetResult<uint>();
        }

        /// <summary>
        /// Set epoch time reference
        /// </summary>
        public async Task<bool> SetEpochAsync(long epochSeconds)
        {
            var response = await SendRequestAsync("setEpoch", new[] { epochSeconds });
            return response != null && !response.HasError;
        }

        /// <summary>
        /// Get current epoch time
        /// </summary>
        public async Task<long?> GetEpochAsync()
        {
            var response = await SendRequestAsync("getEpoch");
            return response?.GetResult<long>();
        }

        /// <summary>
        /// Test LEDs on the sensor
        /// </summary>
        public async Task<bool> LedTestAsync(int durationMs)
        {
            var response = await SendRequestAsync("ledTest", durationMs);
            return response != null && !response.HasError;
        }

        #endregion

        #region Event Handlers
        // Fix the event handler signature
        private void OnNotificationDataReceived(object sender, byte[]? data, string uuid)
        {
            if (data == null || data.Length == 0)
            {
                // No data received, nothing to process
                return;
            }

            // The data MTU is set to 244 bytes. A full data chunk is 244 bytes.
            // If the data length is less than 244 bytes, it indicates a non-full data chunk,
            // which means it's the last part of a JSON-RPC response.
            // Else, check if the last byte is a '}'-sign, it indicates the end of a JSON-RPC response.
            if (data.Length < 244 || (data.Length > 0 && data[data.Length - 1] == (byte)'}'))
            {
                // Cast sender to BTDevice if you need device-specific operations
                var device = sender as BTDevice;
                device?.StopCommunication();
            }
        }

        #endregion

        #region High-Level Convenience Methods

        /// <summary>
        /// Get common sensor configuration
        /// </summary>
        public async Task<Dictionary<string, object>?> GetConfigurationAsync(List<string>? properties = null)
        {
            // If no specific properties requested, return all common ones
            if (properties == null || properties.Count == 0)
            {
                properties = new List<string>();
            }

            var response = await GetAsync(properties.ToArray());
            return response;
        }

        /// <summary>
        /// Update sensor configuration
        /// </summary>
        public async Task<bool> UpdateConfigurationAsync(Dictionary<string, object> config)
        {
            return await SetAsync(config);
        }

        /// <summary>
        /// Download all available logs using configuration values
        /// </summary>
        public async Task<byte[]?> DownloadAllLogsAsync()
        {
            var prepLog = await PrepareLogAsync();
            if (prepLog == null || prepLog == 0)
            {
                return null;
            }

            var logData = await ReadLogAsync(500);
            return logData;
        }

        /// <summary>
        /// Synchronize time with current system time
        /// </summary>
        public async Task<bool> SynchronizeTimeAsync()
        {
            var currentEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return await SetEpochAsync(currentEpoch);
        }

        #endregion
    }
    #endregion
}