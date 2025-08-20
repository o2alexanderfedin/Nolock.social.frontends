namespace NoLock.Social.Core.Common.Constants
{
    /// <summary>
    /// Centralized constants for array sizes, collection limits, and batch processing thresholds.
    /// </summary>
    public static class LimitConstants
    {
        /// <summary>
        /// Batch processing size limits for various operations.
        /// </summary>
        public static class BatchSize
        {
            /// <summary>
            /// Default batch size for processing operations (100 items).
            /// </summary>
            public const int Default = 100;
            
            /// <summary>
            /// Small batch size for memory-intensive operations (10 items).
            /// </summary>
            public const int Small = 10;
            
            /// <summary>
            /// Medium batch size for standard operations (50 items).
            /// </summary>
            public const int Medium = 50;
            
            /// <summary>
            /// Large batch size for lightweight operations (500 items).
            /// </summary>
            public const int Large = 500;
            
            /// <summary>
            /// Batch size for OCR processing operations (25 items).
            /// </summary>
            public const int OcrProcessing = 25;
        }
        
        /// <summary>
        /// Maximum item limits for collections and caches.
        /// </summary>
        public static class MaxItems
        {
            /// <summary>
            /// Maximum items in memory cache (1000 items).
            /// </summary>
            public const int CacheSize = 1000;
            
            /// <summary>
            /// Maximum concurrent operations (50 items).
            /// </summary>
            public const int ConcurrentOperations = 50;
            
            /// <summary>
            /// Maximum retry queue size (100 items).
            /// </summary>
            public const int RetryQueueSize = 100;
            
            /// <summary>
            /// Maximum history entries to maintain (500 items).
            /// </summary>
            public const int HistorySize = 500;
            
            /// <summary>
            /// Maximum failed requests to store (200 items).
            /// </summary>
            public const int FailedRequestsSize = 200;
        }
        
        /// <summary>
        /// Buffer size limits for various operations.
        /// </summary>
        public static class BufferSize
        {
            /// <summary>
            /// Default buffer size for I/O operations (4096 bytes).
            /// </summary>
            public const int Default = 4096;
            
            /// <summary>
            /// Small buffer size for frequent operations (1024 bytes).
            /// </summary>
            public const int Small = 1024;
            
            /// <summary>
            /// Large buffer size for bulk operations (8192 bytes).
            /// </summary>
            public const int Large = 8192;
            
            /// <summary>
            /// Image processing buffer size (65536 bytes).
            /// </summary>
            public const int ImageProcessing = 65536;
        }
        
        /// <summary>
        /// Pagination limits for data retrieval operations.
        /// </summary>
        public static class Pagination
        {
            /// <summary>
            /// Default page size (20 items).
            /// </summary>
            public const int DefaultPageSize = 20;
            
            /// <summary>
            /// Maximum page size allowed (100 items).
            /// </summary>
            public const int MaxPageSize = 100;
            
            /// <summary>
            /// Minimum page size allowed (5 items).
            /// </summary>
            public const int MinPageSize = 5;
        }
        
        /// <summary>
        /// String length limits for validation.
        /// </summary>
        public static class StringLength
        {
            /// <summary>
            /// Maximum URL length (2048 characters).
            /// </summary>
            public const int MaxUrlLength = 2048;
            
            /// <summary>
            /// Maximum file path length (260 characters).
            /// </summary>
            public const int MaxFilePathLength = 260;
            
            /// <summary>
            /// Maximum identifier length (128 characters).
            /// </summary>
            public const int MaxIdLength = 128;
            
            /// <summary>
            /// Maximum description length (1000 characters).
            /// </summary>
            public const int MaxDescriptionLength = 1000;
        }
        
        /// <summary>
        /// Queue size limits for various services.
        /// </summary>
        public static class QueueSize
        {
            /// <summary>
            /// Maximum offline queue size (500 items).
            /// </summary>
            public const int OfflineQueue = 500;
            
            /// <summary>
            /// Maximum pending operations queue (100 items).
            /// </summary>
            public const int PendingOperations = 100;
            
            /// <summary>
            /// Maximum notification queue size (50 items).
            /// </summary>
            public const int Notifications = 50;
        }
    }
}