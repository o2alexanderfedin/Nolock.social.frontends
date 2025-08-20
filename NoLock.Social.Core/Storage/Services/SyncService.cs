using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.Common.Constants;
using NoLock.Social.Core.Storage.Interfaces;

namespace NoLock.Social.Core.Storage.Services
{
    /// <summary>
    /// Service for synchronizing offline data with the server when connectivity is restored
    /// </summary>
    public class SyncService : ISyncService
    {
        private readonly IOfflineQueueService _queueService;
        private readonly IOfflineStorageService _storageService;
        private readonly IConnectivityService _connectivityService;
        private readonly ILogger<SyncService> _logger;

        private bool _isSyncing;
        private SyncStatus _currentStatus;

        public SyncService(
            IOfflineQueueService queueService,
            IOfflineStorageService storageService,
            IConnectivityService connectivityService,
            ILogger<SyncService> logger)
        {
            _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _currentStatus = new SyncStatus();
        }

        public event EventHandler<SyncStartedEventArgs>? SyncStarted;
        public event EventHandler<SyncProgressEventArgs>? SyncProgress;
        public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;
        public event EventHandler<SyncConflictEventArgs>? SyncConflict;

        public async Task SyncOfflineDataAsync()
        {
            if (_isSyncing)
            {
                _logger.LogWarning("Sync operation already in progress");
                return;
            }

            if (!await _connectivityService.IsOnlineAsync())
            {
                _logger.LogWarning("Cannot sync: device is offline");
                return;
            }

            _isSyncing = true;
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogInformation("Starting offline data synchronization");

                // Get all pending operations
                var pendingOperations = await _storageService.GetPendingOperationsAsync();
                var operationTypes = pendingOperations.Select(op => op.OperationType).Distinct().ToArray();

                // Initialize sync status
                _currentStatus = new SyncStatus
                {
                    IsSyncing = true,
                    TotalOperations = pendingOperations.Count,
                    CompletedOperations = 0,
                    FailedOperations = 0,
                    SyncStartTime = startTime
                };

                // Raise sync started event
                SyncStarted?.Invoke(this, new SyncStartedEventArgs
                {
                    TotalOperations = pendingOperations.Count,
                    OperationTypes = operationTypes
                });

                if (pendingOperations.Count == 0)
                {
                    _logger.LogInformation("No pending operations to sync");
                    await CompleteSyncAsync(startTime, true, 0, 0, 0);
                    return;
                }

                // Sort operations by priority and dependencies
                var sortedOperations = SortOperationsByDependencies(pendingOperations);

                int successCount = 0;
                int failureCount = 0;

                // Process each operation
                foreach (var operation in sortedOperations)
                {
                    _currentStatus.CurrentOperation = operation.OperationType;
                    
                    try
                    {
                        await ProcessOperationAsync(operation);
                        successCount++;
                        _currentStatus.CompletedOperations++;

                        _logger.LogDebug("Successfully processed operation {OperationId} of type {OperationType}",
                            operation.OperationId, operation.OperationType);
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _currentStatus.FailedOperations++;
                        
                        _logger.LogError(ex, "Failed to process operation {OperationId} of type {OperationType}",
                            operation.OperationId, operation.OperationType);

                        // Handle operation failure
                        await HandleOperationFailureAsync(operation, ex);
                    }

                    // Report progress
                    SyncProgress?.Invoke(this, new SyncProgressEventArgs
                    {
                        CompletedOperations = _currentStatus.CompletedOperations,
                        TotalOperations = _currentStatus.TotalOperations,
                        CurrentOperation = operation.OperationType,
                        ProgressPercentage = _currentStatus.ProgressPercentage
                    });
                }

                await CompleteSyncAsync(startTime, failureCount == 0, pendingOperations.Count, successCount, failureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync operation failed with unexpected error");
                await CompleteSyncAsync(startTime, false, 0, 0, 0, ex.Message);
            }
            finally
            {
                _isSyncing = false;
                _currentStatus.IsSyncing = false;
            }
        }

