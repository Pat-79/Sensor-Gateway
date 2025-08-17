using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensorGateway.Configuration;
using HashtagChris.DotNetBlueZ;
using HashtagChris.DotNetBlueZ.Extensions;

namespace SensorGateway.Bluetooth
{
    public partial class BTDevice
    {
        #region Constants
        const int WAIT_LOOP_DELAY = 100;
        const int ADAPTER_POWER_TIMEOUT_SECONDS = 5;
        const int MAX_CONNECTION_ATTEMPTS = 3;
        const int CONNECTION_STABILIZATION_DELAY = 2000;
        const int CONNECTION_RETRY_DELAY = 1000;
        const int TOKEN_TIMEOUT_SECONDS = 120;
        const int NOTIFICATION_WAIT_TIMEOUT_SECONDS = 30;
        #endregion
    }
}