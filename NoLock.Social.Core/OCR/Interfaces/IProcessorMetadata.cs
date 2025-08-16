using System;
using System.Collections.Generic;

namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Defines metadata for a document processor plugin.
    /// Provides information about processor capabilities, version, and priority.
    /// </summary>
    public interface IProcessorMetadata
    {
        /// <summary>
        /// Gets the display name of the processor.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the description of what this processor does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the version of the processor.
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// Gets the priority of this processor when multiple processors can handle the same document.
        /// Higher values indicate higher priority.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets the list of capabilities this processor supports.
        /// </summary>
        IReadOnlyCollection<string> Capabilities { get; }

        /// <summary>
        /// Gets a value indicating whether this processor is enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Gets the supported file extensions for this processor.
        /// </summary>
        IReadOnlyCollection<string> SupportedExtensions { get; }

        /// <summary>
        /// Gets any additional metadata as key-value pairs.
        /// </summary>
        IReadOnlyDictionary<string, object> AdditionalMetadata { get; }
    }
}