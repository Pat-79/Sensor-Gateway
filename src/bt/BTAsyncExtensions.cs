using System;
using System.Threading;
using System.Threading.Tasks;

namespace SensorGateway.Bluetooth
{
    /// <summary>
    /// Provides enhanced async utilities for Bluetooth operations with proper cancellation support.
    /// </summary>
    public static class BTAsyncExtensions
    {
        /// <summary>
        /// Polls a condition with exponential backoff and cancellation support.
        /// </summary>
        /// <param name="condition">The condition to poll</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="initialDelay">Initial delay between polls</param>
        /// <param name="maxDelay">Maximum delay between polls</param>
        /// <param name="backoffMultiplier">Multiplier for exponential backoff</param>
        /// <returns>True if condition became true, false if timed out</returns>
        public static async Task<bool> PollConditionAsync(
            Func<Task<bool>> condition,
            TimeSpan timeout,
            CancellationToken cancellationToken = default,
            TimeSpan? initialDelay = null,
            TimeSpan? maxDelay = null,
            double backoffMultiplier = 1.5)
        {
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            var currentDelay = initialDelay ?? TimeSpan.FromMilliseconds(100);
            var maximumDelay = maxDelay ?? TimeSpan.FromSeconds(5);
            var deadline = DateTime.UtcNow.Add(timeout);

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await condition().ConfigureAwait(false))
                    return true;

                // Calculate next delay with exponential backoff
                var nextDelay = TimeSpan.FromMilliseconds(currentDelay.TotalMilliseconds * backoffMultiplier);
                currentDelay = nextDelay > maximumDelay ? maximumDelay : nextDelay;

                // Don't wait longer than remaining time
                var remainingTime = deadline - DateTime.UtcNow;
                var actualDelay = currentDelay > remainingTime ? remainingTime : currentDelay;

                if (actualDelay > TimeSpan.Zero)
                {
                    await Task.Delay(actualDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            return false;
        }

        /// <summary>
        /// Executes an operation with retry logic and exponential backoff.
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operation">The operation to execute</param>
        /// <param name="maxAttempts">Maximum number of attempts</param>
        /// <param name="baseDelay">Base delay between retries</param>
        /// <param name="maxDelay">Maximum delay between retries</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="shouldRetry">Function to determine if an exception should trigger a retry</param>
        /// <returns>The result of the operation</returns>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxAttempts = 3,
            TimeSpan? baseDelay = null,
            TimeSpan? maxDelay = null,
            CancellationToken cancellationToken = default,
            Func<Exception, bool>? shouldRetry = null)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (maxAttempts <= 0)
                throw new ArgumentException("Max attempts must be greater than zero", nameof(maxAttempts));

            var delay = baseDelay ?? TimeSpan.FromSeconds(1);
            var maximumDelay = maxDelay ?? TimeSpan.FromSeconds(30);
            shouldRetry ??= _ => true;

            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < maxAttempts && shouldRetry(ex))
                {
                    lastException = ex;

                    // Calculate delay for next attempt
                    var nextDelay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    var actualDelay = nextDelay > maximumDelay ? maximumDelay : nextDelay;

                    await Task.Delay(actualDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            // Re-throw the last exception if all attempts failed
            throw lastException ?? new InvalidOperationException("Operation failed after all retry attempts");
        }

        /// <summary>
        /// Creates a timeout-aware task that cancels after the specified duration.
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="task">The task to execute</param>
        /// <param name="timeout">Timeout duration</param>
        /// <param name="cancellationToken">Additional cancellation token</param>
        /// <returns>The result of the task</returns>
        /// <exception cref="TimeoutException">Thrown when the operation times out</exception>
        public static async Task<T> WithTimeoutAsync<T>(
            this Task<T> task,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                return await task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Operation timed out after {timeout.TotalSeconds} seconds");
            }
        }
    }
}
