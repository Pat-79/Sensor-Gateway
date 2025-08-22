using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    /// BT510-compatible JSON-RPC response structure
    /// Handles both standard nested results and BT510's root-level properties
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

        // Capture all additional properties (BT510 puts properties at root level)
        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalProperties { get; set; } = new();

        public bool HasError => Error != null;

        /// <summary>
        /// Enhanced GetResult method that handles both standard JSON-RPC and BT510 formats
        /// </summary>
        public T? GetResult<T>()
        {
            // 🔧 Handle BT510 GET responses with properties at root level
            // Example: {"jsonrpc":"2.0","id":2,"mtu":244,"sensorName":"DTT-34179","result":"ok"}
            if (AdditionalProperties.Any())
            {
                var rootProperties = new Dictionary<string, object>();

                foreach (var kvp in AdditionalProperties)
                {
                    // Skip standard JSON-RPC fields
                    if (kvp.Key == "jsonrpc" || kvp.Key == "id" || kvp.Key == "result" || kvp.Key == "error")
                        continue;

                    rootProperties[kvp.Key] = JsonElementToObject(kvp.Value);
                }

                // If we found root-level properties and T is Dictionary<string, object>
                if (rootProperties.Any() && typeof(T) == typeof(Dictionary<string, object>))
                {
                    return (T)(object)rootProperties;
                }
            }

            // 🔧 Handle BT510 SET responses and other simple results
            // Example: {"jsonrpc":"2.0","id":3,"result":"ok"}
            if (Result != null)
            {
                var resultStr = Result.ToString();

                // For Dictionary<string, object> requests with simple results like "ok"
                if (typeof(T) == typeof(Dictionary<string, object>))
                {
                    // If result is just "ok" or similar, return empty dictionary (success)
                    if (resultStr == "ok" || string.IsNullOrEmpty(resultStr))
                    {
                        return (T)(object)new Dictionary<string, object>();
                    }

                    // Try to deserialize as dictionary
                    try
                    {
                        return JsonSerializer.Deserialize<T>(resultStr!);
                    }
                    catch (JsonException)
                    {
                        // Not a valid dictionary, return empty dictionary for "ok" responses
                        return (T)(object)new Dictionary<string, object>();
                    }
                }

                // For other types, try direct deserialization
                try
                {
                    return JsonSerializer.Deserialize<T>(resultStr!);
                }
                catch (JsonException)
                {
                    // If direct deserialization fails, try converting basic types
                    if (typeof(T) == typeof(string))
                        return (T)(object)resultStr!;

                    if (typeof(T) == typeof(bool))
                        return (T)(object)(resultStr == "ok");
                }
            }

            return default;
        }

        /// <summary>
        /// Get a specific property value from BT510's root-level properties
        /// </summary>
        /// <typeparam name="T">Expected type of the property</typeparam>
        /// <param name="propertyName">Name of the property to extract</param>
        /// <returns>Typed property value or default if not found</returns>
        public T GetResult<T>(string propertyName)
        {
            // 🎯 BT510 GET responses: Properties appear at root level
            // Example: {"jsonrpc":"2.0","id":2,"mtu":244,"sensorName":"DTT-34179","result":"ok"}

            if (AdditionalProperties.TryGetValue(propertyName, out var jsonElement))
            {
                try
                {
                    var result = JsonSerializer.Deserialize<T>(jsonElement);
                    return result ?? default(T)!;
                }
                catch (JsonException)
                {
                    // Fallback for basic type conversion
                    if (typeof(T) == typeof(string))
                        return (T)(object)jsonElement.GetString()!;
                    if (typeof(T) == typeof(int) && jsonElement.TryGetInt32(out var intVal))
                        return (T)(object)intVal;
                    if (typeof(T) == typeof(long) && jsonElement.TryGetInt64(out var longVal))
                        return (T)(object)longVal;
                    if (typeof(T) == typeof(bool))
                    {
                        var boolValue = jsonElement.ValueKind == JsonValueKind.True;
                        return (T)(object)boolValue;
                    }
                    if (typeof(T) == typeof(double) && jsonElement.TryGetDouble(out var doubleVal))
                        return (T)(object)doubleVal;
                }
            }

            return default(T)!;
        }

        /// <summary>
        /// Helper method to convert JsonElement to appropriate .NET object type
        /// </summary>
        private static object JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!,
                JsonValueKind.Number => element.TryGetInt32(out int intValue) ? intValue :
                                       element.TryGetInt64(out long longValue) ? longValue :
                                       element.TryGetUInt32(out uint uintValue) ? uintValue :
                                       element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToArray(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
                _ => element.GetRawText()
            };
        }
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
}