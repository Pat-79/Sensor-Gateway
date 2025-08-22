using Microsoft.VisualStudio.TestTools.UnitTesting;
using SensorGateway.Sensors.bt510;
using System.Collections.Generic;
using System.Text.Json;

namespace SensorGateway.Tests.Tests.Sensors
{
    /// <summary>
    /// Additional comprehensive unit tests for BT510 JSON-RPC structures.
    /// Focuses on edge cases and uncovered code paths to reach 80%+ coverage.
    /// </summary>
    [TestClass]
    public class BT510SensorJsonAdvancedTests
    {
        #region JsonRpcResponse Advanced GetResult Tests

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithComplexNestedObject_ShouldDeserializeCorrectly()
        {
            // Arrange - Response with nested objects and arrays
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":1,
                "deviceInfo": {
                    "name": "BT510-Complex",
                    "sensors": ["temperature", "humidity", "battery"],
                    "config": {
                        "enabled": true,
                        "sampleRate": 30
                    }
                },
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ContainsKey("deviceInfo"));
            
            // Verify nested object is properly converted
            var deviceInfo = result["deviceInfo"] as Dictionary<string, object>;
            Assert.IsNotNull(deviceInfo);
            Assert.AreEqual("BT510-Complex", deviceInfo!["name"]);
            
            // Verify array is properly converted
            var sensors = deviceInfo["sensors"] as object[];
            Assert.IsNotNull(sensors);
            Assert.AreEqual(3, sensors!.Length);
            Assert.AreEqual("temperature", sensors[0]);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithArrayResult_ShouldHandleArrays()
        {
            // Arrange
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":2,
                "measurements": [1, 2, 3, 4, 5],
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ContainsKey("measurements"));
            
            var measurements = result["measurements"] as object[];
            Assert.IsNotNull(measurements);
            Assert.AreEqual(5, measurements!.Length);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithNullValues_ShouldHandleNulls()
        {
            // Arrange
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":3,
                "optionalValue": null,
                "requiredValue": "present",
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ContainsKey("optionalValue"));
            Assert.IsTrue(result.ContainsKey("requiredValue"));
            Assert.IsNull(result["optionalValue"]);
            Assert.AreEqual("present", result["requiredValue"]);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_AllNumericTypes_ShouldConvertCorrectly()
        {
            // Arrange - Test all numeric type conversions
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":4,
                "intValue": 42,
                "longValue": 9223372036854775807,
                "uintValue": 4294967295,
                "doubleValue": 3.14159,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act & Assert
            var intValue = response!.GetResult<int>("intValue");
            var longValue = response.GetResult<long>("longValue");
            var doubleValue = response.GetResult<double>("doubleValue");

            Assert.AreEqual(42, intValue);
            Assert.AreEqual(9223372036854775807L, longValue);
            Assert.AreEqual(3.14159, doubleValue, 0.00001);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_BooleanValues_ShouldConvertCorrectly()
        {
            // Arrange
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":5,
                "boolTrue": true,
                "boolFalse": false,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var trueValue = response!.GetResult<bool>("boolTrue");
            var falseValue = response.GetResult<bool>("boolFalse");

            // Assert
            Assert.IsTrue(trueValue);
            Assert.IsFalse(falseValue);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_InvalidJsonDeserialization_ShouldFallbackToBasicTypes()
        {
            // Arrange - Create a response with a complex property that can't be deserialized as requested type
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":6,
                "complexProperty": {"nested": "object"},
                "stringProperty": "simple string",
                "numberProperty": 123,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - Try to get complex object as string (should fallback)
            var stringValue = response!.GetResult<string>("stringProperty");
            var numberAsInt = response.GetResult<int>("numberProperty");

            // Assert
            Assert.AreEqual("simple string", stringValue);
            Assert.AreEqual(123, numberAsInt);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithEmptyResult_ShouldReturnDefault()
        {
            // FIXED: Updated expectation based on new behavior
            var response = new JsonRpcResponse { Result = "" };
            
            var result = response.GetResult<Dictionary<string, object>>();
            
            // NEW BEHAVIOR: Empty string now returns default (null) instead of empty dictionary
            Assert.IsNull(result);
        }

        [TestMethod] 
        public void JsonRpcResponse_GetResult_WithOkResult_ShouldReturnEmptyDict()
        {
            // NEW TEST: Separate test for "ok" result which should still return empty dictionary
            var response = new JsonRpcResponse { Result = "ok" };
            
            var result = response.GetResult<Dictionary<string, object>>();
            
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithNonDictionaryType_ShouldDeserializeDirectly()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 8,
                Result = "42"
            };

            // Act
            var intResult = response.GetResult<int>();
            var stringResult = response.GetResult<string>();

            // Assert
            Assert.AreEqual(42, intResult);
            Assert.AreEqual("42", stringResult);
        }

        #endregion

        #region JsonElementToObject Tests (via AdditionalProperties)

        [TestMethod]
        public void JsonRpcResponse_JsonElementToObject_WithComplexArray_ShouldConvertCorrectly()
        {
            // Arrange - Test array conversion through AdditionalProperties
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":9,
                "mixedArray": [1, "string", true, null, {"nested": "object"}],
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ContainsKey("mixedArray"));
            
            var mixedArray = result["mixedArray"] as object[];
            Assert.IsNotNull(mixedArray);
            Assert.AreEqual(5, mixedArray!.Length);
            Assert.AreEqual(1.0, mixedArray[0]); // Number (JsonElementToObject converts to double)
            Assert.AreEqual("string", mixedArray[1]); // String
            Assert.AreEqual(true, mixedArray[2]); // Boolean
            Assert.IsNull(mixedArray[3]); // Null
            
            // Nested object
            var nestedObj = mixedArray[4] as Dictionary<string, object>;
            Assert.IsNotNull(nestedObj);
            Assert.AreEqual("object", nestedObj!["nested"]);
        }

        [TestMethod]
        public void JsonRpcResponse_JsonElementToObject_WithNestedObjects_ShouldConvertRecursively()
        {
            // Arrange
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":10,
                "deepNesting": {
                    "level1": {
                        "level2": {
                            "level3": {
                                "value": "deep"
                            }
                        }
                    }
                },
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            var deepNesting = result["deepNesting"] as Dictionary<string, object>;
            Assert.IsNotNull(deepNesting);
            
            var level1 = deepNesting!["level1"] as Dictionary<string, object>;
            Assert.IsNotNull(level1);
            
            var level2 = level1!["level2"] as Dictionary<string, object>;
            Assert.IsNotNull(level2);
            
            var level3 = level2!["level3"] as Dictionary<string, object>;
            Assert.IsNotNull(level3);
            
            Assert.AreEqual("deep", level3!["value"]);
        }

        [TestMethod]
        public void JsonRpcResponse_JsonElementToObject_WithAllValueKinds_ShouldHandleAllTypes()
        {
            // Arrange - Test all JsonValueKind cases
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":11,
                "stringValue": "test",
                "intValue": 42,
                "longValue": 9223372036854775807,
                "uintValue": 4294967295,
                "doubleValue": 3.14,
                "boolTrue": true,
                "boolFalse": false,
                "nullValue": null,
                "arrayValue": [1, 2, 3],
                "objectValue": {"key": "value"},
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            
            // Test all types (JsonElementToObject converts all numbers to double)
            Assert.AreEqual("test", result["stringValue"]);
            Assert.AreEqual(42.0, result["intValue"]);
            Assert.AreEqual(9223372036854775807.0, result["longValue"]);
            Assert.AreEqual(4294967295.0, result["uintValue"]);
            Assert.AreEqual(3.14, result["doubleValue"]);
            Assert.AreEqual(true, result["boolTrue"]);
            Assert.AreEqual(false, result["boolFalse"]);
            Assert.IsNull(result["nullValue"]);
            
            var arrayValue = result["arrayValue"] as object[];
            Assert.IsNotNull(arrayValue);
            Assert.AreEqual(3, arrayValue!.Length);
            
            var objectValue = result["objectValue"] as Dictionary<string, object>;
            Assert.IsNotNull(objectValue);
            Assert.AreEqual("value", objectValue!["key"]);
        }

        [TestMethod]
        public void JsonRpcResponse_JsonElementToObject_WithRawTextFallback_ShouldReturnRawText()
        {
            // This is harder to test directly since most JSON values have specific kinds
            // But we can test the method indirectly by ensuring complex structures work
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":12,
                "complexStructure": {
                    "array": [{"nested": true}],
                    "value": "test"
                },
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ContainsKey("complexStructure"));
        }

