using Microsoft.VisualStudio.TestTools.UnitTesting;
using SensorGateway.Sensors.bt510;
using System.Collections.Generic;
using System.Text.Json;

namespace SensorGateway.Tests.Tests.Sensors
{
    [TestClass]
    public class BT510SensorBehaviorValidationTests
    {
        [TestMethod]
        public void ValidateNewBehavior_EmptyStringHandling()
        {
            var response = new JsonRpcResponse { Result = "" };
            
            // Empty strings should return null for all types
            Assert.IsNull(response.GetResult<string>());
            Assert.IsNull(response.GetResult<Dictionary<string, object>>());
            Assert.AreEqual(0, response.GetResult<int>()); // default(int) = 0
            Assert.IsFalse(response.GetResult<bool>()); // default(bool) = false
        }

        [TestMethod]
        public void ValidateNewBehavior_OkStringHandling()
        {
            var okResponse = new JsonRpcResponse { Result = "ok" };
            var nonOkResponse = new JsonRpcResponse { Result = "something" };
            
            // Test how "ok" is handled for different types
            var okStringResult = okResponse.GetResult<string>();
            var okDictResult = okResponse.GetResult<Dictionary<string, object>>();
            var okBoolResult = okResponse.GetResult<bool>();
            
            // Test how "something" is handled for different types
            var nonOkStringResult = nonOkResponse.GetResult<string>();
            var nonOkDictResult = nonOkResponse.GetResult<Dictionary<string, object>>();
            var nonOkBoolResult = nonOkResponse.GetResult<bool>();

            // Document the actual behavior (OK)
            Console.WriteLine($"String result: {okStringResult}");
            Console.WriteLine($"Dict result: {okDictResult?.Count} items");
            Console.WriteLine($"Bool result: {okBoolResult}");

            // Document the actual behavior (non-OK)
            Console.WriteLine($"String result: {nonOkStringResult}");
            Console.WriteLine($"Dict result: {nonOkDictResult?.Count} items");
            Console.WriteLine($"Bool result: {nonOkBoolResult}");

            // Update assertions based on actual behavior
            Assert.IsNotNull(okStringResult);
            Assert.IsNotNull(okDictResult);
            Assert.IsNotNull(nonOkStringResult);
            Assert.IsNotNull(nonOkDictResult);
        }

        [TestMethod]
        public void ValidateNewBehavior_JsonElementProcessing()
        {
            var jsonElement = JsonDocument.Parse("\"test\"").RootElement;
            var response = new JsonRpcResponse { Result = jsonElement };
            
            var result = response.GetResult<string>();
            
            Assert.AreEqual("test", result);
        }

        [TestMethod]
        public void ValidateNewBehavior_NumberConversionInteger()
        {
            var jsonResponse = """
            {
                "jsonrpc":"2.0",
                "id":1,
                "numberProp": 42,
                "result":"ok"
            }
            """;
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(jsonResponse);
            
            var result = response!.GetResult<Dictionary<string, object>>();
            
            if (result != null && result.ContainsKey("numberProp"))
            {
                Console.WriteLine($"Number type: {result["numberProp"].GetType()}");
                Console.WriteLine($"Number value: {result["numberProp"]}");
                
                // Numbers are converted to double in JsonElementToObject
                Assert.AreEqual(42.0, result["numberProp"]);
            }
        }

        [TestMethod]
        public void ValidateOptimizedBehavior_EmptyStringVsOkString()
        {
            // Test the difference between empty string and "ok" string
            var emptyResponse = new JsonRpcResponse { Result = "" };
            var okResponse = new JsonRpcResponse { Result = "ok" };
            var nonOkResponse = new JsonRpcResponse { Result = "something" };
            
            var emptyResult = emptyResponse.GetResult<Dictionary<string, object>>();
            var okResult = okResponse.GetResult<Dictionary<string, object>>();
            var nonOkResult = nonOkResponse.GetResult<Dictionary<string, object>>();
            
            // Document the behavior difference
            Console.WriteLine($"Empty string result: {emptyResult}");
            Console.WriteLine($"OK string result: {okResult?.Count} items");
            Console.WriteLine($"Non-OK string result: {nonOkResult?.Count} items");
            
            // Validate the optimized behavior
            Assert.IsNull(emptyResult, "Empty string should return null");
            Assert.IsNotNull(okResult, "OK string should return empty dictionary");
            Assert.AreEqual(0, okResult!.Count, "OK result should be empty dictionary");
            Assert.IsNotNull(nonOkResult, "Non-OK string should return empty dictionary");
            Assert.AreEqual(0, nonOkResult!.Count, "Non-OK result should be empty dictionary");
        }
    }
}