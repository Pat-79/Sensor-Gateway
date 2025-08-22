using Microsoft.VisualStudio.TestTools.UnitTesting;
using SensorGateway.Sensors.bt510;
using System.Collections.Generic;
using System.Text.Json;

namespace SensorGateway.Tests.Tests.Sensors
{
    /// <summary>
    /// Unit tests for BT510 JSON-RPC data structures and serialization/deserialization.
    /// Tests JsonRpcRequest, JsonRpcResponse, JsonRpcError and related functionality.
    /// </summary>
    [TestClass]
    public class BT510SensorJsonTests
    {
        #region JsonRpcRequest Tests

        [TestMethod]
        public void JsonRpcRequest_DefaultValues_ShouldBeCorrect()
        {
            // Act
            var request = new JsonRpcRequest();

            // Assert
            Assert.AreEqual("2.0", request.JsonRpc);
            Assert.AreEqual("", request.Method);
            Assert.IsNull(request.Params);
            Assert.AreEqual(0u, request.Id);
        }

        [TestMethod]
        public void JsonRpcRequest_WithParameters_ShouldSerializeCorrectly()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                Method = "get",
                Params = new { property = "sensorName" },
                Id = 123
            };

            // Act
            var json = JsonSerializer.Serialize(request);
            var deserialized = JsonSerializer.Deserialize<JsonRpcRequest>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual("2.0", deserialized.JsonRpc);
            Assert.AreEqual("get", deserialized.Method);
            Assert.AreEqual(123u, deserialized.Id);
            Assert.IsNotNull(deserialized.Params);
        }

        [TestMethod]
        public void JsonRpcRequest_NullParameters_ShouldSerializeCorrectly()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                Method = "sync_time",
                Params = null,
                Id = 456
            };

            // Act
            var json = JsonSerializer.Serialize(request);
            var deserialized = JsonSerializer.Deserialize<JsonRpcRequest>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual("sync_time", deserialized.Method);
            Assert.IsNull(deserialized.Params);
            Assert.AreEqual(456u, deserialized.Id);
        }

        #endregion

        #region JsonRpcResponse Tests

        [TestMethod]
        public void JsonRpcResponse_DefaultValues_ShouldBeCorrect()
        {
            // Act
            var response = new JsonRpcResponse();

            // Assert
            Assert.AreEqual("", response.JsonRpc);
            Assert.IsNull(response.Result);
            Assert.IsNull(response.Error);
            Assert.AreEqual(0u, response.Id);
            Assert.IsNotNull(response.AdditionalProperties);
            Assert.AreEqual(0, response.AdditionalProperties.Count);
            Assert.IsFalse(response.HasError);
        }

        [TestMethod]
        public void JsonRpcResponse_WithError_ShouldHaveError()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                Error = new JsonRpcError { Code = -32600, Message = "Invalid Request" }
            };

            // Act & Assert
            Assert.IsTrue(response.HasError);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithStandardResult_ShouldReturnResult()
        {
            // Arrange
            var jsonResponse = """{"jsonrpc":"2.0","id":1,"result":"ok"}""";
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<string>();

            // Assert
            Assert.AreEqual("ok", result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithBT510RootProperties_ShouldReturnDictionary()
        {
            // Arrange - BT510 GET response with properties at root level
            var jsonResponse = """{"jsonrpc":"2.0","id":2,"mtu":244,"sensorName":"3CPO-42","result":"ok"}""";
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ContainsKey("mtu"));
            Assert.IsTrue(result.ContainsKey("sensorName"));
            Assert.AreEqual("3CPO-42", result["sensorName"].ToString());
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithOkResult_ShouldReturnEmptyDictionary()
        {
            // Arrange - BT510 SET response with just "ok"
            var jsonResponse = """{"jsonrpc":"2.0","id":3,"result":"ok"}""";
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count); // Empty dictionary for "ok" responses
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithBooleanConversion_ShouldReturnTrue()
        {
            // Arrange
            var jsonResponse = """{"jsonrpc":"2.0","id":4,"result":"ok"}""";
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<bool>();

            // Assert
            Assert.IsTrue(result); // "ok" should convert to true
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_ShouldReturnSpecificProperty()
        {
            // Arrange
            var jsonResponse = """{"jsonrpc":"2.0","id":5,"temperature":25.5,"humidity":60,"result":"ok"}""";
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var temperature = response!.GetResult<double>("temperature");
            var humidity = response.GetResult<int>("humidity");

            // Assert
            Assert.AreEqual(25.5, temperature);
            Assert.AreEqual(60, humidity);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_NonExistentProperty_ShouldReturnDefault()
        {
            // Arrange
            var jsonResponse = """{"jsonrpc":"2.0","id":6,"result":"ok"}""";
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var result = response!.GetResult<string>("nonexistent");

            // Assert
            Assert.IsNull(result); // Default value for reference type
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_StringProperty_ShouldReturnString()
        {
            // Arrange
            var jsonResponse = """{"jsonrpc":"2.0","id":7,"deviceName":"BT510-Test","result":"ok"}""";
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var deviceName = response!.GetResult<string>("deviceName");

            // Assert
            Assert.AreEqual("BT510-Test", deviceName);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithPropertyName_IntegerProperty_ShouldReturnInteger()
        {
            // Arrange
            var jsonResponse = """{"jsonrpc":"2.0","id":8,"batteryLevel":85,"result":"ok"}""";
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);

            // Act
            var batteryLevel = response!.GetResult<int>("batteryLevel");

            // Assert
            Assert.AreEqual(85, batteryLevel);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithNullResult_ShouldReturnDefault()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 9,
                Result = null
            };

            // Act
            var result = response.GetResult<string>();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void JsonRpcResponse_GetResult_WithInvalidJsonInResult_ShouldFallbackGracefully()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 10,
                Result = "invalid-json-{{"
            };

            // Act
            var result = response.GetResult<Dictionary<string, object>>();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count); // Should return empty dictionary on JSON error
        }

        #endregion

        #region JsonRpcError Tests

        [TestMethod]
        public void JsonRpcError_DefaultValues_ShouldBeCorrect()
        {
            // Act
            var error = new JsonRpcError();

            // Assert
            Assert.AreEqual(0, error.Code);
            Assert.AreEqual("", error.Message);
            Assert.IsNull(error.Data);
        }

        [TestMethod]
        public void JsonRpcError_WithValues_ShouldSerializeCorrectly()
        {
            // Arrange
            var error = new JsonRpcError
            {
                Code = -32601,
                Message = "Method not found",
                Data = new { method = "invalid_method" }
            };

            // Act
            var json = JsonSerializer.Serialize(error);
            var deserialized = JsonSerializer.Deserialize<JsonRpcError>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(-32601, deserialized.Code);
            Assert.AreEqual("Method not found", deserialized.Message);
            Assert.IsNotNull(deserialized.Data);
        }

        [TestMethod]
        public void JsonRpcError_CommonErrorCodes_ShouldBeHandled()
        {
            // Arrange & Act
            var parseError = new JsonRpcError { Code = -32700, Message = "Parse error" };
            var invalidRequest = new JsonRpcError { Code = -32600, Message = "Invalid Request" };
            var methodNotFound = new JsonRpcError { Code = -32601, Message = "Method not found" };
            var invalidParams = new JsonRpcError { Code = -32602, Message = "Invalid params" };
            var internalError = new JsonRpcError { Code = -32603, Message = "Internal error" };

            // Assert
            Assert.AreEqual(-32700, parseError.Code);
            Assert.AreEqual(-32600, invalidRequest.Code);
            Assert.AreEqual(-32601, methodNotFound.Code);
            Assert.AreEqual(-32602, invalidParams.Code);
            Assert.AreEqual(-32603, internalError.Code);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void JsonRpc_FullRoundTrip_RequestAndResponse_ShouldWork()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                Method = "get",
                Params = new Dictionary<string, object> { { "properties", new[] { "sensorName", "mtu" } } },
                Id = 100
            };

            var responseJson = """{"jsonrpc":"2.0","id":100,"mtu":244,"sensorName":"3CPO-42","result":"ok"}""";

            // Act
            var requestJson = JsonSerializer.Serialize(request);
            var requestDeserialized = JsonSerializer.Deserialize<JsonRpcRequest>(requestJson);
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);

            // Assert
            Assert.IsNotNull(requestDeserialized);
            Assert.AreEqual(request.Method, requestDeserialized.Method);
            Assert.AreEqual(request.Id, requestDeserialized.Id);

            Assert.IsNotNull(response);
            Assert.AreEqual(100u, response.Id);
            Assert.IsFalse(response.HasError);

            var properties = response.GetResult<Dictionary<string, object>>();
            Assert.IsNotNull(properties);
            Assert.IsTrue(properties.ContainsKey("sensorName"));
            Assert.IsTrue(properties.ContainsKey("mtu"));
        }

        [TestMethod]
        public void JsonRpc_ErrorResponse_ShouldBeHandled()
        {
            // Arrange
            var errorResponseJson = """{"jsonrpc":"2.0","id":101,"error":{"code":-32601,"message":"Method not found","data":{"method":"invalid_method"}}}""";

            // Act
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(errorResponseJson);

            // Assert
            Assert.IsNotNull(response);
            Assert.AreEqual(101u, response.Id);
            Assert.IsTrue(response.HasError);
            Assert.IsNotNull(response.Error);
            Assert.AreEqual(-32601, response.Error.Code);
            Assert.AreEqual("Method not found", response.Error.Message);
            Assert.IsNotNull(response.Error.Data);
        }

        #endregion
    }
}