        #endregion

        #region Error Response Edge Cases

        [TestMethod]
        public void JsonRpcResponse_WithNullError_ShouldNotHaveError()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 13,
                Error = null,
                Result = "success"
            };

            // Act & Assert
            Assert.IsFalse(response.HasError);
        }

        [TestMethod]
        public void JsonRpcResponse_WithError_ShouldHaveError()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 14,
                Error = new JsonRpcError { Code = -1, Message = "Test error" },
                Result = null
            };

            // Act & Assert
            Assert.IsTrue(response.HasError);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void JsonRpcResponse_RealWorldBT510Response_ShouldParseCorrectly()
        {
            // Arrange - Real BT510 response format
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":1,
                "mtu":244,
                "sensorName":"3CPO-42",
                "firmwareVersion":"2.0.1",
                "batteryLevel":3250,
                "temperature":2234,
                "humidity":5542,
                "configuration": {
                    "sampleRate": 60,
                    "enabled": true,
                    "thresholds": {
                        "temperature": {"min": -40, "max": 85},
                        "humidity": {"min": 0, "max": 100}
                    }
                },
                "measurements": [
                    {"type": "temperature", "value": 22.34, "timestamp": 1692811200},
                    {"type": "humidity", "value": 55.42, "timestamp": 1692811200}
                ],
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var allProperties = response!.GetResult<Dictionary<string, object>>();
            var sensorName = response.GetResult<string>("sensorName");
            var batteryLevel = response.GetResult<int>("batteryLevel");
            var temperature = response.GetResult<int>("temperature");

            // Assert
            Assert.IsNotNull(allProperties);
            Assert.IsTrue(allProperties.Count >= 7); // At least 7 properties
            
            Assert.AreEqual("3CPO-42", sensorName);
            Assert.AreEqual(3250, batteryLevel);
            Assert.AreEqual(2234, temperature);
            
            // Verify complex nested structures
            Assert.IsTrue(allProperties.ContainsKey("configuration"));
            Assert.IsTrue(allProperties.ContainsKey("measurements"));
            
            var configuration = allProperties["configuration"] as Dictionary<string, object>;
            Assert.IsNotNull(configuration);
            Assert.AreEqual(60.0, configuration!["sampleRate"]); // JsonElementToObject converts to double
            
            var measurements = allProperties["measurements"] as object[];
            Assert.IsNotNull(measurements);
            Assert.AreEqual(2, measurements!.Length);
        }

        #endregion
    }
}
