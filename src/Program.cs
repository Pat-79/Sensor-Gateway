using System;
using System.Threading.Tasks;


class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Sensor Gateway - Bluetooth Device Communication Test");

        // <-- Begin of test code
        // This code is a placeholder for the test program.
        // It dynamically loads the TestProgram class and invokes the RunTestAsync method.
        // The test code will be removed in the final application.
        var testProgramType = Type.GetType("TestProgram");
        if (testProgramType != null)
        {
            var method = testProgramType.GetMethod("RunTestAsync", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            
            if (method != null)
            {
                var result = method.Invoke(null, null);
                if (result is Task task)
                {
                    await task;
                    Console.WriteLine("Test completed successfully!");
                }
            }
        }
        // End of test code -->

        return 0;
    }
}