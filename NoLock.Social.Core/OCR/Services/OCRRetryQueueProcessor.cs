using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.Storage.Interfaces;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Service for processing queued OCR retry requests when connectivity is restored.
    /// Automatically retries failed requests with exponential backoff.
    /// </summary>
    public class OCRRetryQueueProcessor : IDisposable
    {
        private readonly IOCRService _ocrService;
        private readonly IFailedRequestStore _failedRequestStore;
        private readonly IConnectivityService _connectivityService;
        private readonly IRetryPolicy _retryPolicy;
        private readonly ILogger<OCRRetryQueueProcessor> _logger;
        private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
        private CancellationTokenSource? _processingCancellation;
        private bool _isProcessing = false;
        private bool _disposed = false;

        /// <summary>
        /// Event raised when retry processing starts.
        /// </summary>
        public event Action? ProcessingStarted;

        /// <summary>
        /// Event raised when retry processing completes.
        /// </summary>
        public event Action<int, int>? ProcessingCompleted;

        /// <summary>
        /// Event raised when a request is successfully retried.
        /// </summary>
        public event Action<string>? RequestSucceeded;

        /// <summary>
        /// Event raised when a request fails permanently.
        /// </summary>
        public event Action<string, string>? RequestFailed;

        /// <summary>
        /// Initializes a new instance of the OCRRetryQueueProcessor class.
        /// </summary>
        /// <param name="ocrService">The OCR service for submitting requests.</param>
        /// <param name="failedRequestStore">Store for failed requests.</param>
        /// <param name="connectivityService">Service for monitoring connectivity.</param>
        /// <param name="retryPolicy">Retry policy for failed requests.</param>
        /// <param name="logger">Logger for processor operations.</param>
        public OCRRetryQueueProcessor(
            IOCRService ocrService,
            IFailedRequestStore failedRequestStore,
            IConnectivityService connectivityService,
            IRetryPolicy retryPolicy,
            ILogger<OCRRetryQueueProcessor> logger)
        {
            _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
            _failedRequestStore = failedRequestStore ?? throw new ArgumentNullException(nameof(failedRequestStore));
            _connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
            _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts monitoring connectivity and processing retry queue when online.
        /// </summary>
        public async Task StartMonitoringAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OCRRetryQueueProcessor));

            _logger.LogInformation("Starting OCR retry queue monitoring");

            // Subscribe to connectivity events
            _connectivityService.OnOnline += OnConnectivityRestored;
            
            // Start connectivity monitoring
            await _connectivityService.StartMonitoringAsync();

            // Check if we're already online and process queue
            if (await _connectivityService.IsOnlineAsync())
            {
                await ProcessRetryQueueAsync();
            }
        }

        /// <summary>
        /// Stops monitoring connectivity and cancels any ongoing processing.
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            if (_disposed)
                return;

            _logger.LogInformation("Stopping OCR retry queue monitoring");

            // Unsubscribe from connectivity events
            _connectivityService.OnOnline -= OnConnectivityRestored;
            
            // Stop connectivity monitoring
            await _connectivityService.StopMonitoringAsync();

            // Cancel any ongoing processing
            _processingCancellation?.Cancel();
            
            // Wait for processing to complete
            await _processingSemaphore.WaitAsync();
            try
            {
                _isProcessing = false;
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }

        /// <summary>
        /// Processes all pending retry requests in the queue.
        /// </summary>
        public async Task ProcessRetryQueueAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OCRRetryQueueProcessor));

            // Prevent concurrent processing
            if (!await _processingSemaphore.WaitAsync(0))
            {
                _logger.LogDebug("Retry queue processing already in progress");
                return;
            }

            try
            {
                if (_isProcessing)
                {
                    _logger.LogDebug("Retry queue already being processed");
                    return;
                }

                _isProcessing = true;
                _processingCancellation = new CancellationTokenSource();
                
                await ProcessQueueInternalAsync(_processingCancellation.Token);
            }
            finally
            {
                _isProcessing = false;
                _processingCancellation?.Dispose();
                _processingCancellation = null;
                _processingSemaphore.Release();
            }
        }

        /// <summary>
        /// Internal method to process the retry queue.
        /// </summary>
        private async Task ProcessQueueInternalAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting OCR retry queue processing");
            ProcessingStarted?.Invoke();

            var successCount = 0;
            var failureCount = 0;

            try
            {
                // Clean up exhausted requests first
                var exhaustedCount = await _failedRequestStore.RemoveExhaustedRequestsAsync(
                    _retryPolicy.MaxAttempts, cancellationToken);
                
                if (exhaustedCount > 0)
                {
                    _logger.LogInformation(
                        "Removed {Count} exhausted requests from retry queue",
                        exhaustedCount);
                }

                // Get all retryable requests
                var requests = await _failedRequestStore.GetRetryableRequestsAsync(
                    _retryPolicy.MaxAttempts, cancellationToken);

                if (!requests.Any())
                {
                    _logger.LogInformation("No pending requests in retry queue");
                    ProcessingCompleted?.Invoke(0, 0);
                    return;
                }

                _logger.LogInformation(
                    "Processing {Count} failed OCR requests",
                    requests.Count);

                // Process each request
                foreach (var failedRequest in requests)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Retry queue processing cancelled");
                        break;
                    }

                    // Check connectivity before each request
                    if (!await _connectivityService.IsOnlineAsync())
                    {
                        _logger.LogWarning("Lost connectivity during retry processing");
                        break;
                    }

                    var success = await ProcessSingleRequestAsync(
                        failedRequest, cancellationToken);

                    if (success)
                        successCount++;
                    else
                        failureCount++;

                    // Add delay between requests to avoid overwhelming the service
                    if (requests.Count > 1)
                    {
                        await Task.Delay(500, cancellationToken);
                    }
                }

                _logger.LogInformation(
                    "Retry queue processing completed. Success: {Success}, Failed: {Failed}",
                    successCount, failureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during retry queue processing");
            }
            finally
            {
                ProcessingCompleted?.Invoke(successCount, failureCount);
            }
        }

        /// <summary>
        /// Processes a single failed request.
        /// </summary>
        private async Task<bool> ProcessSingleRequestAsync(
            FailedOCRRequest failedRequest,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Retrying OCR request. RequestId: {RequestId}, RetryCount: {RetryCount}",
                failedRequest.RequestId, failedRequest.RetryCount);

            try
            {
                // Update retry status before attempting
                await _failedRequestStore.UpdateRetryStatusAsync(
                    failedRequest.RequestId,
                    incrementRetryCount: true,
                    cancellationToken: cancellationToken);

                // Submit the request
                var response = await _ocrService.SubmitDocumentAsync(
                    failedRequest.OriginalRequest,
                    cancellationToken);

                // Check if submission was successful
                if (response != null && 
                    !response.TrackingId.StartsWith("OFFLINE-") &&
                    response.Status != OCRProcessingStatus.Failed)
                {
                    // Remove from failed store on success
                    await _failedRequestStore.RemoveRequestAsync(
                        failedRequest.RequestId,
                        cancellationToken);

                    _logger.LogInformation(
                        "Successfully retried OCR request. RequestId: {RequestId}, " +
                        "NewTrackingId: {TrackingId}",
                        failedRequest.RequestId, response.TrackingId);

                    RequestSucceeded?.Invoke(failedRequest.RequestId);
                    return true;
                }
                else
                {
                    _logger.LogWarning(
                        "OCR retry returned unsuccessful response. RequestId: {RequestId}",
                        failedRequest.RequestId);
                    
                    RequestFailed?.Invoke(
                        failedRequest.RequestId,
                        "Submission returned unsuccessful response");
                    return false;
                }
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                
                _logger.LogError(ex,
                    "Failed to retry OCR request. RequestId: {RequestId}",
                    failedRequest.RequestId);

                // Update error message in store
                await _failedRequestStore.UpdateRetryStatusAsync(
                    failedRequest.RequestId,
                    incrementRetryCount: false,
                    errorMessage: errorMessage,
                    cancellationToken: cancellationToken);

                // Check if this is a permanent failure
                var classifier = new OCRFailureClassifier(
                    _logger as ILogger<OCRFailureClassifier> ?? 
                    throw new InvalidOperationException("Logger type mismatch"));
                
                var failureType = classifier.Classify(ex);
                if (failureType == FailureType.Permanent)
                {
                    // Remove from retry queue if permanent failure
                    await _failedRequestStore.RemoveRequestAsync(
                        failedRequest.RequestId,
                        cancellationToken);
                    
                    _logger.LogWarning(
                        "Removing request due to permanent failure. RequestId: {RequestId}",
                        failedRequest.RequestId);
                }

                RequestFailed?.Invoke(failedRequest.RequestId, errorMessage);
                return false;
            }
        }

        /// <summary>
        /// Handler for when connectivity is restored.
        /// </summary>
        private async void OnConnectivityRestored(object? sender, ConnectivityEventArgs e)
        {
            _logger.LogInformation("Connectivity restored, processing retry queue");
            
            try
            {
                // Add a small delay to ensure connection is stable
                await Task.Delay(2000);
                
                // Process the retry queue
                await ProcessRetryQueueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing retry queue after connectivity restored");
            }
        }

        /// <summary>
        /// Adds a failed request to the retry queue.
        /// </summary>
        /// <param name="request">The OCR request that failed.</param>
        /// <param name="failureType">The type of failure.</param>
        /// <param name="errorMessage">Error message from the failure.</param>
        public async Task AddToRetryQueueAsync(
            OCRSubmissionRequest request,
            FailureType failureType = FailureType.Unknown,
            string? errorMessage = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OCRRetryQueueProcessor));

            var failedRequest = new FailedOCRRequest
            {
                OriginalRequest = request,
                FailureType = failureType,
                LastErrorMessage = errorMessage,
                Priority = 5 // Default priority
            };

            var requestId = await _failedRequestStore.StoreFailedRequestAsync(failedRequest);
            
            _logger.LogInformation(
                "Added request to retry queue. RequestId: {RequestId}, " +
                "ClientRequestId: {ClientRequestId}",
                requestId, request.ClientRequestId);
        }

        /// <summary>
        /// Gets the current status of the retry queue.
        /// </summary>
        public async Task<RetryQueueStatus> GetQueueStatusAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OCRRetryQueueProcessor));

            var pendingCount = await _failedRequestStore.GetPendingCountAsync();
            var retryableRequests = await _failedRequestStore.GetRetryableRequestsAsync(
                _retryPolicy.MaxAttempts);

            return new RetryQueueStatus
            {
                TotalPending = pendingCount,
                RetryableCount = retryableRequests.Count,
                IsProcessing = _isProcessing,
                IsOnline = await _connectivityService.IsOnlineAsync()
            };
        }

        /// <summary>
        /// Disposes resources used by the processor.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            // Stop monitoring
            StopMonitoringAsync().GetAwaiter().GetResult();
            
            // Dispose resources
            _processingCancellation?.Dispose();
            _processingSemaphore?.Dispose();
        }
    }

    /// <summary>
    /// Status information for the retry queue.
    /// </summary>
    public class RetryQueueStatus
    {
        /// <summary>
        /// Total number of pending requests in the queue.
        /// </summary>
        public int TotalPending { get; set; }

        /// <summary>
        /// Number of requests eligible for retry.
        /// </summary>
        public int RetryableCount { get; set; }

        /// <summary>
        /// Whether the queue is currently being processed.
        /// </summary>
        public bool IsProcessing { get; set; }

        /// <summary>
        /// Whether the system is currently online.
        /// </summary>
        public bool IsOnline { get; set; }
    }
}