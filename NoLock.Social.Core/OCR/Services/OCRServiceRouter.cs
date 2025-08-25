using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Router service that delegates to the appropriate OCR service based on document type
    /// </summary>
    public class OCRServiceRouter : IOCRService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OCRServiceRouter> _logger;

        public OCRServiceRouter(IServiceProvider serviceProvider, ILogger<OCRServiceRouter> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task SubmitDocumentAsync(
            OCRSubmissionRequest request, 
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            _logger.LogInformation("Routing OCR request for document type: {DocumentType}", request.DocumentType);

            // Use keyed service resolution to get the appropriate service
            var service = _serviceProvider.GetRequiredKeyedService<IOCRService>(request.DocumentType);
            
            if (service == null)
            {
                throw new NotSupportedException($"Document type {request.DocumentType} is not supported");
            }

            return service.SubmitDocumentAsync(request, ct);
        }
    }
}