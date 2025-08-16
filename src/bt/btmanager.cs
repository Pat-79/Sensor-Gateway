using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace GatewaySensor.Bluetooth
{
    /// <summary>
    /// Manages Bluetooth resources through a token-based system to prevent resource contention
    /// and ensure optimal performance with limited concurrent operations.
    /// </summary>
    public sealed class BTManager : IDisposable
    {
        private static readonly Lazy<BTManager> _instance = new Lazy<BTManager>(() => new BTManager());
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _backgroundCleanupTask;
        private readonly ConcurrentBag<BTToken> _availableTokens;
        private readonly ConcurrentDictionary<uint, BTToken> _allTokens;
        private readonly SemaphoreSlim _tokenSemaphore;
        private readonly object _configLock = new object();
        private volatile bool _disposed = false;
        private uint _nextTokenId = 0;

        /// <summary>
        /// Gets the singleton instance of the BTManager
        /// </summary>
        public static BTManager Instance => _instance.Value;

        /// <summary>
        /// Gets the total number of available tokens for Bluetooth operations
        /// </summary>
        public int TotalTokens => _allTokens.Count;

        /// <summary>
        /// Gets the current number of available tokens
        /// </summary>
        public int AvailableTokens => _tokenSemaphore.CurrentCount;

        private BTManager()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _availableTokens = new ConcurrentBag<BTToken>();
            _allTokens = new ConcurrentDictionary<uint, BTToken>();
            
            // Initialize with capacity for concurrent BT operations
            var maxConcurrentOperations = Math.Max(2, Environment.ProcessorCount / 2);
            
            // Semaphore starts with full capacity - represents available operation slots
            _tokenSemaphore = new SemaphoreSlim(maxConcurrentOperations, maxConcurrentOperations);

            InitializeTokenPool(maxConcurrentOperations);

            // Start the background cleanup thread
            _backgroundCleanupTask = Task.Run(BackgroundCleanupLoop, _cancellationTokenSource.Token);
        }

        #region Token Management

        /// <summary>
        /// Acquires a token for Bluetooth operations with the specified timeout.
        /// This method blocks until a token becomes available or the timeout expires.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for a token. Default is 30 seconds.</param>
        /// <param name="cancellationToken">Cancellation token for early termination</param>
        /// <returns>A BTToken that must be returned after use</returns>
        /// <exception cref="TimeoutException">Thrown when no token becomes available within the timeout period</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the BTManager has been disposed</exception>
        /// <exception cref="InvalidOperationException">Thrown when the token pool is in an invalid state</exception>
        public async Task<BTToken> GetTokenAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var actualTimeout = timeout ?? TimeSpan.FromSeconds(30);
            
            // ALWAYS acquire semaphore first - this enforces the concurrency limit
            var acquired = await _tokenSemaphore.WaitAsync(actualTimeout, cancellationToken);
            
            if (!acquired)
            {
                throw new TimeoutException($"Failed to acquire Bluetooth token within {actualTimeout.TotalSeconds} seconds. " +
                                 $"Available tokens: {AvailableTokens}/{TotalTokens}");
            }

            try
            {
                // After semaphore acquired, get token from bag
                if (_availableTokens.TryTake(out var token))
                {
                    token.MarkAsAcquired();
                    Console.WriteLine($"🟢 BT Token {token.Id} acquired. Available: {AvailableTokens}/{TotalTokens}");
                    return token;
                }

                // This should never happen if pool is properly initialized
                throw new InvalidOperationException("Semaphore acquired but no token available - pool synchronization error");
            }
            catch
            {
                // If we fail to get a token, release the semaphore we acquired
                _tokenSemaphore.Release();
                throw;
            }
        }

        /// <summary>
        /// Synchronous version of GetTokenAsync for scenarios where async is not available
        /// </summary>
        /// <param name="timeout">Maximum time to wait for a token. Default is 30 seconds.</param>
        /// <returns>A BTToken that must be returned after use</returns>
        /// <exception cref="TimeoutException">Thrown when no token becomes available within the timeout period</exception>
        public BTToken GetToken(TimeSpan? timeout = null)
        {
            return GetTokenAsync(timeout).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Returns a token back to the pool, making it available for other operations.
        /// This method is thread-safe and should be called when Bluetooth operations are complete.
        /// </summary>
        /// <param name="token">The token to return to the pool</param>
        /// <exception cref="ArgumentNullException">Thrown when token is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when token is invalid or already returned</exception>
        public void ReturnToken(BTToken token)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token), "Token cannot be null");

            ThrowIfDisposed();

            if (!token.IsValid || token.IsReturned)
            {
                throw new InvalidOperationException($"Invalid token state. Valid: {token.IsValid}, Returned: {token.IsReturned}");
            }

            // Verify this token belongs to this manager
            if (!_allTokens.ContainsKey(token.Id))
            {
                throw new InvalidOperationException($"Token {token.Id} does not belong to this BTManager instance");
            }

            try
            {
                // Mark token as returned and add back to pool
                token.MarkAsReturned();
                _availableTokens.Add(token);
                
                // Release semaphore - makes slot available for next GetTokenAsync() call
                _tokenSemaphore.Release();
                
                Console.WriteLine($"🔄 BT Token {token.Id} returned to pool. Available: {AvailableTokens}/{TotalTokens}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error returning token {token.Id}: {ex.Message}");
                // Still release semaphore to prevent deadlock
                _tokenSemaphore.Release();
                throw;
            }
        }

        #endregion

        #region Token Pool Management

        /// <summary>
        /// Initializes the token pool with the specified capacity
        /// </summary>
        private void InitializeTokenPool(int capacity)
        {
            Console.WriteLine($"🔧 Initializing BT token pool with {capacity} tokens");
            
            for (int i = 0; i < capacity; i++)
            {
                var token = CreateToken();
                _availableTokens.Add(token);
            }
            
            Console.WriteLine($"✅ BT token pool initialized. Available: {AvailableTokens}/{TotalTokens}");
        }

        /// <summary>
        /// Creates a new token with a unique ID
        /// </summary>
        private BTToken CreateToken()
        {
            var tokenId = Interlocked.Increment(ref _nextTokenId);
            var token = new BTToken(tokenId);
            _allTokens.TryAdd(tokenId, token);
            return token;
        }

        #endregion

        #region Background Cleanup

        /// <summary>
        /// Background loop for periodic cleanup tasks and token management
        /// </summary>
        private async Task BackgroundCleanupLoop()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        await PerformCleanupTasks();
                        
                        // Wait for next cleanup cycle
                        await Task.Delay(TimeSpan.FromMinutes(1), _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error in BTManager cleanup loop: {ex.Message}");
                        await Task.Delay(TimeSpan.FromMinutes(1), _cancellationTokenSource.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("🔄 BTManager cleanup loop stopped");
            }
        }

        /// <summary>
        /// Performs periodic cleanup and maintenance tasks
        /// </summary>
        private async Task PerformCleanupTasks()
        {
            // Monitor token pool health
            var availableCount = AvailableTokens;
            var totalCount = TotalTokens;
            
            if (availableCount == 0)
            {
                Console.WriteLine($"⚠️ All BT tokens in use ({totalCount} total). Consider increasing pool size if this persists.");
            }
            
            // Check for leaked tokens (tokens acquired but never returned)
            var leakedTokens = 0;
            foreach (var token in _allTokens.Values)
            {
                if (!token.IsReturned && token.AcquiredAt.HasValue && 
                    DateTime.UtcNow - token.AcquiredAt.Value > TimeSpan.FromMinutes(5))
                {
                    leakedTokens++;
                }
            }
            
            if (leakedTokens > 0)
            {
                Console.WriteLine($"⚠️ Detected {leakedTokens} potentially leaked BT tokens");
            }

            await Task.CompletedTask; // Placeholder for additional cleanup tasks
        }

        #endregion

        #region Disposal

        /// <summary>
        /// Releases all resources used by the BTManager
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            Console.WriteLine("🔄 BTManager disposing...");

            try
            {
                _cancellationTokenSource.Cancel();
                
                // Wait for background task to complete
                if (!_backgroundCleanupTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    Console.WriteLine("⚠️ Background cleanup task did not complete within timeout");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error during BTManager disposal: {ex.Message}");
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _tokenSemaphore?.Dispose();
                _disposed = true;
                
                Console.WriteLine("✅ BTManager disposed");
            }
        }

        /// <summary>
        /// Throws ObjectDisposedException if the instance has been disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BTManager), "BTManager has been disposed");
        }

        #endregion
    }

    #region BTToken Class

    /// <summary>
    /// Represents a token for Bluetooth operations with tracking capabilities
    /// </summary>
    public sealed class BTToken : IDisposable
    {
        private volatile bool _isReturned = false;
        private volatile bool _disposed = false;

        /// <summary>
        /// Gets the unique identifier for this token
        /// </summary>
        public uint Id { get; }

        /// <summary>
        /// Gets the timestamp when this token was acquired
        /// </summary>
        public DateTime? AcquiredAt { get; private set; }

        /// <summary>
        /// Gets whether this token is valid and can be used
        /// </summary>
        public bool IsValid => !_disposed && Id > 0;

        /// <summary>
        /// Gets whether this token has been returned to the pool
        /// </summary>
        public bool IsReturned => _isReturned;

        internal BTToken(uint id)
        {
            Id = id;
        }

        /// <summary>
        /// Marks this token as acquired by a thread
        /// </summary>
        internal void MarkAsAcquired()
        {
            AcquiredAt = DateTime.UtcNow;
            _isReturned = false;
        }

        /// <summary>
        /// Marks this token as returned to the pool
        /// </summary>
        internal void MarkAsReturned()
        {
            _isReturned = true;
            AcquiredAt = null;
        }

        /// <summary>
        /// Automatically returns the token when disposed (using pattern)
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            
            if (!_isReturned && BTManager.Instance != null)
            {
                try
                {
                    BTManager.Instance.ReturnToken(this);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error auto-returning token {Id}: {ex.Message}");
                }
            }
            
            _disposed = true;
        }

        public override string ToString()
        {
            return $"BTToken({Id}, Valid: {IsValid}, Returned: {IsReturned})";
        }
    }

    #endregion
}