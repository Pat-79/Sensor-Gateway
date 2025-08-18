using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SensorGateway.Bluetooth
{
    /// <summary>
    /// Provides memory pooling for Bluetooth operations to reduce garbage collection pressure.
    /// Uses ArrayPool for efficient byte array management in high-frequency scenarios.
    /// See also: https://code-maze.com/csharp-arraypool-memory-optimization/
    /// See also: https://medium.com/@epeshk/the-big-performance-difference-between-arraypools-in-net-b25c9fc5e31d
    /// And a github example: https://github.com/epeshk/arraypool-examples
    /// </summary>
    public static class BTMemoryPool
    {
        #region Private Fields
        
        private static readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Shared;
        private static readonly ConcurrentDictionary<int, int> _poolStatistics = new ConcurrentDictionary<int, int>();
        private static long _totalRentals = 0;
        private static long _totalReturns = 0;
        
        #endregion

        #region Public Methods

        /// <summary>
        /// Rents a byte array from the pool with at least the specified minimum length.
        /// </summary>
        /// <param name="minimumLength">The minimum length of the array needed</param>
        /// <returns>A pooled byte array that must be returned after use</returns>
        public static byte[] RentArray(int minimumLength)
        {
            if (minimumLength <= 0)
                throw new ArgumentException("Minimum length must be greater than zero", nameof(minimumLength));

            var array = _bytePool.Rent(minimumLength);
            
            // Track statistics
            Interlocked.Increment(ref _totalRentals);
            _poolStatistics.AddOrUpdate(array.Length, 1, (key, value) => value + 1);
            
            return array;
        }

        /// <summary>
        /// Returns a rented array back to the pool.
        /// </summary>
        /// <param name="array">The array to return (can be null)</param>
        /// <param name="clearArray">Whether to clear the array before returning to pool</param>
        public static void ReturnArray(byte[]? array, bool clearArray = true)
        {
            if (array == null)
                return;

            _bytePool.Return(array, clearArray);
            Interlocked.Increment(ref _totalReturns);
        }

        /// <summary>
        /// Creates a copy of data using pooled memory, useful for temporary operations.
        /// </summary>
        /// <param name="source">Source data to copy</param>
        /// <returns>A PooledMemoryHandle that automatically returns memory when disposed</returns>
        public static PooledMemoryHandle CreatePooledCopy(ReadOnlySpan<byte> source)
        {
            var pooledArray = RentArray(source.Length);
            source.CopyTo(pooledArray.AsSpan(0, source.Length));
            return new PooledMemoryHandle(pooledArray, source.Length);
        }

        /// <summary>
        /// Gets current pool usage statistics.
        /// </summary>
        /// <returns>Pool statistics including rentals, returns, and size distribution</returns>
        public static PoolStatistics GetStatistics()
        {
            return new PoolStatistics
            {
                TotalRentals = Interlocked.Read(ref _totalRentals),
                TotalReturns = Interlocked.Read(ref _totalReturns),
                OutstandingRentals = Interlocked.Read(ref _totalRentals) - Interlocked.Read(ref _totalReturns),
                SizeDistribution = new Dictionary<int, int>(_poolStatistics)
            };
        }

        #endregion

        #region Supporting Types

        /// <summary>
        /// Represents a handle to pooled memory that automatically returns the memory when disposed.
        /// </summary>
        public readonly struct PooledMemoryHandle : IDisposable
        {
            private readonly byte[] _array;
            
            /// <summary>
            /// The actual length of valid data (may be less than array length).
            /// </summary>
            public int Length { get; }
            
            /// <summary>
            /// Gets a span of the valid data portion.
            /// </summary>
            public ReadOnlySpan<byte> Span => _array.AsSpan(0, Length);
            
            /// <summary>
            /// Gets the raw array (use Length property for valid data size).
            /// </summary>
            public byte[] Array => _array;

            internal PooledMemoryHandle(byte[] array, int length)
            {
                _array = array ?? throw new ArgumentNullException(nameof(array));
                Length = length;
            }

            /// <summary>
            /// Returns the pooled memory back to the pool.
            /// </summary>
            public void Dispose()
            {
                ReturnArray(_array, clearArray: true);
            }
        }

        /// <summary>
        /// Statistics about pool usage.
        /// </summary>
        public class PoolStatistics
        {
            public long TotalRentals { get; internal set; }
            public long TotalReturns { get; internal set; }
            public long OutstandingRentals { get; internal set; }
            public Dictionary<int, int> SizeDistribution { get; internal set; } = new Dictionary<int, int>();

            public override string ToString()
            {
                return $"Pool Stats: Rentals={TotalRentals}, Returns={TotalReturns}, Outstanding={OutstandingRentals}";
            }
        }

        #endregion
    }
}
