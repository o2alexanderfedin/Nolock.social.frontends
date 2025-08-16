using System;
using System.Collections.Generic;
using System.Linq;

namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Wrapper for ProcessedDocument that enables field correction tracking.
    /// Maintains the original document while tracking all user corrections separately.
    /// </summary>
    public class CorrectedProcessedDocument
    {
        /// <summary>
        /// Gets or sets the original processed document.
        /// </summary>
        public ProcessedDocument OriginalDocument { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for this correction session.
        /// </summary>
        public string CorrectionSessionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets when this correction session was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets when this correction session was last modified.
        /// </summary>
        public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets whether there are unsaved changes.
        /// </summary>
        public bool HasUnsavedChanges { get; set; } = false;

        /// <summary>
        /// Gets or sets the collection of field corrections made by the user.
        /// </summary>
        public Dictionary<string, FieldCorrection> FieldCorrections { get; set; } = 
            new Dictionary<string, FieldCorrection>();

        /// <summary>
        /// Gets or sets fields that should be highlighted for correction due to low confidence.
        /// </summary>
        public List<string> LowConfidenceFields { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the confidence threshold used to identify low confidence fields.
        /// </summary>
        public double ConfidenceThreshold { get; set; } = 0.7;

        /// <summary>
        /// Gets or sets additional metadata for the correction session.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the CorrectedProcessedDocument class.
        /// </summary>
        /// <param name="originalDocument">The original processed document.</param>
        public CorrectedProcessedDocument(ProcessedDocument originalDocument)
        {
            OriginalDocument = originalDocument ?? throw new ArgumentNullException(nameof(originalDocument));
        }

        /// <summary>
        /// Parameterless constructor for serialization.
        /// </summary>
        public CorrectedProcessedDocument()
        {
            OriginalDocument = null!; // Will be set during deserialization
        }

        /// <summary>
        /// Gets the effective value for a field (corrected value if exists, otherwise original).
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>The effective field value.</returns>
        public object? GetEffectiveFieldValue(string fieldName)
        {
            if (FieldCorrections.TryGetValue(fieldName, out var correction))
            {
                return correction.CorrectedValue;
            }

            return GetOriginalFieldValue(fieldName);
        }

        /// <summary>
        /// Gets the original value for a field from the source document.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>The original field value.</returns>
        public object? GetOriginalFieldValue(string fieldName)
        {
            // This would need to be implemented with reflection or a field mapping strategy
            // For now, return null - this will be implemented in the service layer
            return null;
        }

        /// <summary>
        /// Gets the effective confidence score for a field (updated if corrected, otherwise original).
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>The effective confidence score.</returns>
        public double GetEffectiveConfidenceScore(string fieldName)
        {
            if (FieldCorrections.TryGetValue(fieldName, out var correction))
            {
                return correction.UpdatedConfidence;
            }

            return GetOriginalConfidenceScore(fieldName);
        }

        /// <summary>
        /// Gets the original confidence score for a field.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>The original confidence score.</returns>
        public double GetOriginalConfidenceScore(string fieldName)
        {
            // Default to the document's overall confidence score
            // This will be refined in the service implementation
            return OriginalDocument?.ConfidenceScore ?? 0.0;
        }

        /// <summary>
        /// Checks if a field has been corrected by the user.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>True if the field has been corrected; otherwise, false.</returns>
        public bool IsFieldCorrected(string fieldName)
        {
            return FieldCorrections.ContainsKey(fieldName);
        }

        /// <summary>
        /// Checks if a field should be highlighted for correction.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns>True if the field should be highlighted; otherwise, false.</returns>
        public bool ShouldHighlightField(string fieldName)
        {
            return LowConfidenceFields.Contains(fieldName);
        }

        /// <summary>
        /// Gets all fields that have been corrected.
        /// </summary>
        /// <returns>A collection of corrected field names.</returns>
        public IReadOnlyList<string> GetCorrectedFields()
        {
            return FieldCorrections.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets all fields that have validation errors.
        /// </summary>
        /// <returns>A collection of field names with validation errors.</returns>
        public IReadOnlyList<string> GetFieldsWithErrors()
        {
            return FieldCorrections
                .Where(kvp => !kvp.Value.IsValid)
                .Select(kvp => kvp.Key)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Gets all fields that have validation warnings.
        /// </summary>
        /// <returns>A collection of field names with validation warnings.</returns>
        public IReadOnlyList<string> GetFieldsWithWarnings()
        {
            return FieldCorrections
                .Where(kvp => kvp.Value.ValidationWarnings.Count > 0)
                .Select(kvp => kvp.Key)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Gets the count of unsaved corrections.
        /// </summary>
        /// <returns>The number of unsaved corrections.</returns>
        public int GetUnsavedCorrectionsCount()
        {
            return FieldCorrections.Count(kvp => !kvp.Value.IsSaved);
        }

        /// <summary>
        /// Checks if all corrections are valid.
        /// </summary>
        /// <returns>True if all corrections are valid; otherwise, false.</returns>
        public bool AreAllCorrectionsValid()
        {
            return FieldCorrections.Values.All(correction => correction.IsValid);
        }

        /// <summary>
        /// Calculates the overall correction confidence score.
        /// </summary>
        /// <returns>The overall confidence score including corrections.</returns>
        public double CalculateOverallConfidence()
        {
            if (FieldCorrections.Count == 0)
            {
                return OriginalDocument?.ConfidenceScore ?? 0.0;
            }

            // Simple average of all field confidence scores
            // This logic can be refined based on business requirements
            var correctedFieldConfidences = FieldCorrections.Values.Select(c => c.UpdatedConfidence);
            var originalConfidence = OriginalDocument?.ConfidenceScore ?? 0.0;
            
            return correctedFieldConfidences.Concat(new[] { originalConfidence }).Average();
        }
    }
}