using System.Threading;
using System.Threading.Tasks;

namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Defines the contract for document type-specific processors in the OCR plugin architecture.
    /// Each processor handles extraction, validation, and formatting for a specific document type.
    /// </summary>
    public interface IDocumentProcessor
    {
        /// <summary>
        /// Gets the document type this processor handles.
        /// </summary>
        string DocumentType { get; }

        /// <summary>
        /// Processes raw OCR data into a structured document format.
        /// </summary>
        /// <param name="rawOcrData">The raw OCR data extracted from the document.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains the processed document with extracted, validated, and formatted data.
        /// </returns>
        Task<object> ProcessAsync(string rawOcrData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates whether this processor can handle the given OCR data.
        /// </summary>
        /// <param name="rawOcrData">The raw OCR data to validate.</param>
        /// <returns>True if this processor can handle the data; otherwise, false.</returns>
        bool CanProcess(string rawOcrData);
    }
}