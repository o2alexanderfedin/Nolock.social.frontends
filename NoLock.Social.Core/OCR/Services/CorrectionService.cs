using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Service for managing OCR field corrections and validation.
    /// Provides functionality to track user corrections, validate field values,
    /// and update confidence scores based on corrections.
    /// </summary>
    public class CorrectionService : ICorrectionService
    {
        private readonly FieldValidationService _validationService;
        private readonly Dictionary<string, CorrectedProcessedDocument> _correctionSessions = new();
        private readonly Dictionary<string, List<FieldCorrection>> _pendingCorrections = new();

        /// <summary>
        /// Initializes a new instance of the CorrectionService class.
        /// </summary>
        /// <param name="validationService">The field validation service.</param>
        public CorrectionService(FieldValidationService validationService)
        {
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        }

        /// <summary>
        /// Creates a correctable copy of a processed document.
        /// </summary>
        /// <param name="document">The original processed document.</param>
        /// <returns>A correctable wrapper containing the document and correction tracking.</returns>
        public async Task<CorrectedProcessedDocument> CreateCorrectableDocumentAsync(ProcessedDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var correctedDocument = new CorrectedProcessedDocument(document);
            
            // Identify low confidence fields
            var lowConfidenceFields = await GetLowConfidenceFieldsAsync(document, correctedDocument.ConfidenceThreshold);
            correctedDocument.LowConfidenceFields = lowConfidenceFields.ToList();

            // Store the correction session
            _correctionSessions[document.DocumentId] = correctedDocument;
            _pendingCorrections[document.DocumentId] = new List<FieldCorrection>();

            return correctedDocument;
        }

        /// <summary>
        /// Applies a field correction to the document.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="fieldName">The name of the field being corrected.</param>
        /// <param name="newValue">The corrected value.</param>
        /// <param name="originalValue">The original OCR value.</param>
        /// <param name="originalConfidence">The original confidence score.</param>
        /// <returns>A validation result indicating success or errors.</returns>
        public async Task<FieldValidationResult> ApplyFieldCorrectionAsync(
            string documentId,
            string fieldName, 
            object newValue, 
            object originalValue, 
            double originalConfidence)
        {
            ValidateInputParameters(documentId, fieldName);
            var correctedDocument = GetCorrectionSession(documentId);
            
            var validationResult = await ValidateFieldAsync(fieldName, newValue, correctedDocument.OriginalDocument.DocumentType);
            var correction = CreateFieldCorrection(fieldName, newValue, originalValue, originalConfidence, validationResult, correctedDocument.OriginalDocument.DocumentType);
            
            UpdateCorrectionSession(correctedDocument, fieldName, correction);
            UpdatePendingCorrections(documentId, correction);
            
            return validationResult;
        }

        private void ValidateInputParameters(string documentId, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("Document ID is required", nameof(documentId));

            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("Field name is required", nameof(fieldName));
        }

        private CorrectedProcessedDocument GetCorrectionSession(string documentId)
        {
            if (!_correctionSessions.TryGetValue(documentId, out var correctedDocument))
                throw new InvalidOperationException($"No correction session found for document {documentId}");
                
            return correctedDocument;
        }

        private FieldCorrection CreateFieldCorrection(
            string fieldName, 
            object newValue, 
            object originalValue, 
            double originalConfidence, 
            FieldValidationResult validationResult, 
            string documentType)
        {
            return new FieldCorrection
            {
                FieldName = fieldName,
                OriginalValue = originalValue,
                CorrectedValue = newValue,
                OriginalConfidence = originalConfidence,
                UpdatedConfidence = CalculateUpdatedConfidence(originalConfidence, !Equals(originalValue, newValue), validationResult),
                CorrectedAt = DateTime.UtcNow,
                IsValid = validationResult.IsValid,
                ValidationErrors = validationResult.Errors,
                ValidationWarnings = validationResult.Warnings,
                FieldType = DetermineFieldType(fieldName, documentType)
            };
        }

        private static void UpdateCorrectionSession(CorrectedProcessedDocument correctedDocument, string fieldName, FieldCorrection correction)
        {
            correctedDocument.FieldCorrections[fieldName] = correction;
            correctedDocument.HasUnsavedChanges = true;
            correctedDocument.LastModifiedAt = DateTime.UtcNow;
        }

        private void UpdatePendingCorrections(string documentId, FieldCorrection correction)
        {
            EnsurePendingCorrectionsListExists(documentId);
            RemoveExistingPendingCorrection(documentId, correction.FieldName);
            _pendingCorrections[documentId].Add(correction);
        }

        private void EnsurePendingCorrectionsListExists(string documentId)
        {
            if (!_pendingCorrections.ContainsKey(documentId))
                _pendingCorrections[documentId] = new List<FieldCorrection>();
        }

        private void RemoveExistingPendingCorrection(string documentId, string fieldName)
        {
            var existingCorrection = _pendingCorrections[documentId].FirstOrDefault(c => c.FieldName == fieldName);
            if (existingCorrection != null)
            {
                _pendingCorrections[documentId].Remove(existingCorrection);
            }
        }

        /// <summary>
        /// Validates a field value based on its type and constraints.
        /// </summary>
        /// <param name="fieldName">The name of the field to validate.</param>
        /// <param name="value">The value to validate.</param>
        /// <param name="documentType">The type of document (Receipt, Check, etc.).</param>
        /// <returns>A validation result with any errors or warnings.</returns>
        public async Task<FieldValidationResult> ValidateFieldAsync(string fieldName, object value, string documentType)
        {
            return await _validationService.ValidateFieldAsync(fieldName, value, documentType);
        }

        /// <summary>
        /// Calculates an updated confidence score based on user correction.
        /// </summary>
        /// <param name="originalConfidence">The original OCR confidence score.</param>
        /// <param name="wasValueChanged">Whether the user changed the value.</param>
        /// <param name="validationResult">The validation result for the corrected value.</param>
        /// <returns>The updated confidence score.</returns>
        public double CalculateUpdatedConfidence(double originalConfidence, bool wasValueChanged, FieldValidationResult validationResult)
        {
            // Start with the original confidence
            var updatedConfidence = originalConfidence;

            if (wasValueChanged)
            {
                // If user changed the value, this indicates the original was incorrect
                // So we boost confidence significantly if the new value is valid
                if (validationResult.IsValid)
                {
                    // Boost confidence to at least 0.9 for user-corrected valid values
                    updatedConfidence = Math.Max(0.9, originalConfidence + 0.3);
                }
                else
                {
                    // If corrected value is invalid, reduce confidence
                    updatedConfidence = Math.Max(0.1, originalConfidence - 0.4);
                }
            }
            else
            {
                // User didn't change the value, so they confirmed it was correct
                // Boost confidence moderately
                updatedConfidence = Math.Min(1.0, originalConfidence + 0.2);
            }

            // Apply validation result adjustments
            if (validationResult.Warnings.Count > 0)
            {
                updatedConfidence = Math.Max(0.1, updatedConfidence - 0.1);
            }

            // Ensure confidence stays within valid range
            return Math.Max(0.0, Math.Min(1.0, updatedConfidence));
        }

        /// <summary>
        /// Gets all corrections made to a document.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <returns>A collection of field corrections.</returns>
        public async Task<IReadOnlyList<FieldCorrection>> GetCorrectionsAsync(string documentId)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("Document ID is required", nameof(documentId));

            if (!_correctionSessions.TryGetValue(documentId, out var correctedDocument))
                return Array.Empty<FieldCorrection>();

            return await Task.FromResult(correctedDocument.FieldCorrections.Values.ToList().AsReadOnly());
        }

        /// <summary>
        /// Saves all pending corrections to the document.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <returns>A task representing the save operation.</returns>
        public async Task<ProcessedDocument> SaveCorrectionsAsync(string documentId)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("Document ID is required", nameof(documentId));

            if (!_correctionSessions.TryGetValue(documentId, out var correctedDocument))
                throw new InvalidOperationException($"No correction session found for document {documentId}");

            // Validate all corrections before saving
            var allValid = correctedDocument.AreAllCorrectionsValid();
            if (!allValid)
            {
                var invalidFields = correctedDocument.GetFieldsWithErrors();
                throw new InvalidOperationException($"Cannot save corrections with validation errors in fields: {string.Join(", ", invalidFields)}");
            }

            // Mark all corrections as saved
            foreach (var correction in correctedDocument.FieldCorrections.Values)
            {
                correction.IsSaved = true;
            }

            // Clear pending corrections
            if (_pendingCorrections.ContainsKey(documentId))
            {
                _pendingCorrections[documentId].Clear();
            }

            // Update document metadata
            correctedDocument.HasUnsavedChanges = false;
            correctedDocument.LastModifiedAt = DateTime.UtcNow;

            // In a real implementation, this would persist the corrected document
            // For now, we'll return the original document with updated confidence
            var updatedDocument = correctedDocument.OriginalDocument;
            updatedDocument.ConfidenceScore = correctedDocument.CalculateOverallConfidence();

            return await Task.FromResult(updatedDocument);
        }

        /// <summary>
        /// Cancels all pending corrections for a document.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <returns>A task representing the cancel operation.</returns>
        public async Task CancelCorrectionsAsync(string documentId)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("Document ID is required", nameof(documentId));

            if (!_correctionSessions.TryGetValue(documentId, out var correctedDocument))
                return;

            // Remove all unsaved corrections
            var fieldsToRemove = correctedDocument.FieldCorrections
                .Where(kvp => !kvp.Value.IsSaved)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var fieldName in fieldsToRemove)
            {
                correctedDocument.FieldCorrections.Remove(fieldName);
            }

            // Clear pending corrections
            if (_pendingCorrections.ContainsKey(documentId))
            {
                _pendingCorrections[documentId].Clear();
            }

            // Update document metadata
            correctedDocument.HasUnsavedChanges = false;
            correctedDocument.LastModifiedAt = DateTime.UtcNow;

            await Task.CompletedTask;
        }

        /// <summary>
        /// Reverts a specific field correction.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="fieldName">The field name to revert.</param>
        /// <returns>A task representing the revert operation.</returns>
        public async Task RevertFieldCorrectionAsync(string documentId, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("Document ID is required", nameof(documentId));

            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("Field name is required", nameof(fieldName));

            if (!_correctionSessions.TryGetValue(documentId, out var correctedDocument))
                throw new InvalidOperationException($"No correction session found for document {documentId}");

            // Remove the field correction
            if (correctedDocument.FieldCorrections.Remove(fieldName))
            {
                correctedDocument.HasUnsavedChanges = true;
                correctedDocument.LastModifiedAt = DateTime.UtcNow;

                // Remove from pending corrections
                if (_pendingCorrections.ContainsKey(documentId))
                {
                    var pendingCorrection = _pendingCorrections[documentId]
                        .FirstOrDefault(c => c.FieldName == fieldName);
                    if (pendingCorrection != null)
                    {
                        _pendingCorrections[documentId].Remove(pendingCorrection);
                    }
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Identifies fields with low confidence that should be highlighted for correction.
        /// </summary>
        /// <param name="document">The processed document.</param>
        /// <param name="confidenceThreshold">The confidence threshold below which fields should be highlighted.</param>
        /// <returns>A collection of field names with low confidence.</returns>
        public async Task<IReadOnlyList<string>> GetLowConfidenceFieldsAsync(ProcessedDocument document, double confidenceThreshold = 0.7)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var lowConfidenceFields = new List<string>();

            // For now, we'll use the overall document confidence as a proxy
            // In a real implementation, this would examine individual field confidence scores
            if (document.ConfidenceScore < confidenceThreshold)
            {
                // Add fields based on document type
                var fields = GetFieldsForDocumentType(document.DocumentType);
                lowConfidenceFields.AddRange(fields);
            }

            return await Task.FromResult(lowConfidenceFields.AsReadOnly());
        }

        /// <summary>
        /// Gets the validation rules for a specific field type.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <param name="documentType">The document type.</param>
        /// <returns>The validation rules for the field.</returns>
        public async Task<FieldValidationRules> GetFieldValidationRulesAsync(string fieldName, string documentType)
        {
            return await _validationService.GetFieldValidationRulesAsync(fieldName, documentType);
        }

        /// <summary>
        /// Gets the field names for a specific document type.
        /// </summary>
        /// <param name="documentType">The document type.</param>
        /// <returns>A collection of field names.</returns>
        private List<string> GetFieldsForDocumentType(string documentType)
        {
            return documentType.ToLower() switch
            {
                "receipt" => new List<string> { "StoreName", "Total", "Subtotal", "TaxAmount", "TransactionDate", "PaymentMethod" },
                "check" => new List<string> { "PayTo", "Amount", "Date", "RoutingNumber", "AccountNumber", "CheckNumber" },
                "w4" => new List<string> { "Name", "SSN", "Address", "FilingStatus", "Allowances" },
                _ => new List<string>()
            };
        }

        /// <summary>
        /// Determines the field type based on field name and document type.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <param name="documentType">The document type.</param>
        /// <returns>The field type.</returns>
        private FieldType DetermineFieldType(string fieldName, string documentType)
        {
            var lowerName = fieldName.ToLower();
            
            if (lowerName.Contains("email")) return FieldType.Email;
            if (lowerName.Contains("phone") || lowerName.Contains("tel")) return FieldType.Phone;
            if (lowerName.Contains("amount") || lowerName.Contains("total") || lowerName.Contains("price") || lowerName.Contains("cost")) return FieldType.Currency;
            if (lowerName.Contains("date") || lowerName.Contains("time")) return FieldType.Date;
            if (lowerName.Contains("percent") || lowerName.Contains("rate")) return FieldType.Percentage;
            if (lowerName.Contains("routing")) return FieldType.RoutingNumber;
            if (lowerName.Contains("account") && lowerName.Contains("number")) return FieldType.AccountNumber;
            if (lowerName.Contains("zip") || lowerName.Contains("postal")) return FieldType.PostalCode;
            if (lowerName.Contains("quantity") || lowerName.Contains("count")) return FieldType.Integer;
            
            return FieldType.Text;
        }

        /// <summary>
        /// Disposes of the correction service and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            _correctionSessions.Clear();
            _pendingCorrections.Clear();
        }
    }
}