namespace NoLock.Social.Core.Common.Constants
{
    /// <summary>
    /// Timeout constants for consistent timing across the application.
    /// </summary>
    public static class TimeoutConstants
    {
        /// <summary>
        /// UI interaction timeouts in milliseconds.
        /// </summary>
        public static class UI
        {
            /// <summary>
            /// Debounce delay for input fields to avoid excessive API calls.
            /// </summary>
            public const int DebounceDelayMs = 300;

            /// <summary>
            /// Short delay for UI animations and transitions.
            /// </summary>
            public const int AnimationDelayMs = 200;

            /// <summary>
            /// Standard delay for toast/notification display.
            /// </summary>
            public const int NotificationDelayMs = 3000;

            /// <summary>
            /// Delay before showing loading indicators.
            /// </summary>
            public const int LoadingIndicatorDelayMs = 500;

            /// <summary>
            /// Default modal close animation delay.
            /// </summary>
            public const int ModalCloseDelayMs = 250;
        }

        /// <summary>
        /// Network operation timeouts in milliseconds.
        /// </summary>
        public static class Network
        {
            /// <summary>
            /// Quick network check timeout for connectivity tests.
            /// </summary>
            public const int QuickCheckTimeoutMs = 1000;

            /// <summary>
            /// Standard HTTP request timeout.
            /// </summary>
            public const int StandardRequestTimeoutMs = 30000;

            /// <summary>
            /// Extended timeout for large file uploads.
            /// </summary>
            public const int FileUploadTimeoutMs = 60000;

            /// <summary>
            /// Timeout for OCR processing requests.
            /// </summary>
            public const int OCRProcessingTimeoutMs = 45000;

            /// <summary>
            /// Short timeout for health check endpoints.
            /// </summary>
            public const int HealthCheckTimeoutMs = 5000;

            /// <summary>
            /// Timeout for batch operations.
            /// </summary>
            public const int BatchOperationTimeoutMs = 120000;
        }

        /// <summary>
        /// Polling and retry intervals in milliseconds.
        /// </summary>
        public static class Polling
        {
            /// <summary>
            /// Fast polling interval for real-time updates.
            /// </summary>
            public const int FastIntervalMs = 1000;

            /// <summary>
            /// Standard polling interval for status checks.
            /// </summary>
            public const int StandardIntervalMs = 5000;

            /// <summary>
            /// Slow polling interval for background tasks.
            /// </summary>
            public const int SlowIntervalMs = 30000;

            /// <summary>
            /// Very slow polling for low-priority background tasks.
            /// </summary>
            public const int BackgroundIntervalMs = 60000;

            /// <summary>
            /// Initial delay before starting polling.
            /// </summary>
            public const int InitialDelayMs = 500;
        }

        /// <summary>
        /// Storage operation timeouts in milliseconds.
        /// </summary>
        public static class Storage
        {
            /// <summary>
            /// IndexedDB operation timeout.
            /// </summary>
            public const int IndexedDBTimeoutMs = 5000;

            /// <summary>
            /// Cache expiration check interval.
            /// </summary>
            public const int CacheCheckIntervalMs = 60000;

            /// <summary>
            /// Offline queue processing interval.
            /// </summary>
            public const int OfflineQueueIntervalMs = 10000;

            /// <summary>
            /// Storage cleanup interval.
            /// </summary>
            public const int CleanupIntervalMs = 300000;
        }

        /// <summary>
        /// Camera and media timeouts in milliseconds.
        /// </summary>
        public static class Camera
        {
            /// <summary>
            /// Camera initialization timeout.
            /// </summary>
            public const int InitializationTimeoutMs = 10000;

            /// <summary>
            /// Frame capture delay for camera preview.
            /// </summary>
            public const int FrameCaptureDelayMs = 100;

            /// <summary>
            /// Auto-focus attempt timeout.
            /// </summary>
            public const int AutoFocusTimeoutMs = 3000;

            /// <summary>
            /// Video recording maximum duration.
            /// </summary>
            public const int MaxRecordingDurationMs = 300000;
        }

        /// <summary>
        /// Test and development timeouts in milliseconds.
        /// </summary>
        public static class Testing
        {
            /// <summary>
            /// Unit test default timeout.
            /// </summary>
            public const int UnitTestTimeoutMs = 5000;

            /// <summary>
            /// Integration test timeout.
            /// </summary>
            public const int IntegrationTestTimeoutMs = 30000;

            /// <summary>
            /// Mock delay for simulated operations.
            /// </summary>
            public const int MockDelayMs = 100;

            /// <summary>
            /// Async operation test timeout.
            /// </summary>
            public const int AsyncTestTimeoutMs = 10000;
        }
        
        /// <summary>
        /// Common delay values in milliseconds.
        /// </summary>
        public static class Delays
        {
            /// <summary>
            /// Very short delay for immediate operations (100ms).
            /// </summary>
            public const int ShortDelay = 100;
            
            /// <summary>
            /// Standard delay for general operations (1000ms).
            /// </summary>
            public const int StandardDelay = 1000;
            
            /// <summary>
            /// Long delay for extended operations (5000ms).
            /// </summary>
            public const int LongDelay = 5000;
            
            /// <summary>
            /// Maximum retry delay in seconds for exponential backoff (60s).
            /// </summary>
            public const int MaxRetryDelaySeconds = 60;
        }
    }
}