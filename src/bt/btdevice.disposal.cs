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
        #region Resource Management

        /// <summary>
        /// Disposes communication resources including event handlers, semaphores, and buffers.
        /// This method is called from the main Dispose method to clean up communication-specific resources.
        /// </summary>
        private void DisposeCommunications()
        {
            if (_responseChar != null)
            {
                _responseChar.Value -= ReceiveNotificationData;
            }

            _bufferSemaphore?.Dispose();
            _dataBuffer?.Dispose();
            _notificationReceived?.Dispose();
        }

        #endregion
    }
}