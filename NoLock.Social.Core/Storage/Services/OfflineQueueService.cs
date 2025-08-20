using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoLock.Social.Core.Storage.Interfaces;
using NoLock.Social.Core.Common.Results;
using NoLock.Social.Core.Common.Extensions;

namespace NoLock.Social.Core.Storage.Services
{
    /// <summary>
    /// Service for managing offline operation queue and synchronization
    /// Processes operations in priority order with retry logic and exponential backoff
    /// </summary>
    public class OfflineQueueService : IOfflineQueueService, IDisposable
    {
        private readonly IOfflineStorageService _storageService;
        private readonly ILogger<OfflineQueueService>? _logger;
        private bool _disposed = false;
        private bool _isProcessing = false;

        // Retry configuration
        private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(1);
        private static readonly int MaxRetryDelaySeconds = 60;

        public OfflineQueueService(IOfflineStorageService storageService, ILogger<OfflineQueueService>? logger = null)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _logger = logger;
        }

        // Events
        public event EventHandler<OfflineQueueEventArgs>? QueueProcessingStarted;
        public event EventHandler<OfflineQueueEventArgs>? QueueProcessingCompleted;
        public event EventHandler<OfflineOperationEventArgs>? OperationSucceeded;
        public event EventHandler<OfflineOperationEventArgs>? OperationFailed;

        public async Task QueueOperationAsync(OfflineOperation operation)
        {
            ThrowIfDisposed();
            
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (_logger != null)
            {
                var result = await _logger.ExecuteWithLogging(async () =>
                {
                    _logger?.LogInformation("Queueing operation {OperationId} of type {OperationType}", 
                        operation.OperationId, operation.OperationType);

                    await _storageService.QueueOfflineOperationAsync(operation);

                    _logger?.LogDebug("Successfully queued operation {OperationId}", operation.OperationId);
                }, $"Failed to queue operation {operation.OperationId}");

                result.ThrowIfFailure();
            }
            else
            {
                // Execute without logging when logger is null
                await _storageService.QueueOfflineOperationAsync(operation);
            }
        }

