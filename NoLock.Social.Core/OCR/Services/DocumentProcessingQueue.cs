using System.Collections.Concurrent;
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
        private bool _disposed;
        
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
            CancellationToken cancellation = default)
        {
            ValidateEnqueueRequest(request);
            ValidateQueueState();

            var queuedDocument = QueuedDocument.CreateFromRequest(request, priority, metadata, "DocumentProcessingQueue");
            return await ProcessDocumentEnqueueAsync(queuedDocument, cancellation);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<QueuedDocument>> GetQueuedDocumentsAsync(CancellationToken cancellation = default)
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
        public async Task<QueuedDocument> GetQueuedDocumentAsync(string queueId, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(queueId))
                throw new ArgumentNullException(nameof(queueId));

            ThrowIfDisposed();

            _queuedDocuments.TryGetValue(queueId, out var document);
            return document;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveDocumentAsync(string queueId, CancellationToken cancellation = default)
        {
            ValidateRemoveRequest(queueId);

            await _queueLock.WaitAsync(cancellation);
            try
            {
                return await ProcessDocumentRemovalAsync(queueId, cancellation);
            }
            finally
            {
                _queueLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task PauseProcessingAsync(CancellationToken cancellation = default)
        {
            ThrowIfDisposed();
            await ChangeStateAsync(QueueState.Paused, cancellation);
        }

        /// <inheritdoc />
        public async Task ResumeProcessingAsync(CancellationToken cancellation = default)
        {
            ThrowIfDisposed();
            await ChangeStateAsync(QueueState.Running, cancellation);
        }

        /// <inheritdoc />
        public async Task<bool> CancelDocumentProcessingAsync(string queueId, CancellationToken cancellation = default)
        {
            ValidateCancelRequest(queueId);

            await _queueLock.WaitAsync(cancellation);
            try
            {
                return await ProcessDocumentCancellationAsync(queueId, cancellation);
            }
            finally
            {
                _queueLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<int> ClearCompletedDocumentsAsync(CancellationToken cancellation = default)
        {
            ThrowIfDisposed();

            await _queueLock.WaitAsync(cancellation);
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
                        await RemoveFromPersistenceAsync(document.QueueId, cancellation);
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
        public async Task<bool> RetryDocumentAsync(string queueId, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(queueId))
                throw new ArgumentNullException(nameof(queueId));

            ThrowIfDisposed();

            await _queueLock.WaitAsync(cancellation);
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
                await PersistDocumentAsync(document, cancellation);

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
        public async Task StartAsync(CancellationToken cancellation = default)
        {
            ThrowIfDisposed();

            // Load persisted queue state
            await LoadPersistedQueueAsync(cancellation);

            // Change to running state
            await ChangeStateAsync(QueueState.Running, cancellation);
        }

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellation = default)
        {
            if (_disposed)
                return;

            await ChangeStateAsync(QueueState.Stopping, cancellation);

            // Wait for current processing to complete (implementation would be in background processor)
            await Task.Delay(100, cancellation); // Brief delay for cleanup

            await ChangeStateAsync(QueueState.Stopped, cancellation);
        }

        /// <inheritdoc />
        public async Task<QueueStatistics> GetStatisticsAsync(CancellationToken cancellation = default)
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
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>True if the document was updated successfully.</returns>
        internal async Task<bool> UpdateDocumentStatusAsync(
            string queueId,
            QueuedDocumentStatus status,
            OCRStatusResponse ocrStatus = null,
            string errorMessage = null,
            string errorCode = null,
            CancellationToken cancellation = default)
        {
            if (!TryGetDocumentForUpdate(queueId, out var document))
                return false;

            var previousStatus = document.Status;
            
            UpdateDocumentWithNewStatus(document, status, errorMessage, errorCode, ocrStatus);
            UpdateStatistics(document, previousStatus);
            
            await PersistDocumentAsync(document, cancellation);
            NotifyStatusChanged(document);

            return true;
        }

        /// <summary>
        /// Gets the next document from the queue for processing.
        /// This method is called by the background processor.
        /// </summary>
        /// <returns>The next queued document or null if queue is empty.</returns>
        internal QueuedDocument GetNextDocumentForProcessing()
        {
            // Get all queued documents sorted by priority (highest first)
            var queuedDocs = _queuedDocuments.Values
                .Where(d => d.Status == QueuedDocumentStatus.Queued)
                .OrderByDescending(d => d.Priority)
                .ThenBy(d => d.QueuedAt)
                .FirstOrDefault();

            if (queuedDocs != null && _processingQueue.TryDequeue(out var queueId))
            {
                // Return the highest priority document
                return queuedDocs;
            }

            return null;
        }

        #endregion

        #region Private Methods

        private void ValidateEnqueueRequest(OCRSubmissionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            ThrowIfDisposed();
        }

        private void ValidateQueueState()
        {
            lock (_stateLock)
            {
                if (_currentState == QueueState.Stopping || _currentState == QueueState.Stopped)
                    throw new InvalidOperationException("Queue is shutting down and cannot accept new documents.");
            }
        }

        private async Task<string> ProcessDocumentEnqueueAsync(QueuedDocument queuedDocument, CancellationToken cancellation)
        {
            await _queueLock.WaitAsync(cancellation);
            try
            {
                AddDocumentToQueue(queuedDocument);
                await PersistDocumentAsync(queuedDocument, cancellation);
                NotifyDocumentQueued(queuedDocument);
                return queuedDocument.QueueId;
            }
            finally
            {
                _queueLock.Release();
            }
        }

        private void AddDocumentToQueue(QueuedDocument queuedDocument)
        {
            _queuedDocuments.TryAdd(queuedDocument.QueueId, queuedDocument);
            UpdateQueuePositions();
            _processingQueue.Enqueue(queuedDocument.QueueId);
        }

        private void NotifyDocumentQueued(QueuedDocument queuedDocument)
        {
            DocumentQueued?.Invoke(this, new QueuedDocumentEventArgs(queuedDocument));
        }

        private bool TryGetDocumentForUpdate(string queueId, out QueuedDocument document)
        {
            return _queuedDocuments.TryGetValue(queueId, out document);
        }

        private void UpdateDocumentWithNewStatus(QueuedDocument document, QueuedDocumentStatus status, string errorMessage, string errorCode, OCRStatusResponse ocrStatus)
        {
            document.UpdateStatus(status, errorMessage, errorCode);
            
            if (ocrStatus != null)
            {
                document.UpdateFromOcrStatus(ocrStatus);
            }
        }

        private void NotifyStatusChanged(QueuedDocument document)
        {
            ProcessingStatusChanged?.Invoke(this, new QueuedDocumentEventArgs(document));

            if (document.IsCompleted)
            {
                ProcessingCompleted?.Invoke(this, new QueuedDocumentEventArgs(document));
            }
        }

        private async Task PersistDocumentAsync(QueuedDocument document, CancellationToken cancellation)
        {
            // No persistence - documents are kept in memory only
            await Task.CompletedTask;
        }

        private async Task RemoveFromPersistenceAsync(string queueId, CancellationToken cancellation)
        {
            // No persistence - documents are kept in memory only
            await Task.CompletedTask;
        }

        private async Task LoadPersistedQueueAsync(CancellationToken cancellation)
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

        private async Task ChangeStateAsync(QueueState newState, CancellationToken cancellation)
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
                if (ShouldUpdateStatistics(document, previousStatus))
                {
                    UpdateCompletionCounters(document);
                    UpdateProcessingTimeStatistics(document);
                    UpdateThroughputStatistics();
                }
            }
        }

        private bool ShouldUpdateStatistics(QueuedDocument document, QueuedDocumentStatus previousStatus)
        {
            return document.IsCompleted && previousStatus != document.Status;
        }

        private void UpdateCompletionCounters(QueuedDocument document)
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
        }

        private void UpdateProcessingTimeStatistics(QueuedDocument document)
        {
            if (document.ProcessingTimeMs.HasValue && _statistics.TotalProcessed > 0)
            {
                _statistics.AverageProcessingTimeMs = 
                    (_statistics.AverageProcessingTimeMs * (_statistics.TotalProcessed - 1) + document.ProcessingTimeMs.Value) 
                    / _statistics.TotalProcessed;
            }
        }

        private void UpdateThroughputStatistics()
        {
            var uptimeMinutes = DateTime.UtcNow.Subtract(_statistics.StartedAt).TotalMinutes;
            if (uptimeMinutes > 0)
            {
                _statistics.ThroughputPerMinute = _statistics.TotalProcessed / uptimeMinutes;
            }
        }

        private void ValidateRemoveRequest(string queueId)
        {
            if (string.IsNullOrEmpty(queueId))
                throw new ArgumentNullException(nameof(queueId));

            ThrowIfDisposed();
        }

        private async Task<bool> ProcessDocumentRemovalAsync(string queueId, CancellationToken cancellation)
        {
            if (!_queuedDocuments.TryGetValue(queueId, out var document))
                return false;

            CancelDocumentIfPossible(document);
            RemoveDocumentFromQueue(queueId);
            
            await RemoveFromPersistenceAsync(queueId, cancellation);
            NotifyDocumentRemovalCompleted(document);
            
            return true;
        }

        private void CancelDocumentIfPossible(QueuedDocument document)
        {
            if (document.IsCancellable)
            {
                document.Cancel();
            }
        }

        private void RemoveDocumentFromQueue(string queueId)
        {
            _queuedDocuments.TryRemove(queueId, out _);
            UpdateQueuePositions();
        }

        private void NotifyDocumentRemovalCompleted(QueuedDocument document)
        {
            ProcessingCompleted?.Invoke(this, new QueuedDocumentEventArgs(document));
        }

        private void ValidateCancelRequest(string queueId)
        {
            if (string.IsNullOrEmpty(queueId))
                throw new ArgumentNullException(nameof(queueId));

            ThrowIfDisposed();
        }

        private async Task<bool> ProcessDocumentCancellationAsync(string queueId, CancellationToken cancellation)
        {
            if (!TryGetCancellableDocument(queueId, out var document))
                return false;

            document.Cancel();
            await PersistDocumentAsync(document, cancellation);
            NotifyDocumentCancellation(document);
            
            return true;
        }

        private bool TryGetCancellableDocument(string queueId, out QueuedDocument document)
        {
            if (!_queuedDocuments.TryGetValue(queueId, out document))
                return false;

            return document.IsCancellable;
        }

        private void NotifyDocumentCancellation(QueuedDocument document)
        {
            ProcessingStatusChanged?.Invoke(this, new QueuedDocumentEventArgs(document));
            ProcessingCompleted?.Invoke(this, new QueuedDocumentEventArgs(document));
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