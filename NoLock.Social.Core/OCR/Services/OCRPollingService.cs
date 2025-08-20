using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NoLock.Social.Core.Common.Extensions;
using NoLock.Social.Core.OCR.Configuration;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Implementation of OCR polling service with automatic result retrieval and cancellation support.
    /// Thread-safe for use in Blazor WebAssembly environment.
    /// </summary>
    public class OCRPollingService : IOCRPollingService
    {
        private readonly IOCRService _ocrService;
        private readonly IPollingService<OCRStatusResponse> _pollingService;
        private readonly ILogger<OCRPollingService> _logger;
        private readonly OCRServiceOptions _options;
        private readonly IWakeLockService _wakeLockService;

        /// <summary>
        /// Initializes a new instance of the OCRPollingService class.
        /// </summary>
        /// <param name="ocrService">The OCR service for status checks.</param>
        /// <param name="pollingService">The generic polling service.</param>
        /// <param name="logger">Logger for service operations.</param>
        /// <param name="options">OCR service configuration options.</param>
        /// <param name="wakeLockService">The wake lock service to prevent device sleep during processing.</param>
        public OCRPollingService(
            IOCRService ocrService,
            IPollingService<OCRStatusResponse> pollingService,
            ILogger<OCRPollingService> logger,
            IOptions<OCRServiceOptions> options,
            IWakeLockService wakeLockService = null)
        {
            _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
            _pollingService = pollingService ?? throw new ArgumentNullException(nameof(pollingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _wakeLockService = wakeLockService; // Optional service - null if not available
        }

        /// <summary>
        /// Polls the OCR service for processing status until completion or timeout.
        /// </summary>
        /// <param name="trackingId">The unique tracking identifier for the OCR submission.</param>
        /// <param name="cancellationToken">Cancellation token for the polling operation.</param>
        /// <returns>The final OCR status with complete results.</returns>
        public async Task<OCRStatusResponse> PollForCompletionAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            return await PollForCompletionWithConfigurationAsync(
                trackingId,
                GetDefaultPollingConfiguration(),
                cancellationToken);
        }

        /// <summary>
        /// Polls the OCR service with progress updates until completion or timeout.
        /// </summary>
        /// <param name="trackingId">The unique tracking identifier for the OCR submission.</param>
        /// <param name="progressCallback">Callback invoked with status updates during polling.</param>
        /// <param name="cancellationToken">Cancellation token for the polling operation.</param>
        /// <returns>The final OCR status with complete results.</returns>
        public async Task<OCRStatusResponse> PollForCompletionWithProgressAsync(
            string trackingId,
            Action<OCRStatusUpdate> progressCallback,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(trackingId))
                throw new ArgumentNullException(nameof(trackingId));

            var configuration = GetDefaultPollingConfiguration();
            var stopwatch = Stopwatch.StartNew();
            var attemptCount = 0;

            _logger.LogInformation(
                "Starting OCR polling with progress for TrackingId: {TrackingId}",
                trackingId);

            // Acquire wake lock to prevent device sleep during processing
            bool wakeLockAcquired = await TryAcquireWakeLockAsync("OCR Polling with Progress", trackingId);

            try
            {
                var result = await _pollingService.PollWithProgressAsync(
                    async (ct) =>
                    {
                        attemptCount++;
                        return await _ocrService.GetStatusAsync(trackingId, ct);
                    },
                    IsOperationComplete,
                    (response) =>
                    {
                        // Convert response to status update and invoke callback
                        if (progressCallback != null)
                        {
                            var update = OCRStatusUpdate.FromResponse(
                                response,
                                attemptCount,
                                stopwatch.Elapsed);
                            
                            _logger.LogDebug(
                                "Progress update for TrackingId: {TrackingId}, " +
                                "Status: {Status}, Progress: {Progress}%",
                                trackingId, update.Status, update.ProgressPercentage);
                            
                            progressCallback(update);
                        }
                    },
                    configuration,
                    cancellationToken);

                _logger.LogInformation(
                    "OCR polling completed successfully for TrackingId: {TrackingId}, " +
                    "Final status: {Status}, Duration: {Duration}s",
                    trackingId, result.Status, stopwatch.Elapsed.TotalSeconds);

                return result;
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex,
                    "OCR polling timed out for TrackingId: {TrackingId} after {Duration}s",
                    trackingId, stopwatch.Elapsed.TotalSeconds);
                throw new OCRProcessingException(
                    $"OCR processing timed out after {configuration.MaxPollingDurationSeconds} seconds",
                    trackingId);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogInformation(
                    "OCR polling cancelled for TrackingId: {TrackingId} after {Duration}s",
                    trackingId, stopwatch.Elapsed.TotalSeconds);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "OCR polling failed for TrackingId: {TrackingId} after {Duration}s",
                    trackingId, stopwatch.Elapsed.TotalSeconds);
                throw new OCRProcessingException(
                    "OCR polling failed due to an unexpected error",
                    trackingId,
                    ex);
            }
            finally
            {
                // Always release wake lock when polling completes, regardless of success/failure
                if (wakeLockAcquired)
                {
                    await TryReleaseWakeLockAsync(trackingId);
                }
            }
        }

        /// <summary>
        /// Polls the OCR service with custom configuration until completion or timeout.
        /// </summary>
        /// <param name="trackingId">The unique tracking identifier for the OCR submission.</param>
        /// <param name="configuration">Custom polling configuration to use.</param>
        /// <param name="cancellationToken">Cancellation token for the polling operation.</param>
        /// <returns>The final OCR status with complete results.</returns>
        public async Task<OCRStatusResponse> PollForCompletionWithConfigurationAsync(
            string trackingId,
            PollingConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(trackingId))
                throw new ArgumentNullException(nameof(trackingId));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _logger.LogInformation(
                "Starting OCR polling for TrackingId: {TrackingId} with custom configuration",
                trackingId);

            // Acquire wake lock to prevent device sleep during processing
            bool wakeLockAcquired = await TryAcquireWakeLockAsync("OCR Polling with Configuration", trackingId);

            try
            {
                var result = await _pollingService.PollAsync(
                    async (ct) => await _ocrService.GetStatusAsync(trackingId, ct),
                    IsOperationComplete,
                    configuration,
                    cancellationToken);

                // Check if processing failed
                if (result.Status == OCRProcessingStatus.Failed)
                {
                    _logger.LogError(
                        "OCR processing failed for TrackingId: {TrackingId}, " +
                        "Error: {ErrorMessage}, Code: {ErrorCode}",
                        trackingId, result.ErrorMessage, result.ErrorCode);
                    
                    throw new OCRProcessingException(
                        result.ErrorMessage ?? "OCR processing failed",
                        trackingId,
                        result.ErrorCode,
                        OCRProcessingStatus.Failed);
                }

                _logger.LogInformation(
                    "OCR polling completed for TrackingId: {TrackingId}, Status: {Status}",
                    trackingId, result.Status);

                return result;
            }
            catch (TimeoutException)
            {
                throw new OCRProcessingException(
                    $"OCR processing timed out after {configuration.MaxPollingDurationSeconds} seconds",
                    trackingId);
            }
            catch (OCRProcessingException)
            {
                throw; // Re-throw OCR-specific exceptions
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                throw new OCRProcessingException(
                    "OCR polling failed due to an unexpected error",
                    trackingId,
                    ex);
            }
            finally
            {
                // Always release wake lock when polling completes, regardless of success/failure
                if (wakeLockAcquired)
                {
                    await TryReleaseWakeLockAsync(trackingId);
                }
            }
        }

        /// <summary>
        /// Attempts to cancel an ongoing OCR processing operation.
        /// </summary>
        /// <param name="trackingId">The unique tracking identifier for the OCR submission.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>True if cancellation was successful, false otherwise.</returns>
        public async Task<bool> CancelProcessingAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(trackingId))
                throw new ArgumentNullException(nameof(trackingId));

            try
            {
                _logger.LogInformation(
                    "Attempting to cancel OCR processing for TrackingId: {TrackingId}",
                    trackingId);

                // First, check the current status
                var status = await _ocrService.GetStatusAsync(trackingId, cancellationToken);

                if (!status.IsCancellable)
                {
                    _logger.LogWarning(
                        "Cannot cancel OCR processing for TrackingId: {TrackingId}, " +
                        "Current status: {Status}",
                        trackingId, status.Status);
                    
                    throw new InvalidOperationException(
                        $"OCR processing cannot be cancelled in status: {status.Status}");
                }

                // TODO: Implement actual cancellation API call when available
                // For now, return true for mock implementation
                _logger.LogInformation(
                    "OCR processing cancellation requested for TrackingId: {TrackingId}",
                    trackingId);

                // Simulate cancellation delay
                await Task.Delay(100, cancellationToken);

                return true;
            }
            catch (InvalidOperationException)
            {
                throw; // Re-throw invalid operation exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to cancel OCR processing for TrackingId: {TrackingId}",
                    trackingId);
                return false;
            }
        }

        /// <summary>
        /// Determines if the OCR operation is complete based on the status response.
        /// </summary>
        /// <param name="response">The OCR status response to check.</param>
        /// <returns>True if the operation is complete (success or failure), false otherwise.</returns>
        private bool IsOperationComplete(OCRStatusResponse response)
        {
            return response.Status == OCRProcessingStatus.Complete ||
                   response.Status == OCRProcessingStatus.Failed;
        }

        /// <summary>
        /// Gets the default polling configuration from options or uses OCR defaults.
        /// </summary>
        /// <returns>The polling configuration to use.</returns>
        private PollingConfiguration GetDefaultPollingConfiguration()
        {
            // Use configuration from options if available, otherwise use OCR defaults
            if (_options.PollingConfiguration != null)
            {
                return _options.PollingConfiguration;
            }

            return PollingConfiguration.OCRDefault;
        }

        /// <summary>
        /// Attempts to acquire a wake lock, logging any failures without throwing.
        /// </summary>
        /// <param name="reason">The reason for acquiring the wake lock.</param>
        /// <param name="trackingId">The tracking ID for logging context.</param>
        /// <returns>True if wake lock was successfully acquired, false otherwise.</returns>
        private async Task<bool> TryAcquireWakeLockAsync(string reason, string trackingId)
        {
            if (_wakeLockService == null)
                return false;

            var result = await _logger.ExecuteWithLogging(
                async () => await _wakeLockService.AcquireWakeLockAsync(reason),
                $"Wake lock acquisition for OCR polling: {trackingId}");

            if (result.IsSuccess)
            {
                if (result.Value.IsSuccess)
                {
                    _logger.LogDebug("Wake lock acquired for OCR polling: {TrackingId}", trackingId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to acquire wake lock for OCR polling: {TrackingId}, Error: {Error}",
                        trackingId, result.Value.ErrorMessage);
                    return false;
                }
            }
            else
            {
                _logger.LogWarning(result.Exception, "Wake lock acquisition failed for OCR polling: {TrackingId}", trackingId);
                return false;
            }
        }

        /// <summary>
        /// Attempts to release a wake lock, logging any failures without throwing.
        /// </summary>
        /// <param name="trackingId">The tracking ID for logging context.</param>
        private async Task TryReleaseWakeLockAsync(string trackingId)
        {
            if (_wakeLockService == null)
                return;

            var result = await _logger.ExecuteWithLogging(
                async () => await _wakeLockService.ReleaseWakeLockAsync(),
                $"Wake lock release after OCR polling: {trackingId}");

            if (result.IsSuccess)
            {
                if (result.Value.IsSuccess)
                {
                    _logger.LogDebug("Wake lock released after OCR polling: {TrackingId}", trackingId);
                }
                else
                {
                    _logger.LogWarning("Failed to release wake lock after OCR polling: {TrackingId}, Error: {Error}",
                        trackingId, result.Value.ErrorMessage);
                }
            }
            else
            {
                _logger.LogWarning(result.Exception, "Wake lock release failed after OCR polling: {TrackingId}", trackingId);
            }
        }
    }
}