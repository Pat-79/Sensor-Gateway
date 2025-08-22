using Microsoft.VisualStudio.TestTools.UnitTesting;
using SensorGateway.Sensors.bt510;
using System.Text.Json;

namespace SensorGateway.Tests.Tests.Sensors
{
    /// <summary>
    /// Edge case and error handling tests for JsonRpcResponse to achieve 80%+ coverage
    /// </summary>
    [TestClass]
    public class BT510SensorJsonEdgeCaseTests
    {
        #region GetResult Fallback Scenarios

        [TestMethod]
        public void JsonRpcResponse_GetResult_JsonException_ShouldFallbackToStringConversion()
        {
            // Arrange - Response with Result that can't be deserialized as complex type but works as string
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 1,
                Result = "simple string result"
            };

            // Act - Try to get as complex type, should fallback to string conversion
            var result = response.GetResult<string>();

            // Assert
            Assert.AreEqual("simple string result", result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_JsonException_BooleanFallback_ShouldReturnTrue()
        {
            // Arrange - Response with "ok" result
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 2,
                Result = "ok"
            };

            // Act - Try to get as boolean, should fallback to (result == "ok")
            var result = response.GetResult<bool>();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_JsonException_BooleanFallback_ShouldReturnFalse()
        {
            // Arrange - Response with non-"ok" result
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 3,
                Result = "error"
            };

            // Act - Try to get as boolean, should fallback to (result == "ok")
            var result = response.GetResult<bool>();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_JsonException_StringFallback()
        {
            // Arrange - Create response with property that will cause JsonException when deserializing as int
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":4,
                "stringProperty": "not a number",
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - Try to get string property as string (should use fallback)
            var result = response!.GetResult<string>("stringProperty");

            // Assert
            Assert.AreEqual("not a number", result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_IntegerFallback()
        {
            // Arrange - Property that can be parsed as int
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":5,
                "intProperty": 42,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - Get as int through fallback mechanism
            var result = response!.GetResult<int>("intProperty");

            // Assert
            Assert.AreEqual(42, result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_LongFallback()
        {
            // Arrange
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":6,
                "longProperty": 9223372036854775807,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<long>("longProperty");

            // Assert
            Assert.AreEqual(9223372036854775807L, result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_DoubleFallback()
        {
            // Arrange
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":7,
                "doubleProperty": 3.14159,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<double>("doubleProperty");

            // Assert
            Assert.AreEqual(3.14159, result, 0.00001);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_BooleanFallback_True()
        {
            // Arrange
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":8,
                "boolProperty": true,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<bool>("boolProperty");

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_BooleanFallback_False()
        {
            // Arrange
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":9,
                "boolProperty": false,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<bool>("boolProperty");

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region Null and Default Value Tests

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithNullResult_ShouldReturnDefault()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 10,
                Result = null
            };

            // Act
            var result = response.GetResult<string>();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_PropertyNotFound_ShouldReturnDefault()
        {
            // Arrange
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":11,
                "existingProperty": "value",
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<string>("nonExistentProperty");

            // Assert
            Assert.IsNull(result); // Default for string
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_PropertyNotFound_IntDefault()
        {
            // Arrange
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":12,
                "existingProperty": "value",
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<int>("nonExistentProperty");

            // Assert
            Assert.AreEqual(0, result); // Default for int
        }

        #endregion

        #region Invalid Type Conversion Tests

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_InvalidBoolConversion_ShouldReturnDefault()
        {
            // Arrange - Property that uses boolean fallback logic
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":14,
                "nullProperty": null,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act - Try to get null as boolean (should use ValueKind logic)
            var result = response!.GetResult<bool>("nullProperty");

            // Assert
            Assert.IsFalse(result); // null is not JsonValueKind.True
        }

        #endregion

        #region Coverage for Unused JsonRpcError Properties

        [TestMethod]
        public void JsonRpcError_Properties_ShouldWork()
        {
            // Arrange
            var error = new JsonRpcError
            {
                Code = -32601,
                Message = "Method not found",
                Data = "Additional error data"
            };

            // Act & Assert
            Assert.AreEqual(-32601, error.Code);
            Assert.AreEqual("Method not found", error.Message);
            Assert.AreEqual("Additional error data", error.Data);
        }

        [TestMethod]
        public void JsonRpcRequest_Properties_ShouldWork()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = 123,
                Method = "test_method",
                Params = new { param1 = "value1", param2 = 42 }
            };

            // Act & Assert
            Assert.AreEqual("2.0", request.JsonRpc);
            Assert.AreEqual(123U, request.Id); // Id property is object? but gets stored as uint
            Assert.AreEqual("test_method", request.Method);
            Assert.IsNotNull(request.Params);
        }

        #endregion
    }
}
