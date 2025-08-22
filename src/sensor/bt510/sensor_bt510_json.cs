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
        /// Optimized for performance with reduced allocations and type checks
        /// </summary>
        public T? GetResult<T>()
        {
            // Cache the type check to avoid repeated reflection calls
            var isDictionaryType = typeof(T) == typeof(Dictionary<string, object>);
            
            // Handle BT510 GET responses with properties at root level
            if (isDictionaryType && AdditionalProperties.Count > 0)
            {
                var rootProperties = ExtractRootProperties();
                if (rootProperties.Count > 0)
                    return (T)(object)rootProperties;
            }

            // Early return for null result
            if (Result == null) 
                return default;

            // Avoid string conversion for non-string results when possible
            if (Result is JsonElement jsonElement)
                return ProcessJsonElement<T>(jsonElement, isDictionaryType);
            
            // Handle string results
            var resultStr = Result.ToString();
            if (string.IsNullOrEmpty(resultStr)) 
                return default;

            return ProcessStringResult<T>(resultStr, isDictionaryType);
        }

        /// <summary>
        /// Extract non-standard JSON-RPC properties from root level
        /// </summary>
        private Dictionary<string, object> ExtractRootProperties()
        {
            var rootProperties = new Dictionary<string, object>(AdditionalProperties.Count);
            
            foreach (var (key, value) in AdditionalProperties)
            {
                if (!IsStandardJsonRpcField(key))
                    rootProperties[key] = JsonElementToObject(value);
            }
            
            return rootProperties;
        }

        /// <summary>
        /// Process JsonElement results without string conversion
        /// </summary>
        private static T? ProcessJsonElement<T>(JsonElement jsonElement, bool isDictionaryType)
        {
            if (isDictionaryType)
            {
                // Try direct JsonElement to Dictionary conversion
                if (jsonElement.ValueKind == JsonValueKind.Object)
                {
                    var dict = jsonElement.EnumerateObject()
                        .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value));
                    return (T)(object)dict;
                }
                
                // Handle "ok" response or other simple cases
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    var str = jsonElement.GetString();
                    if (str == "ok")
                        return (T)(object)new Dictionary<string, object>();
                }
            }

            // Try direct JsonElement deserialization (most efficient)
            if (TryDeserializeJsonElement<T>(jsonElement, out var result))
                return result;

            return default;
        }

        /// <summary>
        /// Process string-based results (fallback)
        /// </summary>
        private static T? ProcessStringResult<T>(string resultStr, bool isDictionaryType)
        {
            if (isDictionaryType)
            {
                if (resultStr == "ok")
                    return (T)(object)new Dictionary<string, object>();

                if (TryDeserialize<T>(resultStr, out var dictResult))
                    return dictResult;
                
                return (T)(object)new Dictionary<string, object>();
            }

            // Try JSON deserialization first (most common case)
            if (TryDeserialize<T>(resultStr, out var result))
                return result;

            // Fast path for common types
            return ConvertStringToType<T>(resultStr);
        }

        /// <summary>
        /// Fast conversion for common string-to-type scenarios
        /// </summary>
        private static T? ConvertStringToType<T>(string value)
        {
            var targetType = typeof(T);
            
            // Use pattern matching for better performance
            return targetType.Name switch
            {
                nameof(String) => (T)(object)value,
                nameof(Boolean) => (T)(object)(value == "ok"),
                nameof(Int32) when int.TryParse(value, out var intVal) => (T)(object)intVal,
                nameof(Double) when double.TryParse(value, out var doubleVal) => (T)(object)doubleVal,
                _ => default
            };
        }

        /// <summary>
        /// Get a specific property value from BT510's root-level properties
        /// </summary>
        public T GetResult<T>(string propertyName)
        {
            if (!AdditionalProperties.TryGetValue(propertyName, out var jsonElement))
                return default(T)!;

            if (TryDeserializeJsonElement<T>(jsonElement, out var result))
                return result;

            return default(T)!;
        }

        private static bool IsStandardJsonRpcField(string fieldName) =>
            fieldName is "jsonrpc" or "id" or "result" or "error";

        private static bool TryDeserialize<T>(string json, out T? result)
        {
            try
            {
                result = JsonSerializer.Deserialize<T>(json);
                return true;
            }
            catch (JsonException)
            {
                result = default;
                return false;
            }
        }

        private static bool TryDeserializeJsonElement<T>(JsonElement element, out T result)
        {
            try
            {
                var deserializedResult = JsonSerializer.Deserialize<T>(element);
                if (deserializedResult != null)
                {
                    result = deserializedResult;
                    return true;
                }
                else
                {
                    result = default(T)!;
                    return false;
                }
            }
            catch (JsonException)
            {
                result = TryConvertBasicType<T>(element);
                return !EqualityComparer<T>.Default.Equals(result, default(T));
            }
        }

        private static T TryConvertBasicType<T>(JsonElement element)
        {
            var targetType = typeof(T);
            
            if (targetType == typeof(string))
                return (T)(object)element.GetString()!;
            
            if (targetType == typeof(int) && element.TryGetInt32(out var intVal))
                return (T)(object)intVal;
            
            if (targetType == typeof(long) && element.TryGetInt64(out var longVal))
                return (T)(object)longVal;
            
            if (targetType == typeof(bool))
                return (T)(object)(element.ValueKind == JsonValueKind.True);
            
            if (targetType == typeof(double) && element.TryGetDouble(out var doubleVal))
                return (T)(object)doubleVal;

            return default(T)!;
        }

        private static object JsonElementToObject(JsonElement element) => element.ValueKind switch
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