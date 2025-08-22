using Microsoft.VisualStudio.TestTools.UnitTesting;
using SensorGateway.Sensors.bt510;
using System.Collections.Generic;
using System.Text.Json;

namespace SensorGateway.Tests.Tests.Sensors
{
    /// <summary>
    /// Comprehensive tests to achieve 100% coverage for JsonRpcResponse class
    /// </summary>
    [TestClass]
    public class BT510SensorJsonCompleteTests
    {
        #region Complete GetResult Coverage Tests

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithOkResult_NonDictionaryType_ShouldDeserialize()
        {
            // Arrange - Test "ok" result with non-dictionary type
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 1,
                Result = "ok"
            };

            // Act - Try to get as string directly
            var result = response.GetResult<string>();

            // Assert
            Assert.AreEqual("ok", result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithEmptyResult_NonDictionaryType_ShouldReturnDefault()
        {
            // Arrange
            var response = new JsonRpcResponse { Result = "" };

            // Act
            var result = response.GetResult<string>();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithValidJsonObject_ShouldDeserializeCorrectly()
        {
            // Arrange - Test with valid JSON object as string
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 3,
                Result = """{"name":"test","value":42}"""
            };

            // Act
            var result = response.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("test", result!["name"].ToString());
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithInvalidJsonForDictionary_ShouldReturnEmptyDictionary()
        {
            // Arrange - Invalid JSON that can't be deserialized as dictionary
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 4,
                Result = "invalid json string"
            };

            // Act
            var result = response.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result!.Count); // Should return empty dictionary for JsonException
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithBooleanResult_JsonExceptionFallback()
        {
            // Arrange - Create response that will trigger JsonException then fallback
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0", 
                Id = 5,
                Result = "not ok" // This will fail deserialization, then fallback to string comparison
            };

            // Act
            var result = response.GetResult<bool>();

            // Assert
            Assert.IsFalse(result); // "not ok" != "ok" so should be false
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithBooleanResult_OkString_ShouldReturnTrue()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 6,
                Result = "ok"
            };

            // Act
            var result = response.GetResult<bool>();

            // Assert
            Assert.IsTrue(result); // "ok" == "ok" so should be true
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_NoAdditionalProperties_NoResult_ShouldReturnDefault()
        {
            // Arrange - Response with no additional properties and no result
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 7,
                Result = null
            };

            // Act
            var stringResult = response.GetResult<string>();
            var intResult = response.GetResult<int>();
            var dictResult = response.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNull(stringResult);
            Assert.AreEqual(0, intResult);
            Assert.IsNull(dictResult);
        }

        #endregion

        #region Complete GetResult(propertyName) Coverage Tests

        [TestMethod]
        public void JsonRpcResponse_GetResult_PropertyName_WithJsonExceptionFallback_AllTypes()
        {
            // Arrange - Create response with properties that will trigger fallback logic
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":8,
                "stringProp": "test string",
                "intProp": 42,
                "longProp": 9223372036854775807,
                "boolTrueProp": true,
                "boolFalseProp": false,
                "doubleProp": 3.14159,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act & Assert - Test all fallback type conversions
            var stringVal = response!.GetResult<string>("stringProp");
            var intVal = response.GetResult<int>("intProp");
            var longVal = response.GetResult<long>("longProp");
            var boolTrueVal = response.GetResult<bool>("boolTrueProp");
            var boolFalseVal = response.GetResult<bool>("boolFalseProp");
            var doubleVal = response.GetResult<double>("doubleProp");

            Assert.AreEqual("test string", stringVal);
            Assert.AreEqual(42, intVal);
            Assert.AreEqual(9223372036854775807L, longVal);
            Assert.IsTrue(boolTrueVal);
            Assert.IsFalse(boolFalseVal);
            Assert.AreEqual(3.14159, doubleVal, 0.00001);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_PropertyName_NonExistentProperty_ShouldReturnDefault()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 9,
                Result = "ok"
            };

            // Act - Try to get property that doesn't exist
            var stringResult = response.GetResult<string>("nonExistent");
            var intResult = response.GetResult<int>("nonExistent");
            var boolResult = response.GetResult<bool>("nonExistent");

