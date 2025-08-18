namespace SensorGateway.Bluetooth
{
    /// <summary>
    /// Contains constant values used throughout BTDevice operations.
    /// Follows Single Responsibility Principle by centralizing configuration constants.
    /// </summary>
    public static class BTDeviceConstants
    {
        #region Timing Constants
        /// <summary>
        /// Delay in milliseconds for wait loops and polling operations.
        /// </summary>
        public const int WAIT_LOOP_DELAY = 100;

        /// <summary>
        /// Timeout in seconds for adapter power-on operations.
        /// </summary>
        public const int ADAPTER_POWER_TIMEOUT_SECONDS = 5;

        /// <summary>
        /// Maximum number of connection attempts before giving up.
        /// </summary>
        public const int MAX_CONNECTION_ATTEMPTS = 3;

        /// <summary>
        /// Delay in milliseconds to allow connection to stabilize.
        /// </summary>
        public const int CONNECTION_STABILIZATION_DELAY = 2000;

        /// <summary>
        /// Delay in milliseconds between connection retry attempts.
        /// </summary>
        public const int CONNECTION_RETRY_DELAY = 1000;

        /// <summary>
        /// Timeout in seconds for token-based operations.
        /// </summary>
        public const int TOKEN_TIMEOUT_SECONDS = 120;

        /// <summary>
        /// Timeout in seconds for waiting on notification responses.
        /// </summary>
        public const int NOTIFICATION_WAIT_TIMEOUT_SECONDS = 30;
        #endregion
    }
}
