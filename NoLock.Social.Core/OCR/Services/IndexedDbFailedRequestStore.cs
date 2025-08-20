using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoLock.Social.Core.Common.Extensions;
using NoLock.Social.Core.Common.Results;
using NoLock.Social.Core.Extensions;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// IndexedDB implementation of failed request store for Blazor WebAssembly.
    /// Provides persistent storage of failed OCR requests for offline retry.
    /// </summary>
    public class IndexedDbFailedRequestStore : IFailedRequestStore, IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<IndexedDbFailedRequestStore> _logger;
        private readonly string _dbName = "OCRFailedRequests";
        private readonly string _storeName = "failedRequests";
        private readonly int _dbVersion = 1;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initializationLock = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the IndexedDbFailedRequestStore class.
        /// </summary>
        /// <param name="jsRuntime">JavaScript runtime for IndexedDB operations.</param>
        /// <param name="logger">Logger for store operations.</param>
        public IndexedDbFailedRequestStore(
            IJSRuntime jsRuntime,
            ILogger<IndexedDbFailedRequestStore> logger)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Ensures the IndexedDB database is initialized.
        /// </summary>
        private async Task EnsureInitializedAsync()
        {
            if (_isInitialized)
                return;

            await _initializationLock.WaitAsync();
            try
            {
                if (_isInitialized)
                    return;

                await InitializeDbAsync();
                _isInitialized = true;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        /// <summary>
        /// Initializes the IndexedDB database and object store.
        /// </summary>
        private async Task InitializeDbAsync()
        {
            await _logger.ExecuteWithLogging(async () =>
            {
                var script = @"
                    (function() {
                        return new Promise((resolve, reject) => {
                            const request = indexedDB.open('" + _dbName + @"', " + _dbVersion + @");
                            
                            request.onerror = () => reject(request.error);
                            request.onsuccess = () => {
                                request.result.close();
                                resolve(true);
                            };
                            
                            request.onupgradeneeded = (event) => {
                                const db = event.target.result;
                                
                                if (!db.objectStoreNames.contains('" + _storeName + @"')) {
                                    const store = db.createObjectStore('" + _storeName + @"', { keyPath: 'requestId' });
                                    store.createIndex('retryCount', 'retryCount', { unique: false });
                                    store.createIndex('failedAt', 'failedAt', { unique: false });
                                    store.createIndex('priority', 'priority', { unique: false });
                                    store.createIndex('failureType', 'failureType', { unique: false });
                                }
                            };
                        });
                    })()";

                await _jsRuntime.InvokeAsync<bool>("eval", script);
                _logger.LogInformation("IndexedDB database initialized successfully");
            },
            "Failed to initialize IndexedDB database");
        }

        /// <summary>
        /// Stores a failed OCR request for later retry.
        /// </summary>
        public async Task<string> StoreFailedRequestAsync(
            FailedOCRRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            await EnsureInitializedAsync();

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                var json = JsonSerializer.Serialize(request);
                var script = @"
                    (function() {
                        return new Promise((resolve, reject) => {
                            const request = indexedDB.open('" + _dbName + @"', " + _dbVersion + @");
                            
                            request.onsuccess = () => {
                                const db = request.result;
                                const transaction = db.transaction(['" + _storeName + @"'], 'readwrite');
                                const store = transaction.objectStore('" + _storeName + @"');
                                const addRequest = store.put(" + json + @");
                                
                                addRequest.onsuccess = () => {
                                    resolve(addRequest.result);
                                    db.close();
                                };
                                
                                addRequest.onerror = () => {
                                    reject(addRequest.error);
                                    db.close();
                                };
                            };
                            
                            request.onerror = () => reject(request.error);
                        });
                    })()";

                await _jsRuntime.InvokeAsync<string>("eval", script);
                
                _logger.LogInformation(
                    "Stored failed OCR request. RequestId: {RequestId}, RetryCount: {RetryCount}",
                    request.RequestId, request.RetryCount);
                
                return request.RequestId;
            },
            $"Failed to store OCR request. RequestId: {request.RequestId}");
            
            return result.IsSuccess ? result.Value : throw new InvalidOperationException($"Failed to store OCR request. RequestId: {request.RequestId}", result.Exception);
        }

        /// <summary>
        /// Retrieves all failed requests that are eligible for retry.
        /// </summary>
        public async Task<IReadOnlyList<FailedOCRRequest>> GetRetryableRequestsAsync(
            int maxRetryCount = 5,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync();

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                var script = @"
                    (function() {
                        return new Promise((resolve, reject) => {
                            const request = indexedDB.open('" + _dbName + @"', " + _dbVersion + @");
                            
                            request.onsuccess = () => {
                                const db = request.result;
                                const transaction = db.transaction(['" + _storeName + @"'], 'readonly');
                                const store = transaction.objectStore('" + _storeName + @"');
                                const getAllRequest = store.getAll();
                                
                                getAllRequest.onsuccess = () => {
                                    const results = getAllRequest.result || [];
                                    const filtered = results.filter(r => 
                                        r.retryCount < " + maxRetryCount + @" && 
                                        r.failureType !== 'Permanent'
                                    );
                                    resolve(filtered);
                                    db.close();
                                };
                                
                                getAllRequest.onerror = () => {
                                    reject(getAllRequest.error);
                                    db.close();
                                };
                            };
                            
                            request.onerror = () => reject(request.error);
                        });
                    })()";

                var json = await _jsRuntime.InvokeAsync<string>("eval", script);
                var requests = JsonSerializer.Deserialize<List<FailedOCRRequest>>(json) ?? new List<FailedOCRRequest>();
                
                // Filter for eligible requests based on retry time
                var now = DateTime.UtcNow;
                var eligibleRequests = requests
                    .Where(r => r.IsEligibleForRetry(maxRetryCount, now))
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.FailedAt)
                    .ToList();

                _logger.LogInformation(
                    "Retrieved {Count} retryable requests from {Total} total failed requests",
                    eligibleRequests.Count, requests.Count);

                return eligibleRequests;
            },
            "Failed to retrieve retryable requests");

            return result.IsSuccess ? result.Value : new List<FailedOCRRequest>();
        }

        /// <summary>
        /// Retrieves a specific failed request by ID.
        /// </summary>
        public async Task<Result<FailedOCRRequest?>> GetRequestAsync(
            string requestId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return Result<FailedOCRRequest?>.Success(null);

            await EnsureInitializedAsync();

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                var script = @"
                    (function() {
                        return new Promise((resolve, reject) => {
                            const request = indexedDB.open('" + _dbName + @"', " + _dbVersion + @");
                            
                            request.onsuccess = () => {
                                const db = request.result;
                                const transaction = db.transaction(['" + _storeName + @"'], 'readonly');
                                const store = transaction.objectStore('" + _storeName + @"');
                                const getRequest = store.get('" + requestId + @"');
                                
                                getRequest.onsuccess = () => {
                                    resolve(getRequest.result || null);
                                    db.close();
                                };
                                
                                getRequest.onerror = () => {
                                    reject(getRequest.error);
                                    db.close();
                                };
                            };
                            
                            request.onerror = () => reject(request.error);
                        });
                    })()";

                var json = await _jsRuntime.InvokeAsync<string?>("eval", script);
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                    return (FailedOCRRequest?)null;

                return JsonSerializer.Deserialize<FailedOCRRequest>(json);
            },
            $"Failed to retrieve request. RequestId: {requestId}");
            
            return result;
        }

        /// <summary>
        /// Updates the retry count and last attempt time for a failed request.
        /// </summary>
        public async Task<bool> UpdateRetryStatusAsync(
            string requestId,
            bool incrementRetryCount = true,
            string? errorMessage = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return false;

            await EnsureInitializedAsync();

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                var requestResult = await GetRequestAsync(requestId, cancellationToken);
                if (!requestResult.IsSuccess)
                    return false;
                    
                var request = requestResult.Value;
                if (request == null)
                    return false;

                if (incrementRetryCount)
                    request.RetryCount++;
                
                request.LastRetryAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(errorMessage))
                    request.LastErrorMessage = errorMessage;

                await StoreFailedRequestAsync(request, cancellationToken);
                
                _logger.LogInformation(
                    "Updated retry status. RequestId: {RequestId}, RetryCount: {RetryCount}",
                    requestId, request.RetryCount);
                
                return true;
            },
            $"Failed to update retry status. RequestId: {requestId}");

            return result.IsSuccess ? result.Value : false;
        }

        /// <summary>
        /// Removes a request from the failed store.
        /// </summary>
        public async Task<bool> RemoveRequestAsync(
            string requestId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return false;

            await EnsureInitializedAsync();

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                var script = @"
                    (function() {
                        return new Promise((resolve, reject) => {
                            const request = indexedDB.open('" + _dbName + @"', " + _dbVersion + @");
                            
                            request.onsuccess = () => {
                                const db = request.result;
                                const transaction = db.transaction(['" + _storeName + @"'], 'readwrite');
                                const store = transaction.objectStore('" + _storeName + @"');
                                const deleteRequest = store.delete('" + requestId + @"');
                                
                                deleteRequest.onsuccess = () => {
                                    resolve(true);
                                    db.close();
                                };
                                
                                deleteRequest.onerror = () => {
                                    reject(deleteRequest.error);
                                    db.close();
                                };
                            };
                            
                            request.onerror = () => reject(request.error);
                        });
                    })()";

                await _jsRuntime.InvokeAsync<bool>("eval", script);
                
                _logger.LogInformation(
                    "Removed failed request. RequestId: {RequestId}",
                    requestId);
                
                return true;
            },
            $"Failed to remove request. RequestId: {requestId}");

            return result.IsSuccess ? result.Value : false;
        }

        /// <summary>
        /// Removes all requests that have exceeded the maximum retry count.
        /// </summary>
        public async Task<int> RemoveExhaustedRequestsAsync(
            int maxRetryCount = 5,
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync();

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                var allRequests = await GetRetryableRequestsAsync(int.MaxValue, cancellationToken);
                var exhaustedRequests = allRequests
                    .Where(r => r.RetryCount >= maxRetryCount || r.FailureType == FailureType.Permanent)
                    .ToList();

                var removedCount = 0;
                foreach (var request in exhaustedRequests)
                {
                    if (await RemoveRequestAsync(request.RequestId, cancellationToken))
                        removedCount++;
                }

                _logger.LogInformation(
                    "Removed {Count} exhausted requests",
                    removedCount);

                return removedCount;
            },
            "Failed to remove exhausted requests");

            return result.IsSuccess ? result.Value : 0;
        }

        /// <summary>
        /// Gets the count of pending failed requests.
        /// </summary>
        public async Task<int> GetPendingCountAsync(
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync();

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                var script = @"
                    (function() {
                        return new Promise((resolve, reject) => {
                            const request = indexedDB.open('" + _dbName + @"', " + _dbVersion + @");
                            
                            request.onsuccess = () => {
                                const db = request.result;
                                const transaction = db.transaction(['" + _storeName + @"'], 'readonly');
                                const store = transaction.objectStore('" + _storeName + @"');
                                const countRequest = store.count();
                                
                                countRequest.onsuccess = () => {
                                    resolve(countRequest.result);
                                    db.close();
                                };
                                
                                countRequest.onerror = () => {
                                    reject(countRequest.error);
                                    db.close();
                                };
                            };
                            
                            request.onerror = () => reject(request.error);
                        });
                    })()";

                return await _jsRuntime.InvokeAsync<int>("eval", script);
            },
            "Failed to get pending count");

            return result.IsSuccess ? result.Value : 0;
        }

        /// <summary>
        /// Clears all failed requests from the store.
        /// </summary>
        public async Task<int> ClearAllAsync(
            CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync();

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                var count = await GetPendingCountAsync(cancellationToken);
                
                var script = @"
                    (function() {
                        return new Promise((resolve, reject) => {
                            const request = indexedDB.open('" + _dbName + @"', " + _dbVersion + @");
                            
                            request.onsuccess = () => {
                                const db = request.result;
                                const transaction = db.transaction(['" + _storeName + @"'], 'readwrite');
                                const store = transaction.objectStore('" + _storeName + @"');
                                const clearRequest = store.clear();
                                
                                clearRequest.onsuccess = () => {
                                    resolve(true);
                                    db.close();
                                };
                                
                                clearRequest.onerror = () => {
                                    reject(clearRequest.error);
                                    db.close();
                                };
                            };
                            
                            request.onerror = () => reject(request.error);
                        });
                    })()";

                await _jsRuntime.InvokeAsync<bool>("eval", script);
                
                _logger.LogInformation("Cleared all {Count} failed requests", count);
                return count;
            },
            "Failed to clear all requests");

            return result.IsSuccess ? result.Value : 0;
        }

        /// <summary>
        /// Disposes resources used by the store.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            _initializationLock?.Dispose();
            await Task.CompletedTask;
        }
    }
}