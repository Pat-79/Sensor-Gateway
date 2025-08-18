using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SensorGateway.Bluetooth
{
    /// <summary>
    /// Handles thread-safe buffer management for Bluetooth device data.
    /// Follows Single Responsibility Principle by focusing solely on buffer operations.
    /// </summary>
    public class BTDeviceBuffer : IDisposable
    {
        #region Private Fields
        private readonly MemoryStream _dataBuffer = new MemoryStream();
        private readonly SemaphoreSlim _bufferSemaphore = new SemaphoreSlim(1, 1);
        private bool _disposed = false;
        #endregion

        #region Properties

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
        public async Task AppendToBufferAsync(byte[] data)
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

        #region IDisposable Implementation

        /// <summary>
        /// Releases all resources used by the BTDeviceBuffer.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the BTDeviceBuffer and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _bufferSemaphore?.Dispose();
                _dataBuffer?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
