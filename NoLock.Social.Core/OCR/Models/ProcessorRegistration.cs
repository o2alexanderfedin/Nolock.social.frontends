using System;
using System.Collections.Generic;
using NoLock.Social.Core.OCR.Interfaces;

namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents a document processor registration with associated metadata.
    /// Wraps a processor instance with its metadata for registry management.
    /// </summary>
    public class ProcessorRegistration : IProcessorMetadata
    {
        /// <summary>
        /// Gets the document processor instance.
        /// </summary>
        public IDocumentProcessor Processor { get; }

        /// <summary>
        /// Gets the display name of the processor.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the description of what this processor does.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the version of the processor.
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Gets the priority of this processor when multiple processors can handle the same document.
        /// Higher values indicate higher priority.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Gets the list of capabilities this processor supports.
        /// </summary>
        public IReadOnlyCollection<string> Capabilities { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this processor is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets the supported file extensions for this processor.
        /// </summary>
        public IReadOnlyCollection<string> SupportedExtensions { get; }

        /// <summary>
        /// Gets any additional metadata as key-value pairs.
        /// </summary>
        public IReadOnlyDictionary<string, object> AdditionalMetadata { get; }

        /// <summary>
        /// Gets the date and time when this processor was registered.
        /// </summary>
        public DateTime RegistrationTime { get; }

        /// <summary>
        /// Initializes a new instance of the ProcessorRegistration class.
        /// </summary>
        /// <param name="processor">The document processor instance.</param>
        /// <param name="displayName">The display name of the processor.</param>
        /// <param name="description">The description of what this processor does.</param>
        /// <param name="version">The version of the processor.</param>
        /// <param name="priority">The priority of this processor (default: 0).</param>
        /// <param name="capabilities">The list of capabilities this processor supports.</param>
        /// <param name="supportedExtensions">The supported file extensions for this processor.</param>
        /// <param name="additionalMetadata">Any additional metadata as key-value pairs.</param>
        public ProcessorRegistration(
            IDocumentProcessor processor,
            string displayName,
            string description,
            Version? version = null,
            int priority = 0,
            IEnumerable<string>? capabilities = null,
            IEnumerable<string>? supportedExtensions = null,
            IDictionary<string, object>? additionalMetadata = null)
        {
            Processor = processor ?? throw new ArgumentNullException(nameof(processor));
            DisplayName = displayName ?? processor.DocumentType;
            Description = description ?? $"Processor for {processor.DocumentType} documents";
            Version = version ?? new Version(1, 0, 0);
            Priority = priority;
            Capabilities = capabilities != null 
                ? new List<string>(capabilities).AsReadOnly() 
                : new List<string>().AsReadOnly();
            SupportedExtensions = supportedExtensions != null 
                ? new List<string>(supportedExtensions).AsReadOnly() 
                : new List<string>().AsReadOnly();
            AdditionalMetadata = additionalMetadata != null 
                ? new Dictionary<string, object>(additionalMetadata) 
                : new Dictionary<string, object>();
            IsEnabled = true;
            RegistrationTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a simple registration with minimal metadata.
        /// </summary>
        /// <param name="processor">The document processor instance.</param>
        /// <returns>A new ProcessorRegistration with default metadata.</returns>
        public static ProcessorRegistration CreateSimple(IDocumentProcessor processor)
        {
            return new ProcessorRegistration(
                processor,
                processor.DocumentType,
                $"Processor for {processor.DocumentType} documents");
        }
    }
}