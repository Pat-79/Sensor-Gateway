using Microsoft.VisualStudio.TestTools.UnitTesting;
using SensorGateway.Sensors.bt510;
using System.Collections.Generic;
using System.Text.Json;

namespace SensorGateway.Tests.Tests.Sensors
{
    /// <summary>
    /// Ultra-specific tests to achieve 100% coverage for JsonRpcResponse
    /// </summary>
    [TestClass]
    public class BT510SensorJsonUltimateTests
    {
        #region Ultra-Specific Edge Cases

        [TestMethod]
        public void JsonRpcResponse_GetResult_EmptyStringResult_DictionaryType_ShouldReturnEmptyDict()
        {
            // Arrange - Empty string result for dictionary type
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 1,
                Result = "" // Empty string
            };

            // Act
            var result = response.GetResult<Dictionary<string, object>>();

            // Assert - Should return empty dictionary for empty string
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result!.Count);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_NullStringResult_DictionaryType_ShouldReturnNull()
        {
            // Arrange - Null string result
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0", 
                Id = 2,
                Result = null
            };

            // Act
            var result = response.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_ValidJsonString_NonDictionaryType_ShouldDeserialize()
        {
            // Arrange - Valid JSON string but requesting non-dictionary type
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 3,
                Result = """["array", "of", "strings"]"""
            };

            // Act - Request as string array
            var result = response.GetResult<string[]>();

            // Assert - Should successfully deserialize
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result!.Length);
            Assert.AreEqual("array", result[0]);
            Assert.AreEqual("of", result[1]);
            Assert.AreEqual("strings", result[2]);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_JsonExceptionPath_SpecificTypes_ShouldUseFallback()
        {
            // Arrange - String that will cause JsonException for specific types
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 4,
                Result = "not json but a plain string"
            };

            // Act - These should use fallback paths after JsonException
            var stringResult = response.GetResult<string>();
            var boolFalseResult = response.GetResult<bool>(); // "not json..." != "ok"

            // Arrange for "ok" boolean test
            response.Result = "ok";
            var boolTrueResult = response.GetResult<bool>(); // "ok" == "ok"

            // Assert
            Assert.AreEqual("not json but a plain string", stringResult);
            Assert.IsFalse(boolFalseResult);
            Assert.IsTrue(boolTrueResult);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_PropertyName_JsonExceptionToFallback()
        {
            // Arrange - Properties that will trigger JsonException then use fallback
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":5,
                "invalidForDeserialization": "plain string, not complex object",
                "validString": "just a string",
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - This should trigger JsonException then fallback to GetString()
            var stringViaFallback = response!.GetResult<string>("validString");

            // Assert
            Assert.AreEqual("just a string", stringViaFallback);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_RootPropertiesButNonDictType_ShouldSkipToResultProcessing()
        {
            // Arrange - Has additional properties but we want a non-dictionary type
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":6,
                "customProp":"value",
                "anotherProp":42,
                "result":"123"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - Request int, not dictionary, so should process Result instead
            var intResult = response!.GetResult<int>();

            // Assert - Should get 123 from Result, not from additional properties
            Assert.AreEqual(123, intResult);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_RootPropsEmptyAfterFiltering_ShouldSkipToResult()
        {
            // Arrange - Only has standard JSON-RPC fields in additional properties
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":7,
                "error":null,
                "result":"should use this"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - Request dictionary, but no custom properties, should use Result
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert - Should return empty dict because "should use this" isn't valid JSON object
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result!.Count);
        }

        #endregion

        #region Property Coverage Edge Cases

        [TestMethod]
        public void JsonRpcRequest_AllProperties_ShouldGetAndSetCorrectly()
        {
            // Arrange & Act - Test JsonRpcRequest properties
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Method = "test_method", 
                Params = new { test = "value" },
                Id = 999
            };

            // Assert
            Assert.AreEqual("2.0", request.JsonRpc);
            Assert.AreEqual("test_method", request.Method);
            Assert.IsNotNull(request.Params);
            Assert.AreEqual(999U, request.Id);
        }

        [TestMethod]
        public void JsonRpcError_AllProperties_ShouldGetAndSetCorrectly()
        {
            // Arrange & Act - Test JsonRpcError properties
            var error = new JsonRpcError
            {
                Code = -32602,
                Message = "Invalid params",
                Data = new { detail = "Parameter validation failed" }
            };

            // Assert
            Assert.AreEqual(-32602, error.Code);
            Assert.AreEqual("Invalid params", error.Message);
            Assert.IsNotNull(error.Data);
        }

        [TestMethod]
        public void JsonRpcResponse_AdditionalProperties_ManualManipulation()
        {
            // Arrange - Test direct manipulation of AdditionalProperties
            var response = new JsonRpcResponse();
            var testElement = JsonDocument.Parse("\"test value\"").RootElement;

            // Act
            response.AdditionalProperties["testProp"] = testElement;

            // Assert
            Assert.AreEqual(1, response.AdditionalProperties.Count);
            Assert.IsTrue(response.AdditionalProperties.ContainsKey("testProp"));
            Assert.AreEqual("test value", response.AdditionalProperties["testProp"].GetString());
        }

        #endregion

        #region JsonElementToObject Comprehensive Coverage

        [TestMethod]
        public void JsonRpcResponse_JsonElementToObject_AllNumberTypePaths()
        {
            // Arrange - Create response that will exercise all number conversion paths
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":8,
                "testNumbers": {
                    "smallInt": 1,
                    "maxInt": 2147483647,
                    "beyondInt": 2147483648,
                    "maxLong": 9223372036854775807,
                    "uint": 4294967295,
                    "decimal": 3.14159,
                    "scientific": 1.23e-10
                },
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - This will exercise JsonElementToObject thoroughly
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result!.ContainsKey("testNumbers"));
            
            var numbers = result["testNumbers"] as Dictionary<string, object>;
            Assert.IsNotNull(numbers);
            Assert.IsTrue(numbers!.ContainsKey("smallInt"));
            Assert.IsTrue(numbers.ContainsKey("maxInt"));
            Assert.IsTrue(numbers.ContainsKey("beyondInt"));
            Assert.IsTrue(numbers.ContainsKey("maxLong"));
            Assert.IsTrue(numbers.ContainsKey("uint"));
            Assert.IsTrue(numbers.ContainsKey("decimal"));
            Assert.IsTrue(numbers.ContainsKey("scientific"));
        }

        #endregion
    }
}