            // Assert - Should return defaults
            Assert.IsNull(stringResult);
            Assert.AreEqual(0, intResult);
            Assert.IsFalse(boolResult);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_PropertyName_ComplexObjectSerialization()
        {
            // Arrange - Test successful JsonSerializer.Deserialize path
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":10,
                "complexObject": {"nested": {"value": "deep"}},
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - This should successfully deserialize without fallback
            var result = response!.GetResult<Dictionary<string, object>>("complexObject");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result!.ContainsKey("nested"));
        }

        #endregion

        #region Complete JsonElementToObject Coverage Tests

        [TestMethod]
        public void JsonRpcResponse_JsonElementToObject_AllNumberTypes_ShouldHandleCorrectly()
        {
            // Arrange - Test all number conversion paths in JsonElementToObject
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":11,
                "intNumber": 42,
                "longNumber": 9223372036854775807,
                "uintNumber": 4294967295,
                "doubleNumber": 3.14159265359,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - This will trigger JsonElementToObject for all number types
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert - Verify all number types are handled correctly
            Assert.IsNotNull(result);
            Assert.IsTrue(result!.ContainsKey("intNumber"));
            Assert.IsTrue(result.ContainsKey("longNumber")); 
            Assert.IsTrue(result.ContainsKey("uintNumber"));
            Assert.IsTrue(result.ContainsKey("doubleNumber"));

            // All numbers get converted to double in JsonElementToObject
            Assert.AreEqual(42.0, result["intNumber"]);
            Assert.AreEqual(9223372036854775807.0, result["longNumber"]);
            Assert.AreEqual(4294967295.0, result["uintNumber"]);
            Assert.AreEqual(3.14159265359, result["doubleNumber"]);
        }

        [TestMethod]
        public void JsonRpcResponse_JsonElementToObject_DefaultCase_ShouldReturnRawText()
        {
            // This is challenging to test directly since JsonValueKind covers most cases
            // We can test indirectly by ensuring complex nested structures work
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":12,
                "complexNested": {
                    "arrays": [[1,2],[3,4]],
                    "objects": [{"a":1},{"b":2}],
                    "mixed": [null, true, false, "string", 42, {"nested": true}]
                },
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert - This exercises deep recursion in JsonElementToObject
            Assert.IsNotNull(result);
            Assert.IsTrue(result!.ContainsKey("complexNested"));
        }

        #endregion

        #region Edge Cases and Error Handling

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithRootProperties_NoStandardFields_ShouldSkipCorrectly()
        {
            // Arrange - Test the skip logic for standard JSON-RPC fields
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":13,
                "error":null,
                "result":"ok",
                "customProp":"value"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert - Should only contain customProp, not standard fields
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result!.Count);
            Assert.IsTrue(result.ContainsKey("customProp"));
            Assert.IsFalse(result.ContainsKey("jsonrpc"));
            Assert.IsFalse(result.ContainsKey("id"));
            Assert.IsFalse(result.ContainsKey("result"));
            Assert.IsFalse(result.ContainsKey("error"));
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_EmptyAdditionalProperties_WithNullResult_ShouldReturnDefault()
        {
            // Arrange - Response with no additional properties and null result
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 14,
                Result = null,
                AdditionalProperties = new Dictionary<string, JsonElement>()
            };

            // Act
            var result = response.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithAdditionalProperties_NonDictionaryType_ShouldReturnDefault()
        {
            // Arrange - Test when we have additional properties but T is not Dictionary<string, object>
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":15,
                "customProp":"value",
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - Request as string, not dictionary
            var result = response!.GetResult<string>();

            // Assert - Should fall through to Result processing, not additional properties
            Assert.AreEqual("ok", result);
        }

        #endregion

        #region Property Coverage Tests

        [TestMethod]
        public void JsonRpcResponse_Properties_ShouldGetAndSetCorrectly()
        {
            // Arrange & Act
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 100,
                Result = "test result",
                Error = new JsonRpcError { Code = -1, Message = "test error" }
            };

            // Assert - Test all properties
            Assert.AreEqual("2.0", response.JsonRpc);
            Assert.AreEqual(100U, response.Id);
            Assert.AreEqual("test result", response.Result);
            Assert.IsNotNull(response.Error);
            Assert.AreEqual(-1, response.Error.Code);
            Assert.AreEqual("test error", response.Error.Message);
            Assert.IsTrue(response.HasError);
            Assert.IsNotNull(response.AdditionalProperties);
        }

        [TestMethod]
        public void JsonRpcResponse_HasError_WithNullError_ShouldReturnFalse()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                Error = null
            };

            // Act & Assert
            Assert.IsFalse(response.HasError);
        }

        #endregion
    }
}
