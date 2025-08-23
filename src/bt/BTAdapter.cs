using System;
using System.Threading;
using System.Threading.Tasks;
using SensorGateway.Configuration;
using HashtagChris.DotNetBlueZ;

namespace SensorGateway.Bluetooth
{
    /// <summary>
    /// Thread-safe singleton wrapper for Bluetooth adapter management.
    /// Ensures the adapter is initialized only once and provides shared access across the application.
    /// </summary>
    public sealed class BTAdapter : IDisposable
    {
        #region Singleton Implementation
        private static readonly Lazy<BTAdapter> _instance = new Lazy<BTAdapter>(() => new BTAdapter(), LazyThreadSafetyMode.ExecutionAndPublication);
        
        /// <summary>
        /// Gets the singleton instance of BTAdapter.
        /// </summary>
        public static BTAdapter Instance => _instance.Value;
        #endregion

        #region Private Fields
        private Adapter? _adapter;
        private readonly SemaphoreSlim _initializationSemaphore = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;
        private bool _disposed = false;
        #endregion

        #region Properties
        /// <summary>
        /// Gets a value indicating whether the adapter has been successfully initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized && _adapter != null;
        #endregion

        #region Constructor
        /// <summary>
        /// Private constructor to enforce singleton pattern.
        /// </summary>
        private BTAdapter()
        {

        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Asynchronously initializes the Bluetooth adapter if not already initialized.
        /// This method is thread-safe and ensures the adapter is initialized only once.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A task that represents the asynchronous initialization operation.
        /// The task result is the initialized adapter instance.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the specified adapter is not found or fails to power on within the timeout period.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the BTAdapter instance has been disposed.
        /// </exception>
        public async Task<Adapter> GetAdapterAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Fast path: if already initialized, return immediately
            if (_isInitialized && _adapter != null)
            {
                return _adapter;
            }

            // Slow path: initialize with thread safety
            await _initializationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Double-checked locking pattern: check again after acquiring the lock
                if (_isInitialized && _adapter != null)
                {
                    return _adapter;
                }

                await InitializeAdapterInternalAsync(cancellationToken).ConfigureAwait(false);
                return _adapter ?? throw new InvalidOperationException("Adapter initialization failed.");
            }
            finally
            {
                _initializationSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets the adapter synchronously. This method blocks until the adapter is initialized.
        /// </summary>
        /// <returns>The initialized adapter instance.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the specified adapter is not found or fails to power on within the timeout period.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the BTAdapter instance has been disposed.
        /// </exception>
        public Adapter GetAdapter()
        {
            return GetAdapterAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Resets the adapter state, forcing reinitialization on the next call to GetAdapterAsync.
        /// This method is useful for error recovery scenarios.
        /// </summary>
        /// <returns>A task that represents the asynchronous reset operation.</returns>
        public async Task ResetAsync()
        {
            ThrowIfDisposed();

            await _initializationSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                _adapter = null;
                _isInitialized = false;
            }
            finally
            {
                _initializationSemaphore.Release();
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Internal method that performs the actual adapter initialization.
        /// This method is called within the semaphore lock to ensure thread safety.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous initialization operation.</returns>
        private async Task InitializeAdapterInternalAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Get the adapter using BlueZ directly (not through the singleton method)
                _adapter = await BlueZManager.GetAdapterAsync(AppConfig.Bluetooth.AdapterName).ConfigureAwait(false);
                if (_adapter == null)
                {
                    throw new InvalidOperationException($"Bluetooth adapter '{AppConfig.Bluetooth.AdapterName}' not found.");
                }

                // Ensure the adapter is powered on
                await EnsureAdapterPoweredAsync(cancellationToken).ConfigureAwait(false);

                _isInitialized = true;
            }
            catch
            {
                // Reset state on failure
                _adapter = null;
                _isInitialized = false;
                throw;
            }
        }

        /// <summary>
        /// Ensures the Bluetooth adapter is powered on within the configured timeout period.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous power-on operation.</returns>
        private async Task EnsureAdapterPoweredAsync(CancellationToken cancellationToken)
        {
            if (_adapter == null)
                throw new InvalidOperationException("Adapter is not initialized.");

            // Check and set power state if needed
            if (!await _adapter.GetPoweredAsync().ConfigureAwait(false))
            {
                await _adapter.SetPoweredAsync(true).ConfigureAwait(false);

                // Poll for power state with timeout
                var timeout = TimeSpan.FromSeconds(BTDeviceConstants.ADAPTER_POWER_TIMEOUT_SECONDS);
                var start = DateTime.UtcNow;

                while (!await _adapter.GetPoweredAsync().ConfigureAwait(false) && 
                       DateTime.UtcNow - start < timeout &&
                       !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(BTDeviceConstants.WAIT_LOOP_DELAY, cancellationToken).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!await _adapter.GetPoweredAsync().ConfigureAwait(false))
                {
                    throw new InvalidOperationException($"Failed to power on adapter '{AppConfig.Bluetooth.AdapterName}' within timeout.");
                }
            }
        }

        /// <summary>
        /// Throws an ObjectDisposedException if the instance has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BTAdapter));
            }
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Disposes of the BTAdapter resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _initializationSemaphore?.Dispose();
                _adapter = null;
                _isInitialized = false;
                _disposed = true;
            }
        }
        #endregion
    }
}