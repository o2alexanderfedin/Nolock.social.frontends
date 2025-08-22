using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Implementation of background processing queue for OCR documents.
    /// Manages queue state in memory and provides thread-safe access to document queue operations.
    /// </summary>
    public class DocumentProcessingQueue : IBackgroundProcessingQueue, IDisposable
    {
        
        // Thread-safe collections for in-memory queue state
        private readonly ConcurrentDictionary<string, QueuedDocument> _queuedDocuments;
        private readonly ConcurrentQueue<string> _processingQueue;
        private readonly SemaphoreSlim _persistenceLock;
        private readonly SemaphoreSlim _queueLock;
        
        // Queue state management
        private QueueState _currentState;
        private readonly object _stateLock = new object();
        private bool _disposed = false;
        
        // Queue statistics
        private QueueStatistics _statistics;
        private readonly object _statisticsLock = new object();

        /// <summary>
        /// Initializes a new instance of the DocumentProcessingQueue class.
        /// </summary>
        public DocumentProcessingQueue()
        {
            
            _queuedDocuments = new ConcurrentDictionary<string, QueuedDocument>();
            _processingQueue = new ConcurrentQueue<string>();
            _persistenceLock = new SemaphoreSlim(1, 1);
            _queueLock = new SemaphoreSlim(1, 1);
            
            _currentState = QueueState.Stopped;
            _statistics = new QueueStatistics
            {
                StartedAt = DateTime.UtcNow
            };
        }

        #region Events

        /// <inheritdoc />
        public event EventHandler<QueuedDocumentEventArgs> DocumentQueued;

        /// <inheritdoc />
        public event EventHandler<QueuedDocumentEventArgs> ProcessingStatusChanged;

        /// <inheritdoc />
        public event EventHandler<QueuedDocumentEventArgs> ProcessingCompleted;

        /// <inheritdoc />
        public event EventHandler<QueueStateChangedEventArgs> QueueStateChanged;

        #endregion

        #region Properties

        /// <inheritdoc />
        public QueueState CurrentState
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState;
                }
            }
        }

        /// <inheritdoc />
        public int QueueCount => _queuedDocuments.Values.Count(d => d.Status == QueuedDocumentStatus.Queued);

        /// <inheritdoc />
        public int ProcessingCount => _queuedDocuments.Values.Count(d => d.Status == QueuedDocumentStatus.Processing);

        /// <inheritdoc />
        public int CompletedCount => _queuedDocuments.Values.Count(d => d.IsCompleted);

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public async Task<string> EnqueueDocumentAsync(
            OCRSubmissionRequest request,
            QueuePriority priority = QueuePriority.Normal,
            Dictionary<string, object> metadata = null,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            ThrowIfDisposed();

            lock (_stateLock)
            {
                if (_currentState == QueueState.Stopping)
                    throw new InvalidOperationException("Queue is shutting down and cannot accept new documents.");
            }

            // Create queued document
            var queuedDocument = QueuedDocument.CreateFromRequest(request, priority, metadata, "DocumentProcessingQueue");

            await _queueLock.WaitAsync(cancellationToken);
            try
            {
                // Add to in-memory queue
                _queuedDocuments.TryAdd(queuedDocument.QueueId, queuedDocument);
                
                // Update queue positions
                UpdateQueuePositions();
                
                // Persist to IndexedDB
                await PersistDocumentAsync(queuedDocument, cancellationToken);

                // Add to processing queue for background processing
                _processingQueue.Enqueue(queuedDocument.QueueId);

                // Raise event
                DocumentQueued?.Invoke(this, new QueuedDocumentEventArgs(queuedDocument));

                return queuedDocument.QueueId;
            }
            finally
            {
                _queueLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<QueuedDocument>> GetQueuedDocumentsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Return current in-memory state
            var documents = _queuedDocuments.Values
                .OrderByDescending(d => d.Priority)
                .ThenBy(d => d.QueuedAt)
                .ToList();

            return documents.AsReadOnly();
        }

        /// <inheritdoc />
        public async Task<QueuedDocument> GetQueuedDocumentAsync(string queueId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(queueId))
                throw new ArgumentNullException(nameof(queueId));

            ThrowIfDisposed();

            _queuedDocuments.TryGetValue(queueId, out var document);
            return document;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveDocumentAsync(string queueId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(queueId))
                throw new ArgumentNullException(nameof(queueId));

            ThrowIfDisposed();

            await _queueLock.WaitAsync(cancellationToken);
            try
            {
                if (!_queuedDocuments.TryGetValue(queueId, out var document))
                    return false;

                // Cancel processing if currently processing
                if (document.IsCancellable)
                {
                    document.Cancel();
                }

                // Remove from in-memory collections
                _queuedDocuments.TryRemove(queueId, out _);
                
                // Update queue positions
                UpdateQueuePositions();

                // Remove from persistence
                await RemoveFromPersistenceAsync(queueId, cancellationToken);

                // Raise event
                ProcessingCompleted?.Invoke(this, new QueuedDocumentEventArgs(document));

                return true;
            }
            finally
            {
                _queueLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task PauseProcessingAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await ChangeStateAsync(QueueState.Paused, cancellationToken);
        }

        /// <inheritdoc />
        public async Task ResumeProcessingAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await ChangeStateAsync(QueueState.Running, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> CancelDocumentProcessingAsync(string queueId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(queueId))
                throw new ArgumentNullException(nameof(queueId));

            ThrowIfDisposed();

            await _queueLock.WaitAsync(cancellationToken);
            try
            {
                if (!_queuedDocuments.TryGetValue(queueId, out var document))
                    return false;

                if (!document.IsCancellable)
                    return false;

                // Cancel the document
                document.Cancel();

                // Update persistence
                await PersistDocumentAsync(document, cancellationToken);

                // Raise event
                ProcessingStatusChanged?.Invoke(this, new QueuedDocumentEventArgs(document));
                ProcessingCompleted?.Invoke(this, new QueuedDocumentEventArgs(document));

                return true;
            }
            finally
            {
                _queueLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<int> ClearCompletedDocumentsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await _queueLock.WaitAsync(cancellationToken);
            try
            {
                var completedDocuments = _queuedDocuments.Values
                    .Where(d => d.IsCompleted)
                    .ToList();

                int removedCount = 0;

                foreach (var document in completedDocuments)
                {
                    if (_queuedDocuments.TryRemove(document.QueueId, out _))
                    {
                        await RemoveFromPersistenceAsync(document.QueueId, cancellationToken);
                        removedCount++;
                    }
                }

                UpdateQueuePositions();
                return removedCount;
            }
            finally
            {
                _queueLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> RetryDocumentAsync(string queueId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(queueId))
                throw new ArgumentNullException(nameof(queueId));

            ThrowIfDisposed();

            await _queueLock.WaitAsync(cancellationToken);
            try
            {
                if (!_queuedDocuments.TryGetValue(queueId, out var document))
                    return false;

                if (!document.IsRetryable)
                    throw new InvalidOperationException("Document is not in a retryable state.");

                // Prepare document for retry
                document.PrepareForRetry();

                // Add back to processing queue
                _processingQueue.Enqueue(queueId);

                // Update queue positions
                UpdateQueuePositions();

                // Update persistence
                await PersistDocumentAsync(document, cancellationToken);

                // Raise event
                ProcessingStatusChanged?.Invoke(this, new QueuedDocumentEventArgs(document));

                return true;
            }
            finally
            {
                _queueLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Load persisted queue state
            await LoadPersistedQueueAsync(cancellationToken);

            // Change to running state
            await ChangeStateAsync(QueueState.Running, cancellationToken);
        }

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return;

            await ChangeStateAsync(QueueState.Stopping, cancellationToken);

            // Wait for current processing to complete (implementation would be in background processor)
            await Task.Delay(100, cancellationToken); // Brief delay for cleanup

            await ChangeStateAsync(QueueState.Stopped, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<QueueStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            lock (_statisticsLock)
            {
                // Update current statistics
                var stats = new QueueStatistics
                {
                    TotalProcessed = _statistics.TotalProcessed,
                    SuccessfullyProcessed = _statistics.SuccessfullyProcessed,
                    FailedProcessing = _statistics.FailedProcessing,
                    AverageProcessingTimeMs = _statistics.AverageProcessingTimeMs,
                    ThroughputPerMinute = _statistics.ThroughputPerMinute,
                    StartedAt = _statistics.StartedAt,
                    LastUpdated = DateTime.UtcNow
                };

                return stats;
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Updates the status of a queued document and persists the changes.
        /// This method is called by the background processor.
        /// </summary>
        /// <param name="queueId">The queue ID of the document to update.</param>
        /// <param name="status">The new status.</param>
        /// <param name="ocrStatus">Optional OCR status response.</param>
        /// <param name="errorMessage">Optional error message.</param>
        /// <param name="errorCode">Optional error code.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the document was updated successfully.</returns>
        internal async Task<bool> UpdateDocumentStatusAsync(
            string queueId,
            QueuedDocumentStatus status,
            OCRStatusResponse ocrStatus = null,
            string errorMessage = null,
            string errorCode = null,
            CancellationToken cancellationToken = default)
        {
            if (!_queuedDocuments.TryGetValue(queueId, out var document))
                return false;

            var previousStatus = document.Status;

            // Update document status
            document.UpdateStatus(status, errorMessage, errorCode);

            // Update OCR status if provided
            if (ocrStatus != null)
            {
                document.UpdateFromOcrStatus(ocrStatus);
            }

            // Update statistics
            UpdateStatistics(document, previousStatus);

            // Persist changes
            await PersistDocumentAsync(document, cancellationToken);

            // Raise appropriate events
            ProcessingStatusChanged?.Invoke(this, new QueuedDocumentEventArgs(document));

            if (document.IsCompleted)
            {
                ProcessingCompleted?.Invoke(this, new QueuedDocumentEventArgs(document));
            }

            return true;
        }

        /// <summary>
        /// Gets the next document from the queue for processing.
        /// This method is called by the background processor.
        /// </summary>
        /// <returns>The next queued document or null if queue is empty.</returns>
        internal QueuedDocument GetNextDocumentForProcessing()
        {
            if (_processingQueue.TryDequeue(out var queueId))
            {
                if (_queuedDocuments.TryGetValue(queueId, out var document) &&
                    document.Status == QueuedDocumentStatus.Queued)
                {
                    return document;
                }
            }

            return null;
        }

        #endregion

        #region Private Methods

        private async Task PersistDocumentAsync(QueuedDocument document, CancellationToken cancellationToken)
        {
            // No persistence - documents are kept in memory only
            await Task.CompletedTask;
        }

        private async Task RemoveFromPersistenceAsync(string queueId, CancellationToken cancellationToken)
        {
            // No persistence - documents are kept in memory only
            await Task.CompletedTask;
        }

        private async Task LoadPersistedQueueAsync(CancellationToken cancellationToken)
        {
            // No persistence - queue starts empty
            UpdateQueuePositions();
            await Task.CompletedTask;
        }

        private void UpdateQueuePositions()
        {
            var queuedDocuments = _queuedDocuments.Values
                .Where(d => d.Status == QueuedDocumentStatus.Queued)
                .OrderByDescending(d => d.Priority)
                .ThenBy(d => d.QueuedAt)
                .ToList();

            for (int i = 0; i < queuedDocuments.Count; i++)
            {
                queuedDocuments[i].QueuePosition = i + 1;
            }
        }

        private async Task ChangeStateAsync(QueueState newState, CancellationToken cancellationToken)
        {
            QueueState previousState;
            
            lock (_stateLock)
            {
                previousState = _currentState;
                _currentState = newState;
            }

            if (previousState != newState)
            {
                QueueStateChanged?.Invoke(this, new QueueStateChangedEventArgs(previousState, newState));
            }
        }

        private void UpdateStatistics(QueuedDocument document, QueuedDocumentStatus previousStatus)
        {
            lock (_statisticsLock)
            {
                if (document.IsCompleted && previousStatus != document.Status)
                {
                    _statistics.TotalProcessed++;

                    if (document.Status == QueuedDocumentStatus.Completed)
                    {
                        _statistics.SuccessfullyProcessed++;
                    }
                    else if (document.Status == QueuedDocumentStatus.Failed)
                    {
                        _statistics.FailedProcessing++;
                    }

                    // Update average processing time
                    if (document.ProcessingTimeMs.HasValue && _statistics.TotalProcessed > 0)
                    {
                        _statistics.AverageProcessingTimeMs = 
                            (_statistics.AverageProcessingTimeMs * (_statistics.TotalProcessed - 1) + document.ProcessingTimeMs.Value) 
                            / _statistics.TotalProcessed;
                    }

                    // Calculate throughput
                    var uptimeMinutes = DateTime.UtcNow.Subtract(_statistics.StartedAt).TotalMinutes;
                    if (uptimeMinutes > 0)
                    {
                        _statistics.ThroughputPerMinute = _statistics.TotalProcessed / uptimeMinutes;
                    }
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DocumentProcessingQueue));
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the DocumentProcessingQueue and releases all resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the DocumentProcessingQueue.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Cancel all active operations
                foreach (var document in _queuedDocuments.Values)
                {
                    document.CancellationTokenSource?.Dispose();
                }

                _persistenceLock?.Dispose();
                _queueLock?.Dispose();

                _disposed = true;
            }
        }

        #endregion
    }
}