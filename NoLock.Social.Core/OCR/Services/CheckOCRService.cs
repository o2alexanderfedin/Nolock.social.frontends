using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Generated;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.Storage.Interfaces;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// OCR service specifically for processing check documents with CAS integration
    /// </summary>
    public class CheckOCRService : IOCRService
    {
        private readonly MistralOCRClient _ocrClient;
        private readonly ICASService _casService;
        private readonly ILogger<CheckOCRService> _logger;

        public CheckOCRService(
            MistralOCRClient ocrClient, 
            ICASService casService,
            ILogger<CheckOCRService> logger)
        {
            _ocrClient = ocrClient ?? throw new ArgumentNullException(nameof(ocrClient));
            _casService = casService ?? throw new ArgumentNullException(nameof(casService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SubmitDocumentAsync(
            OCRSubmissionRequest request, 
            CancellationToken ct = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.ImageData is not { Length: > 0 })
                throw new ArgumentException("Image data cannot be empty", nameof(request));

            if (request.DocumentType != DocumentType.Check)
                throw new ArgumentException($"CheckOCRService can only process Check documents, got {request.DocumentType}", nameof(request));

            try
            {
                _logger.LogInformation("Processing check document");

                // Store image in CAS
                var imageHash = await _casService.StoreAsync(
                    request.ImageData, 
                    ct);
                
                _logger.LogDebug("Stored check image in CAS with hash: {Hash}", imageHash);

                // Create file parameter for the API
                using var stream = new MemoryStream(request.ImageData);
                var fileParam = new FileParameter(stream, "document");

                // Call Mistral OCR API for check processing
                var checkResult = await _ocrClient.ProcessCheckOcrAsync(fileParam, ct);

                if (checkResult?.Error != null)
                {
                    _logger.LogError("OCR processing failed for check. Error: {Error}", checkResult.Error);
                }
                else if (checkResult?.ModelData != null)
                {
                    var check = checkResult.ModelData;
                    _logger.LogInformation("OCR processing completed for check. Check Number: {CheckNumber}, Amount: {Amount}, Processing Time: {Time}", 
                        check.CheckNumber ?? "Unknown",
                        check.Amount ?? 0,
                        checkResult.ProcessingTime);
                    
                    // Store OCR result in CAS
                    var resultHash = await _casService.StoreAsync(
                        checkResult.ModelData, 
                        ct);
                    
                    _logger.LogDebug("Stored OCR result in CAS with hash: {Hash}", resultHash);
                }
                else
                {
                    _logger.LogWarning("Received empty result from OCR API for check");
                }
            }
            catch (MistralOCRException ex)
            {
                _logger.LogError(ex, "Mistral OCR API error. Status: {StatusCode}, Response: {Response}", 
                    ex.StatusCode, ex.Response);
                
                throw new InvalidOperationException(
                    $"OCR API error: {ex.Message}", 
                    ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error calling OCR API");
                throw new InvalidOperationException(
                    "Network error occurred while submitting document", 
                    ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "OCR submission cancelled or timed out");
                throw new InvalidOperationException(
                    "OCR submission was cancelled or timed out", 
                    ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during OCR submission");
                throw new InvalidOperationException(
                    "An unexpected error occurred during OCR submission", 
                    ex);
            }
        }
    }
}