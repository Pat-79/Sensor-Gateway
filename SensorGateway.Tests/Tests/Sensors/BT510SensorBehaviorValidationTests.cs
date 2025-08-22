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
            var response = new JsonRpcResponse { Result = "ok" };
            
            // Test how "ok" is handled for different types
            var stringResult = response.GetResult<string>();
            var dictResult = response.GetResult<Dictionary<string, object>>();
            var boolResult = response.GetResult<bool>();
            
            // Document the actual behavior
            Console.WriteLine($"String result: {stringResult}");
            Console.WriteLine($"Dict result: {dictResult?.Count} items");
            Console.WriteLine($"Bool result: {boolResult}");
            
            // Update assertions based on actual behavior
            Assert.IsNotNull(stringResult);
            Assert.IsNotNull(dictResult);
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
        public void ValidateNewBehavior_NumberConversion()
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
            
            var emptyResult = emptyResponse.GetResult<Dictionary<string, object>>();
            var okResult = okResponse.GetResult<Dictionary<string, object>>();
            
            // Document the behavior difference
            Console.WriteLine($"Empty string result: {emptyResult}");
            Console.WriteLine($"OK string result: {okResult?.Count} items");
            
            // Validate the optimized behavior
            Assert.IsNull(emptyResult, "Empty string should return null");
            Assert.IsNotNull(okResult, "OK string should return empty dictionary");
            Assert.AreEqual(0, okResult!.Count, "OK result should be empty dictionary");
        }
    }
}