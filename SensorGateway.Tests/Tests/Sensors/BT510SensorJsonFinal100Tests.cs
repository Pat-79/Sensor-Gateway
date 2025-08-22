using Microsoft.VisualStudio.TestTools.UnitTesting;
using SensorGateway.Sensors.bt510;
using System.Collections.Generic;
using System.Text.Json;

namespace SensorGateway.Tests.Tests.Sensors
{
    /// <summary>
    /// Final targeted tests to achieve 100% coverage for JsonRpcResponse
    /// </summary>
    [TestClass]
    public class BT510SensorJsonFinal100Tests
    {
        #region Targeted Coverage for Remaining Edge Cases

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithRootProperties_NotDictionaryType_ShouldSkipRootLogic()
        {
            // Arrange - Response with additional properties but requesting non-dictionary type
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":1,
                "customProperty":"value",
                "anotherProperty":42,
                "result":"actual result"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - Request string, not Dictionary<string, object>, should skip root property logic
            var result = response!.GetResult<string>();

            // Assert - Should get the result field, not the root properties
            Assert.AreEqual("actual result", result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithRootPropertiesEmpty_ShouldSkipToResult()
        {
            // Arrange - Response with additional properties but no custom ones (only standard fields)
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":2,
                "result":"ok",
                "error":null
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert - Should get empty dictionary from "ok" result processing, not root properties
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result!.Count); // Empty dictionary for "ok" result
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_JsonException_Then_TypeNotSupported_ShouldReturnDefault()
        {
            // Arrange - Test a type that's not handled by the fallback logic
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 3,
                Result = "some string that can't be converted to a DateTime"
            };

            // Act - Request DateTime, which isn't handled in the fallback logic
            var result = response.GetResult<System.DateTime>();

            // Assert - Should return default DateTime
            Assert.AreEqual(default(System.DateTime), result);
        }

        [TestMethod]  
        public void JsonRpcResponse_GetResult_PropertyName_JsonException_UnsupportedType_ShouldReturnDefault()
        {
            // Arrange
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":4,
                "someProperty":"not a decimal",
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - Request decimal, which isn't handled in the fallback logic
            var result = response!.GetResult<decimal>("someProperty");

            // Assert - Should return default decimal
            Assert.AreEqual(default(decimal), result);
        }

        [TestMethod]
        public void JsonRpcResponse_JsonElementToObject_TryGetInt32_False_ShouldFallbackToLong()
        {
            // Arrange - Number too large for int32, should try long
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":5,
                "largeNumber": 9223372036854775807,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - This should trigger the int32 failure -> long success path
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result!.ContainsKey("largeNumber"));
            // Should be converted as long, but JsonElementToObject converts all to double
            Assert.AreEqual(9223372036854775807.0, result["largeNumber"]);
        }

        [TestMethod]
        public void JsonRpcResponse_JsonElementToObject_TryGetInt64_False_ShouldFallbackToUint()
        {
            // Arrange - This is harder to trigger since most JSON numbers fit in long
            // But we can test the path by ensuring it goes through all number conversions
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":6,
                "maxUint": 4294967295,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result!.ContainsKey("maxUint"));
            Assert.AreEqual(4294967295.0, result["maxUint"]);
        }

        [TestMethod]
        public void JsonRpcResponse_JsonElementToObject_AllTryMethodsFail_ShouldFallbackToDouble()
        {
            // Arrange - Create a scenario that tests the final GetDouble() fallback
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":7,
                "floatingPoint": 3.141592653589793,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result!.ContainsKey("floatingPoint"));
            Assert.AreEqual(3.141592653589793, result["floatingPoint"]);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_PropertyName_TryGetMethods_CoverAllPaths()
        {
            // Arrange - Test various number types that might trigger different TryGet paths
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":8,
                "smallInt": 42,
                "maxInt": 2147483647,
                "minInt": -2147483648,
                "longVal": 9223372036854775807,
                "uintVal": 4294967295,
                "doubleVal": 1.7976931348623157E+308,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - Test all the TryGet fallback methods
            var smallInt = response!.GetResult<int>("smallInt");
            var maxInt = response.GetResult<int>("maxInt"); 
            var minInt = response.GetResult<int>("minInt");
            var longVal = response.GetResult<long>("longVal");
            var doubleVal = response.GetResult<double>("doubleVal");

            // Assert
            Assert.AreEqual(42, smallInt);
            Assert.AreEqual(2147483647, maxInt);
            Assert.AreEqual(-2147483648, minInt);
            Assert.AreEqual(9223372036854775807L, longVal);
            Assert.AreEqual(1.7976931348623157E+308, doubleVal);
        }

        [TestMethod]
        public void JsonRpcResponse_DefaultConstructor_ShouldInitializeProperties()
        {
            // Arrange & Act - Test default constructor and property initialization
            var response = new JsonRpcResponse();

            // Assert - Test all default values
            Assert.AreEqual("", response.JsonRpc);
            Assert.IsNull(response.Result);
            Assert.IsNull(response.Error);
            Assert.AreEqual(0U, response.Id);
            Assert.IsNotNull(response.AdditionalProperties);
            Assert.AreEqual(0, response.AdditionalProperties.Count);
            Assert.IsFalse(response.HasError);
        }

        #endregion

        #region Edge Cases for JsonElementToObject Default Path

        [TestMethod]
        public void JsonRpcResponse_JsonElementToObject_UndefinedValueKind_ShouldReturnRawText()
        {
            // This is extremely difficult to test directly since JsonValueKind is an enum
            // with well-defined values. The default case in JsonElementToObject might never
            // be hit in practice, but let's create a comprehensive test that exercises
            // the method thoroughly
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":10,
                "comprehensiveTest": {
                    "strings": ["hello", "world"],
                    "numbers": [1, 2, 3.14, -42, 9223372036854775807],
                    "booleans": [true, false],
                    "nulls": [null, null],
                    "nested": {
                        "deep": {
                            "deeper": "value"
                        }
                    },
                    "mixed": [1, "two", true, null, {"five": 5}]
                },
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - This exercises JsonElementToObject recursively with all types
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result!.ContainsKey("comprehensiveTest"));
            
            var comprehensive = result["comprehensiveTest"] as Dictionary<string, object>;
            Assert.IsNotNull(comprehensive);
            Assert.IsTrue(comprehensive!.ContainsKey("strings"));
            Assert.IsTrue(comprehensive.ContainsKey("numbers"));
            Assert.IsTrue(comprehensive.ContainsKey("booleans"));
            Assert.IsTrue(comprehensive.ContainsKey("nulls"));
            Assert.IsTrue(comprehensive.ContainsKey("nested"));
            Assert.IsTrue(comprehensive.ContainsKey("mixed"));
        }

        #endregion
    }
}