        public async Task SyncSessionAsync(DocumentSession session)
        {
            if (!await _connectivityService.IsOnlineAsync())
            {
                _logger.LogWarning("Cannot sync session: device is offline");
                return;
            }

            try
            {
                _logger.LogInformation("Syncing session {SessionId}", session.SessionId);

                // Create session on server
                await ProcessSessionCreateOperationAsync(session);

                // Upload all pages
                foreach (var page in session.Pages)
                {
                    await ProcessPageAddOperationAsync(session.SessionId, page);
                }

                // Finalize session on server
                await ProcessSessionDisposeOperationAsync(session);

                _logger.LogInformation("Successfully synced session {SessionId}", session.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync session {SessionId}", session.SessionId);
                throw;
            }
        }

        public Task<SyncStatus> GetSyncStatusAsync()
        {
            return Task.FromResult(_currentStatus);
        }

        private List<OfflineOperation> SortOperationsByDependencies(List<OfflineOperation> operations)
        {
            // Sort by priority first, then by creation time
            // Ensure session_create comes before page_add, and page_add comes before session_dispose
            return operations
                .OrderBy(op => op.Priority)
                .ThenBy(op => GetOperationOrder(op.OperationType))
                .ThenBy(op => op.CreatedAt)
                .ToList();
        }

        private int GetOperationOrder(string operationType)
        {
            return operationType switch
            {
                "session_create" => 1,
                "page_add" => 2,
                "session_dispose" => 3,
                _ => 0
            };
        }

        private async Task ProcessOperationAsync(OfflineOperation operation)
        {
            switch (operation.OperationType)
            {
                case "session_create":
                    await ProcessSessionCreateOperationAsync(operation);
                    break;
                case "page_add":
                    await ProcessPageAddOperationAsync(operation);
                    break;
                case "session_dispose":
                    await ProcessSessionDisposeOperationAsync(operation);
                    break;
                default:
                    _logger.LogWarning("Unknown operation type: {OperationType}", operation.OperationType);
                    break;
            }

            // Mark operation as completed
            await _storageService.RemoveOperationAsync(operation.OperationId);
        }

        private async Task ProcessSessionCreateOperationAsync(OfflineOperation operation)
        {
            var sessionData = JsonSerializer.Deserialize<DocumentSession>(operation.Payload);
            if (sessionData != null)
            {
                await ProcessSessionCreateOperationAsync(sessionData);
            }
        }

        private async Task ProcessSessionCreateOperationAsync(DocumentSession session)
        {
            // TODO: Replace with actual API call when API service is available
            _logger.LogInformation("Creating session {SessionId} on server", session.SessionId);
            
            // Simulate API call delay
            await Task.Delay(TimeoutConstants.Testing.MockDelayMs);
            
            // Mock implementation - in real scenario, this would call the API service
            // await _apiService.CreateSessionAsync(session);
        }

        private async Task ProcessPageAddOperationAsync(OfflineOperation operation)
        {
            var pageData = JsonSerializer.Deserialize<PageAddOperation>(operation.Payload);
            if (pageData != null)
            {
                await ProcessPageAddOperationAsync(pageData.SessionId, pageData.Image);
            }
        }

        private async Task ProcessPageAddOperationAsync(string sessionId, CapturedImage image)
        {
            // TODO: Replace with actual API call when API service is available
            _logger.LogInformation("Uploading page {ImageId} for session {SessionId}", image.Id, sessionId);
            
            // Simulate API call delay  
            await Task.Delay(TimeoutConstants.UI.AnimationDelayMs);
            
            // Mock implementation - in real scenario, this would upload the image and link to session
            // await _apiService.UploadPageAsync(sessionId, image);
        }

        private async Task ProcessSessionDisposeOperationAsync(OfflineOperation operation)
        {
            var sessionData = JsonSerializer.Deserialize<DocumentSession>(operation.Payload);
            if (sessionData != null)
            {
                await ProcessSessionDisposeOperationAsync(sessionData);
            }
        }

        private async Task ProcessSessionDisposeOperationAsync(DocumentSession session)
        {
            // TODO: Replace with actual API call when API service is available
            _logger.LogInformation("Finalizing session {SessionId} on server", session.SessionId);
            
            // Simulate API call delay
            await Task.Delay(TimeoutConstants.Testing.MockDelayMs);
            
            // Mock implementation - in real scenario, this would finalize the session
            // await _apiService.FinalizeSessionAsync(session.SessionId);
        }

        private async Task HandleOperationFailureAsync(OfflineOperation operation, Exception exception)
        {
            operation.RetryCount++;

            if (operation.RetryCount < operation.MaxRetries)
            {
                _logger.LogInformation("Scheduling retry for operation {OperationId} (attempt {RetryCount}/{MaxRetries})",
                    operation.OperationId, operation.RetryCount, operation.MaxRetries);
                
                // Re-queue the operation for retry
                await _queueService.QueueOperationAsync(operation);
            }
            else
            {
                _logger.LogError("Operation {OperationId} failed after {MaxRetries} attempts",
                    operation.OperationId, operation.MaxRetries);

                // Raise conflict event for manual resolution
                SyncConflict?.Invoke(this, new SyncConflictEventArgs
                {
                    Operation = operation,
                    ConflictDescription = $"Operation failed after {operation.MaxRetries} attempts: {exception.Message}",
                    SuggestedResolution = ConflictResolutionStrategy.Skip,
                    AutoResolved = false
                });
            }
        }

        private async Task CompleteSyncAsync(DateTime startTime, bool isSuccess, int total, int successful, int failed, string? errorMessage = null)
        {
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogInformation("Sync completed. Success: {IsSuccess}, Total: {Total}, Successful: {Successful}, Failed: {Failed}, Duration: {Duration}",
                isSuccess, total, successful, failed, duration);

            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs
            {
                IsSuccess = isSuccess,
                TotalOperations = total,
                SuccessfulOperations = successful,
                FailedOperations = failed,
                SyncDuration = duration,
                ErrorMessage = errorMessage
            });

            _currentStatus.IsSyncing = false;
        }

        /// <summary>
        /// Helper class for page add operation payload
        /// </summary>
        private class PageAddOperation
        {
            public string SessionId { get; set; } = string.Empty;
            public CapturedImage Image { get; set; } = new();
        }
    }
}