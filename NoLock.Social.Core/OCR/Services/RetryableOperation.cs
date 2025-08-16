using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Interfaces;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Wrapper for executing operations with configurable retry logic.
    /// Provides a fluent API for retryable operations.
    /// </summary>
    /// <typeparam name="TResult">The type of the operation result.</typeparam>
    public class RetryableOperation<TResult>
    {
        private readonly ILogger _logger;
        private readonly IRetryPolicy _retryPolicy;
        private readonly Func<CancellationToken, Task<TResult>> _operation;
        private Action<int, Exception, int>? _onRetryCallback;
        private Action<TResult>? _onSuccessCallback;
        private Action<Exception>? _onFailureCallback;
        private string _operationName = "Operation";

        /// <summary>
        /// Initializes a new instance of the RetryableOperation class.
        /// </summary>
        /// <param name="operation">The operation to execute with retry.</param>
        /// <param name="retryPolicy">The retry policy to apply.</param>
        /// <param name="logger">Logger for operation tracking.</param>
        public RetryableOperation(
            Func<CancellationToken, Task<TResult>> operation,
            IRetryPolicy retryPolicy,
            ILogger logger)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
            _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Sets the name of the operation for logging purposes.
        /// </summary>
        /// <param name="name">The operation name.</param>
        /// <returns>The current instance for method chaining.</returns>
        public RetryableOperation<TResult> WithName(string name)
        {
            _operationName = name ?? "Operation";
            return this;
        }

        /// <summary>
        /// Sets a callback to be invoked before each retry attempt.
        /// </summary>
        /// <param name="callback">The retry callback (attempt, exception, delayMs).</param>
        /// <returns>The current instance for method chaining.</returns>
        public RetryableOperation<TResult> OnRetry(Action<int, Exception, int> callback)
        {
            _onRetryCallback = callback;
            return this;
        }

        /// <summary>
        /// Sets a callback to be invoked when the operation succeeds.
        /// </summary>
        /// <param name="callback">The success callback.</param>
        /// <returns>The current instance for method chaining.</returns>
        public RetryableOperation<TResult> OnSuccess(Action<TResult> callback)
        {
            _onSuccessCallback = callback;
            return this;
        }

        /// <summary>
        /// Sets a callback to be invoked when all retry attempts fail.
        /// </summary>
        /// <param name="callback">The failure callback.</param>
        /// <returns>The current instance for method chaining.</returns>
        public RetryableOperation<TResult> OnFailure(Action<Exception> callback)
        {
            _onFailureCallback = callback;
            return this;
        }

        /// <summary>
        /// Executes the operation with retry logic.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The result of the successful operation.</returns>
        /// <exception cref="AggregateException">Thrown when all retry attempts fail.</exception>
        public async Task<TResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Starting retryable operation '{OperationName}' with max {MaxAttempts} attempts",
                _operationName, _retryPolicy.MaxAttempts);

            try
            {
                var result = await _retryPolicy.ExecuteWithCallbackAsync(
                    _operation,
                    (attempt, exception, delayMs) =>
                    {
                        _logger.LogWarning(
                            "Operation '{OperationName}' failed on attempt {Attempt}. " +
                            "Retrying in {DelayMs}ms. Error: {ErrorMessage}",
                            _operationName, attempt, delayMs, exception.Message);
                        
                        _onRetryCallback?.Invoke(attempt, exception, delayMs);
                    },
                    cancellationToken);

                _logger.LogInformation(
                    "Operation '{OperationName}' completed successfully",
                    _operationName);

                _onSuccessCallback?.Invoke(result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Operation '{OperationName}' failed after all retry attempts",
                    _operationName);

                _onFailureCallback?.Invoke(ex);
                throw;
            }
        }
    }

    /// <summary>
    /// Factory for creating retryable operations.
    /// </summary>
    public class RetryableOperationFactory
    {
        private readonly IRetryPolicy _defaultPolicy;
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Initializes a new instance of the RetryableOperationFactory class.
        /// </summary>
        /// <param name="defaultPolicy">Default retry policy for operations.</param>
        /// <param name="loggerFactory">Logger factory for creating loggers.</param>
        public RetryableOperationFactory(
            IRetryPolicy defaultPolicy,
            ILoggerFactory loggerFactory)
        {
            _defaultPolicy = defaultPolicy ?? throw new ArgumentNullException(nameof(defaultPolicy));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <summary>
        /// Creates a new retryable operation with the default policy.
        /// </summary>
        /// <typeparam name="TResult">The type of the operation result.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <returns>A new retryable operation instance.</returns>
        public RetryableOperation<TResult> Create<TResult>(
            Func<CancellationToken, Task<TResult>> operation)
        {
            return Create(operation, _defaultPolicy);
        }

        /// <summary>
        /// Creates a new retryable operation with a custom policy.
        /// </summary>
        /// <typeparam name="TResult">The type of the operation result.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="retryPolicy">Custom retry policy to use.</param>
        /// <returns>A new retryable operation instance.</returns>
        public RetryableOperation<TResult> Create<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            IRetryPolicy retryPolicy)
        {
            var logger = _loggerFactory.CreateLogger<RetryableOperation<TResult>>();
            return new RetryableOperation<TResult>(operation, retryPolicy, logger);
        }

        /// <summary>
        /// Creates a new retryable operation for void operations.
        /// </summary>
        /// <param name="operation">The operation to execute.</param>
        /// <returns>A new retryable operation instance.</returns>
        public RetryableOperation<bool> CreateVoid(
            Func<CancellationToken, Task> operation)
        {
            return CreateVoid(operation, _defaultPolicy);
        }

        /// <summary>
        /// Creates a new retryable operation for void operations with custom policy.
        /// </summary>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="retryPolicy">Custom retry policy to use.</param>
        /// <returns>A new retryable operation instance.</returns>
        public RetryableOperation<bool> CreateVoid(
            Func<CancellationToken, Task> operation,
            IRetryPolicy retryPolicy)
        {
            // Wrap void operation to return a bool result
            async Task<bool> wrappedOperation(CancellationToken ct)
            {
                await operation(ct);
                return true;
            }

            var logger = _loggerFactory.CreateLogger<RetryableOperation<bool>>();
            return new RetryableOperation<bool>(wrappedOperation, retryPolicy, logger);
        }
    }
}