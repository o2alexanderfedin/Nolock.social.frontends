using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.Common.Results;
using NoLock.Social.Core.Common.Extensions;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Manages registration and discovery of document processors.
    /// Thread-safe implementation suitable for Blazor WebAssembly.
    /// Enhanced with automatic document type detection capabilities.
    /// </summary>
    public class DocumentProcessorRegistry : IDocumentProcessorRegistry
    {
        private readonly ConcurrentDictionary<string, ProcessorRegistration> _registrations;
        private readonly ILogger<DocumentProcessorRegistry> _logger;
        private readonly IDocumentTypeDetector? _documentTypeDetector;

        /// <summary>
        /// Initializes a new instance of the DocumentProcessorRegistry class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        public DocumentProcessorRegistry(ILogger<DocumentProcessorRegistry> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _registrations = new ConcurrentDictionary<string, ProcessorRegistration>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Initializes a new instance of the DocumentProcessorRegistry class with document type detector.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="documentTypeDetector">Document type detector for enhanced auto-detection.</param>
        public DocumentProcessorRegistry(
            ILogger<DocumentProcessorRegistry> logger,
            IDocumentTypeDetector? documentTypeDetector) : this(logger)
        {
            _documentTypeDetector = documentTypeDetector;
        }

        /// <summary>
        /// Initializes a new instance of the DocumentProcessorRegistry class with initial processors.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="processors">Initial processors to register.</param>
        public DocumentProcessorRegistry(
            ILogger<DocumentProcessorRegistry> logger,
            IEnumerable<IDocumentProcessor> processors) : this(logger)
        {
            if (processors != null)
            {
                foreach (var processor in processors)
                {
                    RegisterProcessor(processor);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the DocumentProcessorRegistry class with detector and processors.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="documentTypeDetector">Document type detector for enhanced auto-detection.</param>
        /// <param name="processors">Initial processors to register.</param>
        public DocumentProcessorRegistry(
            ILogger<DocumentProcessorRegistry> logger,
            IDocumentTypeDetector? documentTypeDetector,
            IEnumerable<IDocumentProcessor> processors) : this(logger, documentTypeDetector)
        {
            if (processors != null)
            {
                foreach (var processor in processors)
                {
                    RegisterProcessor(processor);
                }
            }
        }

        /// <inheritdoc />
        public int ProcessorCount => _registrations.Count;

        /// <inheritdoc />
        public void RegisterProcessor(IDocumentProcessor processor)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            if (string.IsNullOrWhiteSpace(processor.DocumentType))
            {
                throw new ArgumentException("Processor must have a valid DocumentType.", nameof(processor));
            }

            var registration = ProcessorRegistration.CreateSimple(processor);

            if (_registrations.TryAdd(processor.DocumentType, registration))
            {
                _logger.LogInformation("Registered processor for document type: {DocumentType}", processor.DocumentType);
            }
            else
            {
                // Try to update if it already exists
                _registrations[processor.DocumentType] = registration;
                _logger.LogWarning("Updated existing processor for document type: {DocumentType}", processor.DocumentType);
            }
        }

        /// <inheritdoc />
        public void RegisterProcessor(IDocumentProcessor processor, IProcessorMetadata metadata)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            if (string.IsNullOrWhiteSpace(processor.DocumentType))
            {
                throw new ArgumentException("Processor must have a valid DocumentType.", nameof(processor));
            }

            var registration = new ProcessorRegistration(
                processor,
                metadata.DisplayName,
                metadata.Description,
                metadata.Version,
                metadata.Priority,
                metadata.Capabilities,
                metadata.SupportedExtensions,
                metadata.AdditionalMetadata as IDictionary<string, object>);

            if (_registrations.TryAdd(processor.DocumentType, registration))
            {
                _logger.LogInformation("Registered processor for document type: {DocumentType} with priority {Priority}", 
                    processor.DocumentType, metadata.Priority);
            }
            else
            {
                // Try to update if it already exists
                _registrations[processor.DocumentType] = registration;
                _logger.LogWarning("Updated existing processor for document type: {DocumentType} with priority {Priority}", 
                    processor.DocumentType, metadata.Priority);
            }
        }

        /// <inheritdoc />
        public void RegisterProcessor(
            IDocumentProcessor processor,
            string displayName,
            string description,
            Version? version = null,
            int priority = 0,
            IEnumerable<string>? capabilities = null,
            IEnumerable<string>? supportedExtensions = null)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            if (string.IsNullOrWhiteSpace(processor.DocumentType))
            {
                throw new ArgumentException("Processor must have a valid DocumentType.", nameof(processor));
            }

            var registration = new ProcessorRegistration(
                processor,
                displayName,
                description,
                version,
                priority,
                capabilities,
                supportedExtensions);

            if (_registrations.TryAdd(processor.DocumentType, registration))
            {
                _logger.LogInformation("Registered processor for document type: {DocumentType} with priority {Priority}", 
                    processor.DocumentType, priority);
            }
            else
            {
                // Try to update if it already exists
                _registrations[processor.DocumentType] = registration;
                _logger.LogWarning("Updated existing processor for document type: {DocumentType} with priority {Priority}", 
                    processor.DocumentType, priority);
            }
        }

        /// <inheritdoc />
        public bool UnregisterProcessor(string documentType)
        {
            if (string.IsNullOrWhiteSpace(documentType))
            {
                return false;
            }

            if (_registrations.TryRemove(documentType, out var removedRegistration))
            {
                _logger.LogInformation("Unregistered processor for document type: {DocumentType}", documentType);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public IDocumentProcessor? GetProcessor(string documentType)
        {
            if (string.IsNullOrWhiteSpace(documentType))
            {
                return null;
            }

            if (_registrations.TryGetValue(documentType, out var registration))
            {
                return registration.IsEnabled ? registration.Processor : null;
            }
            return null;
        }

        /// <inheritdoc />
        public async Task<IDocumentProcessor?> FindProcessorForDataAsync(string rawOcrData)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                return null;
            }

            // First, try using the document type detector if available
            if (_documentTypeDetector != null)
            {
                var detectionResult = await _logger.ExecuteWithLogging(
                    async () => await _documentTypeDetector.DetectDocumentTypeAsync(rawOcrData),
                    "Document type detection");
                
                if (detectionResult.IsSuccess && detectionResult.Value != null && 
                    !string.IsNullOrEmpty(detectionResult.Value.DocumentType) &&
                    detectionResult.Value.DocumentType != "Unknown" &&
                    detectionResult.Value.DocumentType != "Ambiguous")
                {
                    var detectedProcessor = GetProcessor(detectionResult.Value.DocumentType);
                    
                    if (detectedProcessor != null && detectedProcessor.CanProcess(rawOcrData))
                    {
                        _logger.LogInformation(
                            "Found processor {ProcessorType} using document type detector with confidence {Confidence:P}",
                            detectedProcessor.DocumentType,
                            detectionResult.Value.ConfidenceScore);
                        
                        return detectedProcessor;
                    }
                }
                
                // Log if detection was ambiguous or low confidence
                if (detectionResult.IsSuccess && detectionResult.Value != null && detectionResult.Value.RequiresManualConfirmation)
                {
                    _logger.LogWarning(
                        "Document type detection requires manual confirmation: {Reason}",
                        detectionResult.Value.ManualConfirmationReason);
                }
            }

            // Fallback to trying each processor's CanProcess method, using priority ordering
            var highestPriorityProcessor = GetHighestPriorityProcessor(rawOcrData);
            
            if (highestPriorityProcessor != null)
            {
                _logger.LogDebug("Found processor {ProcessorType} for OCR data using CanProcess check", 
                    highestPriorityProcessor.DocumentType);
                return highestPriorityProcessor;
            }

            _logger.LogWarning("No suitable processor found for OCR data");
            return null;
        }

        /// <inheritdoc />
        public IEnumerable<string> GetRegisteredTypes()
        {
            return _registrations.Keys.OrderBy(k => k);
        }

        /// <inheritdoc />
        public async Task<object> ProcessDocumentAsync(
            string rawOcrData,
            string? documentType = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                throw new ArgumentException("Raw OCR data cannot be null or empty.", nameof(rawOcrData));
            }

            IDocumentProcessor? processor = null;

            // If document type is specified, try to get that specific processor
            if (!string.IsNullOrWhiteSpace(documentType))
            {
                processor = GetProcessor(documentType);
                
                if (processor == null)
                {
                    _logger.LogWarning("No processor registered for document type: {DocumentType}", documentType);
                }
                else if (!processor.CanProcess(rawOcrData))
                {
                    _logger.LogWarning("Processor for {DocumentType} cannot process the provided data", documentType);
                    processor = null;
                }
            }

            // If no processor found yet, try auto-detection
            if (processor == null)
            {
                processor = await FindProcessorForDataAsync(rawOcrData);
            }

            if (processor == null)
            {
                var registeredTypes = string.Join(", ", GetRegisteredTypes());
                throw new InvalidOperationException(
                    $"No suitable processor found for the document. Registered types: {registeredTypes}");
            }

            _logger.LogInformation("Processing document with {ProcessorType} processor", processor.DocumentType);

            try
            {
                return await processor.ProcessAsync(rawOcrData, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document with {ProcessorType} processor", processor.DocumentType);
                throw new InvalidOperationException(
                    $"Error processing document with {processor.DocumentType} processor", ex);
            }
        }

        /// <inheritdoc />
        public bool IsProcessorRegistered(string documentType)
        {
            if (string.IsNullOrWhiteSpace(documentType))
            {
                return false;
            }

            return _registrations.ContainsKey(documentType);
        }

        /// <inheritdoc />
        public async Task<Models.DocumentTypeDetectionResult?> DetectDocumentTypeAsync(
            string rawOcrData,
            CancellationToken cancellationToken = default)
        {
            if (_documentTypeDetector == null)
            {
                _logger.LogDebug("No document type detector available");
                return null;
            }

            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                return Models.DocumentTypeDetectionResult.Unknown();
            }

            try
            {
                var result = await _documentTypeDetector.DetectDocumentTypeAsync(rawOcrData, cancellationToken);
                
                // Verify that we have a processor for the detected type
                if (result != null && 
                    !string.IsNullOrEmpty(result.DocumentType) &&
                    result.DocumentType != "Unknown" &&
                    result.DocumentType != "Ambiguous")
                {
                    if (!IsProcessorRegistered(result.DocumentType))
                    {
                        _logger.LogWarning(
                            "Detected document type {DocumentType} but no processor is registered for it",
                            result.DocumentType);
                        
                        result.RequiresManualConfirmation = true;
                        result.ManualConfirmationReason = $"No processor registered for detected type: {result.DocumentType}";
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting document type");
                return Models.DocumentTypeDetectionResult.Unknown();
            }
        }

        /// <inheritdoc />
        public bool SetProcessorEnabled(string documentType, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(documentType))
            {
                return false;
            }

            if (_registrations.TryGetValue(documentType, out var registration))
            {
                registration.IsEnabled = enabled;
                _logger.LogInformation("Processor for document type {DocumentType} has been {Status}", 
                    documentType, enabled ? "enabled" : "disabled");
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public IDocumentProcessor? GetHighestPriorityProcessor(string rawOcrData)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                return null;
            }

            var compatibleProcessors = GetCompatibleProcessors(rawOcrData);
            var highestPriority = compatibleProcessors.FirstOrDefault();
            
            return highestPriority?.Processor;
        }

        /// <inheritdoc />
        public IEnumerable<ProcessorRegistration> GetCompatibleProcessors(string rawOcrData)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                return Enumerable.Empty<ProcessorRegistration>();
            }

            var compatible = new List<ProcessorRegistration>();

            foreach (var registration in _registrations.Values.Where(r => r.IsEnabled))
            {
                try
                {
                    if (registration.Processor.CanProcess(rawOcrData))
                    {
                        compatible.Add(registration);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking processor {ProcessorType} for compatibility", 
                        registration.Processor.DocumentType);
                }
            }

            return compatible.OrderByDescending(r => r.Priority);
        }

        /// <inheritdoc />
        public bool UpdateProcessorPriority(string documentType, int priority)
        {
            if (string.IsNullOrWhiteSpace(documentType))
            {
                return false;
            }

            if (_registrations.TryGetValue(documentType, out var registration))
            {
                // Create a new registration with updated priority (immutability pattern)
                var updatedRegistration = new ProcessorRegistration(
                    registration.Processor,
                    registration.DisplayName,
                    registration.Description,
                    registration.Version,
                    priority,
                    registration.Capabilities,
                    registration.SupportedExtensions,
                    registration.AdditionalMetadata as IDictionary<string, object>)
                {
                    IsEnabled = registration.IsEnabled
                };

                _registrations[documentType] = updatedRegistration;
                _logger.LogInformation("Updated priority for processor {DocumentType} to {Priority}", 
                    documentType, priority);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public IEnumerable<ProcessorRegistration> GetProcessorInfo()
        {
            return _registrations.Values.OrderByDescending(r => r.Priority).ThenBy(r => r.DisplayName);
        }

        /// <inheritdoc />
        public ProcessorRegistration? GetProcessorInfo(string documentType)
        {
            if (string.IsNullOrWhiteSpace(documentType))
            {
                return null;
            }

            _registrations.TryGetValue(documentType, out var registration);
            return registration;
        }

        /// <inheritdoc />
        public IEnumerable<ProcessorRegistration> GetEnabledProcessors()
        {
            return _registrations.Values
                .Where(r => r.IsEnabled)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.DisplayName);
        }

        /// <inheritdoc />
        public IEnumerable<ProcessorRegistration> GetDisabledProcessors()
        {
            return _registrations.Values
                .Where(r => !r.IsEnabled)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.DisplayName);
        }

        /// <inheritdoc />
        public async Task<object> ProcessDocumentWithOverrideAsync(
            string rawOcrData,
            string documentType,
            bool forceProcessing = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                throw new ArgumentException("Raw OCR data cannot be null or empty.", nameof(rawOcrData));
            }

            if (string.IsNullOrWhiteSpace(documentType))
            {
                throw new ArgumentException("Document type cannot be null or empty.", nameof(documentType));
            }

            var processor = GetProcessor(documentType);

            if (processor == null)
            {
                var registeredTypes = string.Join(", ", GetRegisteredTypes());
                throw new InvalidOperationException(
                    $"No processor registered for document type '{documentType}'. Registered types: {registeredTypes}");
            }

            // Check if processor can handle the data unless force processing is enabled
            if (!forceProcessing && !processor.CanProcess(rawOcrData))
            {
                _logger.LogWarning(
                    "Processor for {DocumentType} reports it cannot process the data. Use forceProcessing=true to override.",
                    documentType);
                
                throw new InvalidOperationException(
                    $"Processor for '{documentType}' cannot process the provided data. " +
                    "Consider using forceProcessing=true if you want to override this check.");
            }

            _logger.LogInformation(
                "Processing document with manual override. Type: {DocumentType}, Force: {ForceProcessing}",
                documentType,
                forceProcessing);

            try
            {
                return await processor.ProcessAsync(rawOcrData, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document with manual override for type {DocumentType}", documentType);
                throw new InvalidOperationException(
                    $"Error processing document as '{documentType}'", ex);
            }
        }
    }
}