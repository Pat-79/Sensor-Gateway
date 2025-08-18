using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Add this for Last() extension method
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using SensorGateway.Configuration;
using HashtagChris.DotNetBlueZ;
using HashtagChris.DotNetBlueZ.Extensions;
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
        #region Constants
        const string LAIRD_CUSTOM_SERVICE_UUID = "569a1101-b87f-490c-92cb-11ba5ea5167c";
        const string LAIRD_JSONRPC_RESPONSE_CHAR_UUID = "569a2000-b87f-490c-92cb-11ba5ea5167c";
        const string LAIRD_JSONRPC_COMMAND_CHAR_UUID = "569a2001-b87f-490c-92cb-11ba5ea5167c";
        #endregion

        #region Private Fields
        private uint _nextId = 1;
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

            // Setup the service and characteristics as shown in TestProgram.cs
            // These operations might be async in the BTDevice implementation
            await Device.SetServiceAsync(LAIRD_CUSTOM_SERVICE_UUID).ConfigureAwait(false);
            await Device.SetNotificationsAsync(LAIRD_JSONRPC_RESPONSE_CHAR_UUID).ConfigureAwait(false);
            await Device.SetCommandCharacteristicAsync(LAIRD_JSONRPC_COMMAND_CHAR_UUID).ConfigureAwait(false);
            
            Device.NotificationDataReceived += OnNotificationDataReceived;
        }

        /// <summary>
        /// Simplified JSON-RPC request sender based on TestProgram.cs pattern
        /// </summary>
        private async Task<JsonRpcResponse?> SendRequestAsync(string method, object? parameters = null)
        {
            if (Device == null)
                throw new InvalidOperationException("Device is not initialized");

            var request = new JsonRpcRequest    
            {
                Method = method,
                Params = parameters,
                Id = GetNextId()
            };

            string requestJson = JsonSerializer.Serialize(request);
            byte[] requestBytes = System.Text.Encoding.UTF8.GetBytes(requestJson);

            await Device.ClearBufferAsync().ConfigureAwait(false); // Clear any previous data in the buffer

            // Write the command and wait for data to be received with a notification
            await Device.WriteWithoutResponseAsync(requestBytes, waitForNotificationDataReceived: true).ConfigureAwait(false);
            
            // Get the response data
            var responseData = await Device.GetBufferDataAsync().ConfigureAwait(false);
            if (responseData.Length == 0)
                return null;


            var responseJson = System.Text.Encoding.UTF8.GetString(responseData);
            Console.WriteLine($"Received response: {responseJson}");
            return JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
        }
        #endregion

        #region BT510 JSON-RPC Methods

        /// <summary>
        /// Get attribute values from the BT510 sensor
        /// </summary>
        public async Task<Dictionary<string, object>?> GetAsync(params string[] properties)
        {
            var response = await SendRequestAsync("get", properties).ConfigureAwait(false);
            return response?.GetResult<Dictionary<string, object>>();
        }

        /// <summary>
        /// Set attributes on the BT510 sensor
        /// </summary>
        public async Task<bool> SetAsync(Dictionary<string, object> properties)
        {
            var response = await SendRequestAsync("set", properties).ConfigureAwait(false);
            return response != null && !response.HasError;
        }

        /// <summary>
        /// Read all attributes from the sensor
        /// </summary>
        public async Task<Dictionary<string, object>?> DumpAsync(int? mode = null)
        {
            var response = await SendRequestAsync("dump", mode).ConfigureAwait(false);
            return response?.GetResult<Dictionary<string, object>>();
        }

        /// <summary>
        /// Restart the BT510 sensor
        /// </summary>
        public async Task<bool> RebootAsync(int bootloaderMode = 0)
        {
            object? parameters = bootloaderMode == 0 ? null : bootloaderMode;
            var response = await SendRequestAsync("reboot", parameters).ConfigureAwait(false);
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
            //var response = await SendRequestAsync("factoryReset").ConfigureAwait(false);
            //return response != null && !response.HasError;
            throw new NotImplementedException("Factory reset is not implemented for safety reasons.");
        }
        */

        /// <summary>
        /// Prepare sensor log for reading
        /// </summary>
        public async Task<bool> PrepareLogAsync(int mode = 0)
        {
            // TestProgram.cs shows params as array: "params": [0]
            var response = await SendRequestAsync("prepareLog", new[] { mode }).ConfigureAwait(false);
            return response != null && !response.HasError;
        }

        /// <summary>
        /// Read log entries from the sensor
        /// </summary>
        public async Task<byte[]?> ReadLogAsync(int numberOfEvents)
        {
            // TestProgram.cs shows params as array: "params": [500]
            var response = await SendRequestAsync("readLog", new[] { numberOfEvents }).ConfigureAwait(false);
            
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
        public async Task<bool> AckLogAsync(int numberOfEvents)
        {
            var response = await SendRequestAsync("ackLog", new[] { numberOfEvents }).ConfigureAwait(false);
            return response != null && !response.HasError;
        }

        /// <summary>
        /// Set epoch time reference
        /// </summary>
        public async Task<bool> SetEpochAsync(long epochSeconds)
        {
            var response = await SendRequestAsync("setEpoch", new[] { epochSeconds }).ConfigureAwait(false);
            return response != null && !response.HasError;
        }

        /// <summary>
        /// Get current epoch time
        /// </summary>
        public async Task<long?> GetEpochAsync()
        {
            var response = await SendRequestAsync("getEpoch").ConfigureAwait(false);
            return response?.GetResult<long>();
        }

        /// <summary>
        /// Test LEDs on the sensor
        /// </summary>
        public async Task<bool> LedTestAsync(int durationMs)
        {
            var response = await SendRequestAsync("ledTest", durationMs).ConfigureAwait(false);
            return response != null && !response.HasError;
        }

        #endregion

        #region Event Handlers
        // Fix the event handler signature
        private void OnNotificationDataReceived(object sender, byte[]? data, string uuid)
        {
            Console.Write(".");
            if (data == null || data.Length == 0)
            {
                // No data received, nothing to process
                return;
            }

            // Cast sender to BTDevice if you need device-specific operations
            var device = sender as BTDevice;

            // Check if last byte equals a }-sign, since this indicates the end of a JSON-RPC response
            if (data.Last() == '}')
            {
                Console.WriteLine(System.Text.Encoding.UTF8.GetString(data));
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

            var response = await GetAsync(properties.ToArray()).ConfigureAwait(false);
            return response;
        }

        /// <summary>
        /// Update sensor configuration
        /// </summary>
        public async Task<bool> UpdateConfigurationAsync(Dictionary<string, object> config)
        {
            return await SetAsync(config).ConfigureAwait(false);
        }

        /// <summary>
        /// Download all available logs in batches
        /// </summary>
        public async Task<List<byte[]>> DownloadAllLogsAsync()
        {
            var allLogs = new List<byte[]>();
            
            // Prepare in FIFO mode
            await PrepareLogAsync(0).ConfigureAwait(false);
            
            const int batchSize = 128; // Max size of a batch is 128 log entries: 8 bytes * 128 = 1024 bytes of data
            byte[]? batch;
            
            do
            {
                batch = await ReadLogAsync(batchSize).ConfigureAwait(false);
                if (batch?.Length > 0)
                {
                    allLogs.Add(batch);
                    // For acknowledgment, we need the number of entries, not the byte length
                    // Each log entry is 8 bytes, so divide by 8
                    var entryCount = batch.Length / 8;
                    
                    // For testing: no ack for the download, as this will clear the logs
                    //await AckLogAsync(entryCount).ConfigureAwait(false);
                }
            } while (batch?.Length == (batchSize * 8)); // 128 entries × 8 bytes each = 1024 bytes
            
            return allLogs;
        }

        /// <summary>
        /// Synchronize time with current system time
        /// </summary>
        public async Task<bool> SynchronizeTimeAsync()
        {
            var currentEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return await SetEpochAsync(currentEpoch).ConfigureAwait(false);
        }

        #endregion
    }
    #endregion
}