using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Common.Extensions;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.Storage.Interfaces;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Decorator for OCRService that adds retry logic with exponential backoff.
    /// Handles transient failures and provides offline queue support.
    /// </summary>
    public class OCRServiceWithRetry : IOCRService
    {
        private readonly IOCRService _innerService;
        private readonly IRetryPolicy _retryPolicy;
        private readonly RetryableOperationFactory _operationFactory;
        private readonly ILogger<OCRServiceWithRetry> _logger;
        private readonly IConnectivityService? _connectivityService;
        private readonly IOfflineQueueService? _offlineQueueService;

        /// <summary>
        /// Event raised when a retry attempt is made.
        /// </summary>
        public event Action<int, Exception, int>? OnRetryAttempt;

        /// <summary>
        /// Event raised when an operation is queued for offline retry.
        /// </summary>
        public event Action<OCRSubmissionRequest>? OnQueuedForOffline;

        /// <summary>
        /// Initializes a new instance of the OCRServiceWithRetry class.
        /// </summary>
        /// <param name="innerService">The inner OCR service to wrap.</param>
        /// <param name="retryPolicy">The retry policy to apply.</param>
        /// <param name="operationFactory">Factory for creating retryable operations.</param>
        /// <param name="logger">Logger for service operations.</param>
        /// <param name="connectivityService">Optional connectivity service for offline detection.</param>
        /// <param name="offlineQueueService">Optional offline queue service for storing failed requests.</param>
        public OCRServiceWithRetry(
            IOCRService innerService,
            IRetryPolicy retryPolicy,
            RetryableOperationFactory operationFactory,
            ILogger<OCRServiceWithRetry> logger,
            IConnectivityService? connectivityService = null,
            IOfflineQueueService? offlineQueueService = null)
        {
            _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
            _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
            _operationFactory = operationFactory ?? throw new ArgumentNullException(nameof(operationFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectivityService = connectivityService;
            _offlineQueueService = offlineQueueService;
        }

        /// <summary>
        /// Submits a document for OCR processing with automatic retry on transient failures.
        /// </summary>
        /// <param name="request">The OCR submission request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The OCR submission response.</returns>
        public async Task<OCRSubmissionResponse> SubmitDocumentAsync(
            OCRSubmissionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // Check connectivity before attempting
            if (_connectivityService != null && !await _connectivityService.IsOnlineAsync())
            {
                _logger.LogWarning(
                    "System is offline. Queueing OCR request for later submission. " +
                    "ClientRequestId: {ClientRequestId}",
                    request.ClientRequestId);

                await QueueForOfflineRetryAsync(request);
                
                // Return a placeholder response indicating offline queue
                return new OCRSubmissionResponse
                {
                    TrackingId = $"OFFLINE-{Guid.NewGuid():N}",
                    Status = OCRProcessingStatus.Queued,
                    SubmittedAt = DateTime.UtcNow,
                    EstimatedCompletionTime = null
                };
            }

            try
            {
                // Create retryable operation for document submission
                var operation = _operationFactory
                    .Create(ct => _innerService.SubmitDocumentAsync(request, ct))
                    .WithName($"OCR Submit - {request.ClientRequestId}")
                    .OnRetry((attempt, ex, delayMs) =>
                    {
                        _logger.LogWarning(
                            "OCR submission retry attempt {Attempt} for ClientRequestId: {ClientRequestId}. " +
                            "Waiting {DelayMs}ms. Error: {ErrorMessage}",
                            attempt, request.ClientRequestId, delayMs, ex.Message);
                        
                        OnRetryAttempt?.Invoke(attempt, ex, delayMs);
                    })
                    .OnFailure(async ex =>
                    {
                        _logger.LogError(ex,
                            "OCR submission failed after all retry attempts for ClientRequestId: {ClientRequestId}",
                            request.ClientRequestId);

                        // Queue for offline retry if available
                        if (_offlineQueueService != null)
                        {
                            await QueueForOfflineRetryAsync(request);
                        }
                    });

                return await operation.ExecuteAsync(cancellationToken);
            }
            catch (AggregateException aggEx)
            {
                // Check if we should queue for offline retry
                if (ShouldQueueForOffline(aggEx))
                {
                    await QueueForOfflineRetryAsync(request);
                    
                    // Return offline queued response
                    return new OCRSubmissionResponse
                    {
                        TrackingId = $"OFFLINE-{Guid.NewGuid():N}",
                        Status = OCRProcessingStatus.Failed,
                        SubmittedAt = DateTime.UtcNow,
                        EstimatedCompletionTime = null
                    };
                }
                
                throw;
            }
        }

        /// <summary>
        /// Gets the processing status with automatic retry on transient failures.
        /// </summary>
        /// <param name="trackingId">The tracking ID of the OCR submission.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The OCR status response.</returns>
        public async Task<OCRStatusResponse> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(trackingId))
                throw new ArgumentNullException(nameof(trackingId));

            // Handle offline tracking IDs
            if (trackingId.StartsWith("OFFLINE-"))
            {
                return new OCRStatusResponse
                {
                    TrackingId = trackingId,
                    Status = OCRProcessingStatus.Queued,
                    ProgressPercentage = 0,
                    SubmittedAt = DateTime.UtcNow,
                    StatusMessage = "Request is queued for processing when connection is restored",
                    QueuePosition = null
                };
            }

            // Create retryable operation for status check
            var operation = _operationFactory
                .Create(ct => _innerService.GetStatusAsync(trackingId, ct))
                .WithName($"OCR Status - {trackingId}")
                .OnRetry((attempt, ex, delayMs) =>
                {
                    _logger.LogDebug(
                        "OCR status check retry attempt {Attempt} for TrackingId: {TrackingId}. " +
                        "Waiting {DelayMs}ms",
                        attempt, trackingId, delayMs);
                    
                    OnRetryAttempt?.Invoke(attempt, ex, delayMs);
                });

            return await operation.ExecuteAsync(cancellationToken);
        }

        /// <summary>
        /// Queues a request for offline retry.
        /// </summary>
        private async Task QueueForOfflineRetryAsync(OCRSubmissionRequest request)
        {
            if (_offlineQueueService == null)
            {
                _logger.LogWarning(
                    "Offline queue service not available. Cannot queue request for ClientRequestId: {ClientRequestId}",
                    request.ClientRequestId);
                return;
            }

            var result = await _logger.ExecuteWithLogging(
                async () =>
                {
                    var operation = new OfflineOperation
                    {
                        OperationType = "OCR",
                        Payload = System.Text.Json.JsonSerializer.Serialize(request),
                        Priority = 1,
                        MaxRetries = 5
                    };
                    
                    await _offlineQueueService.QueueOperationAsync(operation);
                    
                    _logger.LogInformation(
                        "OCR request queued for offline retry. ClientRequestId: {ClientRequestId}",
                        request.ClientRequestId);
                    
                    OnQueuedForOffline?.Invoke(request);
                },
                $"Queue OCR request for offline retry. ClientRequestId: {request.ClientRequestId}");
            
            if (result.IsFailure)
            {
                _logger.LogError(result.Exception,
                    "Failed to queue OCR request for offline retry. ClientRequestId: {ClientRequestId}",
                    request.ClientRequestId);
            }
        }

        /// <summary>
        /// Determines if an exception indicates the request should be queued for offline retry.
        /// </summary>
        private bool ShouldQueueForOffline(AggregateException exception)
        {
            // Check if all inner exceptions are network-related
            foreach (var innerEx in exception.InnerExceptions)
            {
                if (innerEx is HttpRequestException ||
                    innerEx is TaskCanceledException ||
                    innerEx is TimeoutException)
                {
                    continue;
                }
                
                // If any exception is not network-related, don't queue
                return false;
            }
            
            return true;
        }
    }

    /// <summary>
    /// Configuration for OCR retry behavior.
    /// </summary>
    public class OCRRetryConfiguration
    {
        /// <summary>
        /// Maximum number of retry attempts for submissions.
        /// Default: 3
        /// </summary>
        public int MaxSubmissionAttempts { get; set; } = 3;

        /// <summary>
        /// Maximum number of retry attempts for status checks.
        /// Default: 2
        /// </summary>
        public int MaxStatusCheckAttempts { get; set; } = 2;

        /// <summary>
        /// Initial delay between retries in milliseconds.
        /// Default: 1000ms
        /// </summary>
        public int InitialDelayMs { get; set; } = 1000;

        /// <summary>
        /// Maximum delay between retries in milliseconds.
        /// Default: 30000ms (30 seconds)
        /// </summary>
        public int MaxDelayMs { get; set; } = 30000;

        /// <summary>
        /// Backoff multiplier for exponential backoff.
        /// Default: 2.0
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Whether to queue failed requests for offline retry.
        /// Default: true
        /// </summary>
        public bool EnableOfflineQueue { get; set; } = true;

        /// <summary>
        /// Creates a retry policy for OCR submissions.
        /// </summary>
        public IRetryPolicy CreateSubmissionPolicy(
            ILogger logger,
            IFailureClassifier classifier)
        {
            return new ExponentialBackoffRetryPolicy(
                logger as ILogger<ExponentialBackoffRetryPolicy> ?? 
                    throw new ArgumentException("Logger type mismatch", nameof(logger)),
                classifier,
                MaxSubmissionAttempts,
                InitialDelayMs,
                MaxDelayMs,
                BackoffMultiplier);
        }

        /// <summary>
        /// Creates a retry policy for OCR status checks.
        /// </summary>
        public IRetryPolicy CreateStatusCheckPolicy(
            ILogger logger,
            IFailureClassifier classifier)
        {
            return new ExponentialBackoffRetryPolicy(
                logger as ILogger<ExponentialBackoffRetryPolicy> ?? 
                    throw new ArgumentException("Logger type mismatch", nameof(logger)),
                classifier,
                MaxStatusCheckAttempts,
                InitialDelayMs / 2, // Status checks use shorter initial delay
                MaxDelayMs / 2,
                BackoffMultiplier);
        }
    }
}