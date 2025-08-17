using System;
using SensorGateway.Configuration;


class Program
{
    static void Main(string[] args)
    {
        //AppConfig.Initialize();
        Console.WriteLine(AppConfig.Bluetooth.AdapterName);

        return; // Exit early for now, as this is a placeholder for the main program logic
    }
}