        public async Task ProcessQueueAsync()
        {
            ThrowIfDisposed();

            if (_isProcessing)
            {
                _logger?.LogWarning("Queue processing already in progress, skipping");
                return;
            }

            _isProcessing = true;
            var startTime = DateTime.UtcNow;
            var stats = new OfflineQueueEventArgs();

            try
            {
                _logger?.LogInformation("Starting queue processing");
                QueueProcessingStarted?.Invoke(this, stats);

                // Get all pending operations and sort by priority (lower = higher priority), then by creation time (FIFO)
                var pendingOperations = await _storageService.GetPendingOperationsAsync();
                var sortedOperations = pendingOperations
                    .OrderBy(op => op.Priority)
                    .ThenBy(op => op.CreatedAt)
                    .ToList();

                _logger?.LogInformation("Found {Count} pending operations to process", sortedOperations.Count);

                foreach (var operation in sortedOperations)
                {
                    stats.OperationsProcessed++;

                    try
                    {
                        // Check if operation has exceeded max retries
                        if (operation.RetryCount >= operation.MaxRetries)
                        {
                            _logger?.LogWarning("Operation {OperationId} has exceeded max retries ({MaxRetries}), skipping", 
                                operation.OperationId, operation.MaxRetries);
                            
                            var failedArgs = new OfflineOperationEventArgs
                            {
                                Operation = operation,
                                ErrorMessage = "Maximum retry attempts exceeded"
                            };
                            OperationFailed?.Invoke(this, failedArgs);
                            stats.OperationsFailed++;
                            continue;
                        }

                        // Apply exponential backoff delay for retries
                        if (operation.RetryCount > 0)
                        {
                            var delay = CalculateRetryDelay(operation.RetryCount);
                            _logger?.LogDebug("Applying retry delay of {Delay}ms for operation {OperationId} (retry {RetryCount})", 
                                delay.TotalMilliseconds, operation.OperationId, operation.RetryCount);
                            await Task.Delay(delay);
                        }

                        // Process the operation
                        var success = await ProcessOperationAsync(operation);

                        if (success)
                        {
                            // Remove successful operation from queue
                            await _storageService.RemoveOperationAsync(operation.OperationId);
                            
                            var successArgs = new OfflineOperationEventArgs { Operation = operation };
                            OperationSucceeded?.Invoke(this, successArgs);
                            stats.OperationsSucceeded++;

                            _logger?.LogInformation("Successfully processed operation {OperationId}", operation.OperationId);
                        }
                        else
                        {
                            // Increment retry count and update operation
                            operation.RetryCount++;
                            await _storageService.QueueOfflineOperationAsync(operation); // This should update the existing operation

                            var failedArgs = new OfflineOperationEventArgs
                            {
                                Operation = operation,
                                ErrorMessage = "Operation processing failed, scheduled for retry"
                            };
                            OperationFailed?.Invoke(this, failedArgs);
                            stats.OperationsFailed++;

                            _logger?.LogWarning("Operation {OperationId} failed, retry count: {RetryCount}/{MaxRetries}", 
                                operation.OperationId, operation.RetryCount, operation.MaxRetries);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error processing operation {OperationId}", operation.OperationId);
                        
                        // Increment retry count for failed operations
                        operation.RetryCount++;
                        await _storageService.QueueOfflineOperationAsync(operation);

                        var failedArgs = new OfflineOperationEventArgs
                        {
                            Operation = operation,
                            ErrorMessage = ex.Message,
                            Exception = ex
                        };
                        OperationFailed?.Invoke(this, failedArgs);
                        stats.OperationsFailed++;
                    }
                }

                stats.ProcessingTime = DateTime.UtcNow - startTime;
                _logger?.LogInformation("Queue processing completed. Processed: {Processed}, Succeeded: {Succeeded}, Failed: {Failed}, Time: {Time}ms",
                    stats.OperationsProcessed, stats.OperationsSucceeded, stats.OperationsFailed, stats.ProcessingTime.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Critical error during queue processing");
                throw;
            }
            finally
            {
                _isProcessing = false;
                QueueProcessingCompleted?.Invoke(this, stats);
            }
        }

        public async Task<OfflineQueueStatus> GetQueueStatusAsync()
        {
            ThrowIfDisposed();

            var result = await (_logger ?? NullLogger<OfflineQueueService>.Instance)
                .ExecuteWithLogging(async () =>
                {
                    var pendingOperations = await _storageService.GetPendingOperationsAsync();
                    
                    var status = new OfflineQueueStatus
                    {
                        PendingOperations = pendingOperations.Count,
                        HighPriorityOperations = pendingOperations.Count(op => op.Priority <= 1),
                        FailedOperations = pendingOperations.Count(op => op.RetryCount > 0),
                        OldestOperationTime = pendingOperations.Any() ? pendingOperations.Min(op => op.CreatedAt) : null,
                        IsProcessing = _isProcessing
                    };

                    return status;
                }, "Failed to get queue status");

            return result.ThrowIfFailure();
        }

        public async Task ClearProcessedOperationsAsync()
        {
            ThrowIfDisposed();

            if (_logger != null)
            {
                var result = await _logger.ExecuteWithLogging(async () =>
                {
                    var pendingOperations = await _storageService.GetPendingOperationsAsync();
                    
                    // Only keep operations that have failed and are below max retry count
                    var operationsToKeep = pendingOperations
                        .Where(op => op.RetryCount > 0 && op.RetryCount < op.MaxRetries)
                        .ToList();

                    // Clear all data and re-add operations that should be kept
                    await _storageService.ClearAllDataAsync();

                    foreach (var operation in operationsToKeep)
                    {
                        await _storageService.QueueOfflineOperationAsync(operation);
                    }

                    var clearedCount = pendingOperations.Count - operationsToKeep.Count;
                    _logger?.LogInformation("Cleared {ClearedCount} processed operations, kept {KeptCount} failed operations", 
                        clearedCount, operationsToKeep.Count);
                }, "Failed to clear processed operations");

                result.ThrowIfFailure();
            }
            else
            {
                // Execute without logging when logger is null
                var pendingOperations = await _storageService.GetPendingOperationsAsync();
                var operationsToKeep = pendingOperations
                    .Where(op => op.RetryCount > 0 && op.RetryCount < op.MaxRetries)
                    .ToList();
                await _storageService.ClearAllDataAsync();
                foreach (var operation in operationsToKeep)
                {
                    await _storageService.QueueOfflineOperationAsync(operation);
                }
            }
        }

        /// <summary>
        /// Process a single operation. Override this method to implement specific operation handling.
        /// </summary>
        /// <param name="operation">The operation to process</param>
        /// <returns>True if successful, false if failed and should be retried</returns>
        protected virtual async Task<bool> ProcessOperationAsync(OfflineOperation operation)
        {
            // Base implementation - this should be overridden by subclasses or injected handlers
            // For now, we'll simulate processing and return success for demonstration
            _logger?.LogDebug("Processing operation {OperationId} of type {OperationType}", 
                operation.OperationId, operation.OperationType);
            
            // Simulate some async work
            await Task.Delay(100);
            
            // TODO: Implement actual operation processing based on operation type
            // This could involve calling external APIs, uploading files, etc.
            
            return true; // Default to success for now
        }

        /// <summary>
        /// Calculate retry delay using exponential backoff with jitter
        /// </summary>
        /// <param name="retryCount">Number of previous retry attempts</param>
        /// <returns>Delay before next retry attempt</returns>
        private static TimeSpan CalculateRetryDelay(int retryCount)
        {
            // Exponential backoff: 1s, 2s, 4s, 8s, 16s, 32s, max 60s
            var delaySeconds = Math.Min(Math.Pow(2, retryCount - 1), MaxRetryDelaySeconds);
            
            // Add jitter (±25%) to prevent thundering herd
            var random = new Random();
            var jitter = 1.0 + (random.NextDouble() - 0.5) * 0.5; // 0.75 to 1.25
            
            return TimeSpan.FromSeconds(delaySeconds * jitter);
        }

        // IDisposable Implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clean up managed resources
                    // Note: _storageService is not disposed here as it's injected dependency
                    _logger?.LogDebug("OfflineQueueService disposed");
                }

                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OfflineQueueService));
            }
        }
    }
}