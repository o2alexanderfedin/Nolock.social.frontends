using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Common.Constants;
using NoLock.Social.Core.OCR.Interfaces;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Implementation of retry policy with exponential backoff strategy.
    /// Thread-safe for use in Blazor WebAssembly single-threaded environment.
    /// </summary>
    public class ExponentialBackoffRetryPolicy : IRetryPolicy
    {
        private readonly ILogger<ExponentialBackoffRetryPolicy> _logger;
        private readonly IFailureClassifier _failureClassifier;
        private readonly Random _jitter = new Random();

        /// <summary>
        /// Gets the maximum number of retry attempts.
        /// </summary>
        public int MaxAttempts { get; }

        /// <summary>
        /// Gets the initial delay between retry attempts in milliseconds.
        /// </summary>
        public int InitialDelayMs { get; }

        /// <summary>
        /// Gets the maximum delay between retry attempts in milliseconds.
        /// </summary>
        public int MaxDelayMs { get; }

        /// <summary>
        /// Gets the backoff multiplier for exponential backoff.
        /// </summary>
        public double BackoffMultiplier { get; }

        /// <summary>
        /// Initializes a new instance of the ExponentialBackoffRetryPolicy class.
        /// </summary>
        /// <param name="logger">Logger for retry operations.</param>
        /// <param name="failureClassifier">Classifier for determining failure types.</param>
        /// <param name="maxAttempts">Maximum number of retry attempts (default: RetryPolicyConstants.DefaultMaxRetryAttempts).</param>
        /// <param name="initialDelayMs">Initial delay in milliseconds (default: RetryPolicyConstants.DefaultInitialDelayMs).</param>
        /// <param name="maxDelayMs">Maximum delay in milliseconds (default: RetryPolicyConstants.DefaultMaxDelayMs).</param>
        /// <param name="backoffMultiplier">Backoff multiplier (default: RetryPolicyConstants.DefaultBackoffMultiplier).</param>
        public ExponentialBackoffRetryPolicy(
            ILogger<ExponentialBackoffRetryPolicy> logger,
            IFailureClassifier failureClassifier,
            int maxAttempts = RetryPolicyConstants.DefaultMaxRetryAttempts,
            int initialDelayMs = RetryPolicyConstants.DefaultInitialDelayMs,
            int maxDelayMs = RetryPolicyConstants.DefaultMaxDelayMs,
            double backoffMultiplier = RetryPolicyConstants.DefaultBackoffMultiplier)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _failureClassifier = failureClassifier ?? throw new ArgumentNullException(nameof(failureClassifier));
            
            if (maxAttempts <= 0)
                throw new ArgumentException("Max attempts must be greater than 0", nameof(maxAttempts));
            if (initialDelayMs <= 0)
                throw new ArgumentException("Initial delay must be greater than 0", nameof(initialDelayMs));
            if (maxDelayMs < initialDelayMs)
                throw new ArgumentException("Max delay must be greater than or equal to initial delay", nameof(maxDelayMs));
            if (backoffMultiplier < 1.0)
                throw new ArgumentException("Backoff multiplier must be at least 1.0", nameof(backoffMultiplier));

            MaxAttempts = maxAttempts;
            InitialDelayMs = initialDelayMs;
            MaxDelayMs = maxDelayMs;
            BackoffMultiplier = backoffMultiplier;
        }

        /// <summary>
        /// Determines if an exception should trigger a retry.
        /// </summary>
        /// <param name="exception">The exception to evaluate.</param>
        /// <returns>True if the operation should be retried; otherwise, false.</returns>
        public bool ShouldRetry(Exception exception)
        {
            if (exception == null)
                return false;

            var failureType = _failureClassifier.Classify(exception);
            
            _logger.LogDebug(
                "Exception classified as {FailureType}: {ExceptionType} - {Message}",
                failureType, exception.GetType().Name, exception.Message);

            return failureType == FailureType.Transient || failureType == FailureType.Unknown;
        }

        /// <summary>
        /// Calculates the delay before the next retry attempt with jitter.
        /// </summary>
        /// <param name="attemptNumber">The current attempt number (1-based).</param>
        /// <returns>The delay in milliseconds before the next retry.</returns>
        public int CalculateDelay(int attemptNumber)
        {
            if (attemptNumber <= 0)
                return InitialDelayMs;

            // Calculate exponential backoff
            var exponentialDelay = InitialDelayMs * Math.Pow(BackoffMultiplier, attemptNumber - 1);
            
            // Apply max delay cap
            var cappedDelay = Math.Min(exponentialDelay, MaxDelayMs);
            
            // Add jitter (±20% randomization to avoid thundering herd)
            var jitterRange = cappedDelay * RetryPolicyConstants.JitterPercentage;
            var jitter = (_jitter.NextDouble() - RetryPolicyConstants.JitterCenterValue) * RetryPolicyConstants.JitterRangeMultiplier * jitterRange;
            
            return Math.Max(RetryPolicyConstants.MinimumDelayMs, (int)(cappedDelay + jitter));
        }

        /// <summary>
        /// Executes an operation with retry logic based on the policy.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The result of the successful operation.</returns>
        public async Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteWithCallbackAsync(
                operation,
                null, // No callback
                cancellationToken);
        }

        /// <summary>
        /// Executes an operation with retry logic and progress reporting.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="onRetry">Callback invoked before each retry attempt.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The result of the successful operation.</returns>
        public async Task<TResult> ExecuteWithCallbackAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            Action<int, Exception, int>? onRetry,
            CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var exceptions = new List<Exception>();
            
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    _logger.LogDebug(
                        "Executing operation, attempt {Attempt} of {MaxAttempts}",
                        attempt, MaxAttempts);

                    var result = await operation(cancellationToken);
                    
                    if (attempt > 1)
                    {
                        _logger.LogInformation(
                            "Operation succeeded after {Attempts} attempts",
                            attempt);
                    }
                    
                    return result;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Operation cancelled during attempt {Attempt}", attempt);
                    throw;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    
                    // Check if we should retry
                    if (!ShouldRetry(ex))
                    {
                        _logger.LogWarning(
                            "Exception classified as permanent failure, not retrying: {ExceptionType} - {Message}",
                            ex.GetType().Name, ex.Message);
                        throw;
                    }
                    
                    // Check if we have more attempts
                    if (attempt >= MaxAttempts)
                    {
                        _logger.LogError(
                            "Operation failed after {MaxAttempts} attempts. Last error: {ExceptionType} - {Message}",
                            MaxAttempts, ex.GetType().Name, ex.Message);
                        throw new AggregateException(
                            $"Operation failed after {MaxAttempts} retry attempts",
                            exceptions);
                    }
                    
                    // Calculate delay for next attempt
                    var delayMs = CalculateDelay(attempt);
                    
                    _logger.LogWarning(
                        "Operation failed on attempt {Attempt} of {MaxAttempts}. " +
                        "Retrying in {DelayMs}ms. Error: {ExceptionType} - {Message}",
                        attempt, MaxAttempts, delayMs, ex.GetType().Name, ex.Message);
                    
                    // Invoke callback if provided
                    onRetry?.Invoke(attempt, ex, delayMs);
                    
                    // Wait before retry
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
            
            // This should never be reached due to the logic above
            throw new InvalidOperationException("Retry logic failed unexpectedly");
        }
    }

    /// <summary>
    /// Default implementation of failure classifier for OCR operations.
    /// </summary>
    public class OCRFailureClassifier : IFailureClassifier
    {
        private readonly ILogger<OCRFailureClassifier> _logger;

        /// <summary>
        /// HTTP status codes that indicate transient failures.
        /// </summary>
        private static readonly HashSet<HttpStatusCode> TransientHttpStatusCodes = new HashSet<HttpStatusCode>
        {
            (HttpStatusCode)HttpStatusConstants.ClientError.RequestTimeout,       // 408
            (HttpStatusCode)HttpStatusConstants.ClientError.TooManyRequests,      // 429
            (HttpStatusCode)HttpStatusConstants.ServerError.InternalServerError,  // 500
            (HttpStatusCode)HttpStatusConstants.ServerError.BadGateway,           // 502
            (HttpStatusCode)HttpStatusConstants.ServerError.ServiceUnavailable,   // 503
            (HttpStatusCode)HttpStatusConstants.ServerError.GatewayTimeout        // 504
        };

        /// <summary>
        /// HTTP status codes that indicate permanent failures.
        /// </summary>
        private static readonly HashSet<HttpStatusCode> PermanentHttpStatusCodes = new HashSet<HttpStatusCode>
        {
            (HttpStatusCode)HttpStatusConstants.ClientError.BadRequest,           // 400
            (HttpStatusCode)HttpStatusConstants.ClientError.Unauthorized,         // 401
            (HttpStatusCode)HttpStatusConstants.ClientError.Forbidden,            // 403
            (HttpStatusCode)HttpStatusConstants.ClientError.NotFound,             // 404
            (HttpStatusCode)HttpStatusConstants.ClientError.MethodNotAllowed,     // 405
            (HttpStatusCode)HttpStatusConstants.ClientError.NotAcceptable,        // 406
            (HttpStatusCode)HttpStatusConstants.ClientError.Conflict,             // 409
            (HttpStatusCode)HttpStatusConstants.ClientError.Gone,                 // 410
            (HttpStatusCode)HttpStatusConstants.ClientError.UnprocessableEntity,  // 422
            (HttpStatusCode)HttpStatusConstants.ServerError.NotImplemented        // 501
        };

        /// <summary>
        /// Initializes a new instance of the OCRFailureClassifier class.
        /// </summary>
        /// <param name="logger">Logger for classification operations.</param>
        public OCRFailureClassifier(ILogger<OCRFailureClassifier> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Classifies an exception to determine if it represents a transient or permanent failure.
        /// </summary>
        /// <param name="exception">The exception to classify.</param>
        /// <returns>The classification of the failure.</returns>
        public FailureType Classify(Exception exception)
        {
            if (exception == null)
                return FailureType.Unknown;

            // Check for HTTP exceptions
            if (exception is HttpRequestException httpEx)
            {
                return ClassifyHttpException(httpEx);
            }

            // Check for OCR service exceptions
            if (exception is OCRServiceException ocrEx)
            {
                return ClassifyOCRException(ocrEx);
            }

            // Check for timeout exceptions
            if (exception is TimeoutException || exception is TaskCanceledException)
            {
                _logger.LogDebug("Timeout/cancellation classified as transient");
                return FailureType.Transient;
            }

            // Check for argument exceptions (usually permanent)
            if (exception is ArgumentException || exception is InvalidOperationException)
            {
                _logger.LogDebug("Argument/InvalidOperation exception classified as permanent");
                return FailureType.Permanent;
            }

            // Check inner exception if present
            if (exception.InnerException != null)
            {
                var innerClassification = Classify(exception.InnerException);
                if (innerClassification != FailureType.Unknown)
                {
                    _logger.LogDebug(
                        "Using inner exception classification: {Classification}",
                        innerClassification);
                    return innerClassification;
                }
            }

            // Default to unknown for unrecognized exceptions
            _logger.LogDebug(
                "Unknown exception type {ExceptionType}, defaulting to Unknown classification",
                exception.GetType().Name);
            return FailureType.Unknown;
        }

        /// <summary>
        /// Classifies HTTP request exceptions based on status codes.
        /// </summary>
        private FailureType ClassifyHttpException(HttpRequestException exception)
        {
            // Try to extract status code from message (Blazor WebAssembly limitation)
            var message = exception.Message;
            
            // Common patterns in HTTP exception messages
            if (message.Contains(HttpStatusConstants.StatusStrings.TooManyRequests) || message.Contains("Too Many Requests"))
            {
                _logger.LogDebug("HTTP {StatusCode} classified as transient", HttpStatusConstants.ClientError.TooManyRequests);
                return FailureType.Transient;
            }
            
            if (message.Contains(HttpStatusConstants.StatusStrings.ServiceUnavailable) || message.Contains("Service Unavailable"))
            {
                _logger.LogDebug("HTTP {StatusCode} classified as transient", HttpStatusConstants.ServerError.ServiceUnavailable);
                return FailureType.Transient;
            }
            
            if (message.Contains(HttpStatusConstants.StatusStrings.GatewayTimeout) || message.Contains("Gateway Timeout"))
            {
                _logger.LogDebug("HTTP {StatusCode} classified as transient", HttpStatusConstants.ServerError.GatewayTimeout);
                return FailureType.Transient;
            }
            
            if (message.Contains(HttpStatusConstants.StatusStrings.BadRequest) || message.Contains("Bad Request"))
            {
                _logger.LogDebug("HTTP {StatusCode} classified as permanent", HttpStatusConstants.ClientError.BadRequest);
                return FailureType.Permanent;
            }
            
            if (message.Contains(HttpStatusConstants.StatusStrings.Unauthorized) || message.Contains("Unauthorized"))
            {
                _logger.LogDebug("HTTP {StatusCode} classified as permanent", HttpStatusConstants.ClientError.Unauthorized);
                return FailureType.Permanent;
            }
            
            if (message.Contains(HttpStatusConstants.StatusStrings.NotFound) || message.Contains("Not Found"))
            {
                _logger.LogDebug("HTTP {StatusCode} classified as permanent", HttpStatusConstants.ClientError.NotFound);
                return FailureType.Permanent;
            }

            // Network-related errors are usually transient
            if (message.Contains("network") || message.Contains("connection") || 
                message.Contains("timeout") || message.Contains("socket"))
            {
                _logger.LogDebug("Network error classified as transient");
                return FailureType.Transient;
            }

            _logger.LogDebug("HTTP exception classified as unknown: {Message}", message);
            return FailureType.Unknown;
        }

        /// <summary>
        /// Classifies OCR service exceptions.
        /// </summary>
        private FailureType ClassifyOCRException(OCRServiceException exception)
        {
            // Check inner exception first
            if (exception.InnerException != null)
            {
                var innerClassification = Classify(exception.InnerException);
                if (innerClassification != FailureType.Unknown)
                    return innerClassification;
            }

            // Check for specific OCR error patterns
            var message = exception.Message.ToLowerInvariant();
            
            if (message.Contains("invalid") || message.Contains("unsupported") || 
                message.Contains("required"))
            {
                _logger.LogDebug("OCR validation error classified as permanent");
                return FailureType.Permanent;
            }
            
            if (message.Contains("timeout") || message.Contains("unavailable") || 
                message.Contains("busy"))
            {
                _logger.LogDebug("OCR availability error classified as transient");
                return FailureType.Transient;
            }

            return FailureType.Unknown;
        }
    }
}