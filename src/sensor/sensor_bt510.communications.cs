using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SensorGateway.Configuration;
using HashtagChris.DotNetBlueZ;
using HashtagChris.DotNetBlueZ.Extensions;

namespace SensorGateway.Sensors.bt510
{
    #region BTAddress Class

    public partial class BT510Sensor
    {
        #region Constants
        const string LAIRD_CUSTOM_SERVICE_UUID = "569a1101-b87f-490c-92cb-11ba5ea5167c";
        const string LAIRD_JSONRPC_RESPONSE_CHAR_UUID = "569a2000-b87f-490c-92cb-11ba5ea5167c";
        const string LAIRD_JSONRPC_COMMAND_CHAR_UUID = "569a2001-b87f-490c-92cb-11ba5ea5167c";
        #endregion
    }
    #endregion
}