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
        #region Private Buffer Fields
        private readonly MemoryStream _dataBuffer = new MemoryStream();
        private readonly SemaphoreSlim _bufferSemaphore = new SemaphoreSlim(1, 1);
        #endregion

        #region Buffer Management Properties

        /// <summary>
        /// Gets the current size of the data buffer in bytes.
        /// </summary>
        /// <value>The current buffer size in bytes.</value>
        public long BufferSize
        {
            get
            {
                return GetBufferSizeAsync().GetAwaiter().GetResult();
            }
        }

        #endregion

        #region Buffer Management Methods

        /// <summary>
        /// Asynchronously retrieves the current buffer contents as a byte array.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// a copy of the current buffer data as a byte array.
        /// </returns>
        public async Task<byte[]> GetBufferDataAsync()
        {
            await _bufferSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                return _dataBuffer.ToArray();
            }
            finally
            {
                _bufferSemaphore.Release();
            }
        }

        /// <summary>
        /// Synchronously retrieves the current buffer contents as a byte array.
        /// </summary>
        /// <returns>A copy of the current buffer data as a byte array.</returns>
        public byte[] GetBufferData()
        {
            return GetBufferDataAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously gets the current size of the data buffer in bytes.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// the current buffer size in bytes.
        /// </returns>
        public async Task<long> GetBufferSizeAsync()
        {
            await _bufferSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                return _dataBuffer.Length;
            }
            finally
            {
                _bufferSemaphore.Release();
            }
        }

        /// <summary>
        /// Asynchronously clears all data from the internal buffer and resets its position.
        /// This method provides thread-safe buffer cleanup functionality.
        /// </summary>
        /// <returns>A task that represents the asynchronous buffer clearing operation.</returns>
        public async Task ClearBufferAsync()
        {
            await _bufferSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                _dataBuffer.SetLength(0);
                _dataBuffer.Position = 0;
            }
            finally
            {
                _bufferSemaphore.Release();
            }
        }

        /// <summary>
        /// Synchronously clears all data from the internal buffer.
        /// </summary>
        public void ClearBuffer()
        {
            ClearBufferAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Efficiently appends byte data to the internal buffer in a thread-safe manner.
        /// </summary>
        /// <param name="data">The byte array to append to the buffer. Null or empty arrays are ignored.</param>
        /// <returns>A task that represents the asynchronous append operation.</returns>
        private async Task AppendToBufferAsync(byte[] data)
        {
            if (data?.Length > 0)
            {
                await _bufferSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    await _dataBuffer.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                }
                finally
                {
                    _bufferSemaphore.Release();
                }
            }
        }

        #endregion
    }
}