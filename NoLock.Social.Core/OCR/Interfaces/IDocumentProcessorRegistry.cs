using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Defines the contract for managing document processor plugins.
    /// Provides registration, discovery, and routing of document processing.
    /// </summary>
    public interface IDocumentProcessorRegistry
    {
        /// <summary>
        /// Registers a document processor for a specific document type.
        /// </summary>
        /// <param name="processor">The document processor to register.</param>
        void RegisterProcessor(IDocumentProcessor processor);

        /// <summary>
        /// Registers a document processor with metadata.
        /// </summary>
        /// <param name="processor">The document processor to register.</param>
        /// <param name="metadata">The metadata for the processor.</param>
        void RegisterProcessor(IDocumentProcessor processor, IProcessorMetadata metadata);

        /// <summary>
        /// Registers a document processor with detailed metadata.
        /// </summary>
        /// <param name="processor">The document processor to register.</param>
        /// <param name="displayName">The display name of the processor.</param>
        /// <param name="description">The description of what this processor does.</param>
        /// <param name="version">The version of the processor.</param>
        /// <param name="priority">The priority of this processor (default: 0).</param>
        /// <param name="capabilities">The list of capabilities this processor supports.</param>
        /// <param name="supportedExtensions">The supported file extensions for this processor.</param>
        void RegisterProcessor(
            IDocumentProcessor processor,
            string displayName,
            string description,
            Version? version = null,
            int priority = 0,
            IEnumerable<string>? capabilities = null,
            IEnumerable<string>? supportedExtensions = null);

        /// <summary>
        /// Unregisters a document processor.
        /// </summary>
        /// <param name="documentType">The document type of the processor to unregister.</param>
        /// <returns>True if the processor was unregistered; otherwise, false.</returns>
        bool UnregisterProcessor(string documentType);

        /// <summary>
        /// Gets a processor for the specified document type.
        /// </summary>
        /// <param name="documentType">The type of document to process.</param>
        /// <returns>The document processor if found; otherwise, null.</returns>
        IDocumentProcessor? GetProcessor(string documentType);

        /// <summary>
        /// Determines which processor can handle the given OCR data.
        /// </summary>
        /// <param name="rawOcrData">The raw OCR data to analyze.</param>
        /// <returns>The appropriate document processor if found; otherwise, null.</returns>
        IDocumentProcessor? FindProcessorForData(string rawOcrData);

        /// <summary>
        /// Gets all registered document types.
        /// </summary>
        /// <returns>A collection of registered document type names.</returns>
        IEnumerable<string> GetRegisteredTypes();

        /// <summary>
        /// Processes a document using the appropriate processor.
        /// </summary>
        /// <param name="rawOcrData">The raw OCR data to process.</param>
        /// <param name="documentType">Optional document type hint. If not provided, will auto-detect.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>The processed document result.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when no suitable processor is found.</exception>
        Task<object> ProcessDocumentAsync(
            string rawOcrData, 
            string? documentType = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a processor is registered for the specified document type.
        /// </summary>
        /// <param name="documentType">The document type to check.</param>
        /// <returns>True if a processor is registered; otherwise, false.</returns>
        bool IsProcessorRegistered(string documentType);

        /// <summary>
        /// Gets the count of registered processors.
        /// </summary>
        int ProcessorCount { get; }

        /// <summary>
        /// Detects the document type with confidence scoring.
        /// </summary>
        /// <param name="rawOcrData">The raw OCR data to analyze.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>Detection result with document type and confidence score, or null if no detector is available.</returns>
        Task<Models.DocumentTypeDetectionResult?> DetectDocumentTypeAsync(
            string rawOcrData,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes a document with manual type override, bypassing auto-detection.
        /// Use when detection confidence is low or user manually selects document type.
        /// </summary>
        /// <param name="rawOcrData">The raw OCR data to process.</param>
        /// <param name="documentType">The manually specified document type.</param>
        /// <param name="forceProcessing">If true, attempts processing even if CanProcess returns false.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>The processed document result.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when the specified processor is not found.</exception>
        Task<object> ProcessDocumentWithOverrideAsync(
            string rawOcrData,
            string documentType,
            bool forceProcessing = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Enables or disables a processor at runtime.
        /// </summary>
        /// <param name="documentType">The document type of the processor to enable/disable.</param>
        /// <param name="enabled">True to enable, false to disable.</param>
        /// <returns>True if the processor was found and updated; otherwise, false.</returns>
        bool SetProcessorEnabled(string documentType, bool enabled);

        /// <summary>
        /// Gets information about all registered processors.
        /// </summary>
        /// <returns>A collection of processor registration information.</returns>
        IEnumerable<ProcessorRegistration> GetProcessorInfo();

        /// <summary>
        /// Gets information about a specific processor.
        /// </summary>
        /// <param name="documentType">The document type of the processor.</param>
        /// <returns>The processor registration information if found; otherwise, null.</returns>
        ProcessorRegistration? GetProcessorInfo(string documentType);

        /// <summary>
        /// Gets the processor with the highest priority for handling the given OCR data.
        /// </summary>
        /// <param name="rawOcrData">The raw OCR data to analyze.</param>
        /// <returns>The processor with the highest priority if found; otherwise, null.</returns>
        IDocumentProcessor? GetHighestPriorityProcessor(string rawOcrData);

        /// <summary>
        /// Gets all processors that can handle the given OCR data, ordered by priority.
        /// </summary>
        /// <param name="rawOcrData">The raw OCR data to analyze.</param>
        /// <returns>A collection of processors ordered by priority (highest first).</returns>
        IEnumerable<ProcessorRegistration> GetCompatibleProcessors(string rawOcrData);

        /// <summary>
        /// Updates the priority of a processor.
        /// </summary>
        /// <param name="documentType">The document type of the processor.</param>
        /// <param name="priority">The new priority value.</param>
        /// <returns>True if the processor was found and updated; otherwise, false.</returns>
        bool UpdateProcessorPriority(string documentType, int priority);

        /// <summary>
        /// Gets all enabled processors.
        /// </summary>
        /// <returns>A collection of enabled processor registrations.</returns>
        IEnumerable<ProcessorRegistration> GetEnabledProcessors();

        /// <summary>
        /// Gets all disabled processors.
        /// </summary>
        /// <returns>A collection of disabled processor registrations.</returns>
        IEnumerable<ProcessorRegistration> GetDisabledProcessors();
    }
}