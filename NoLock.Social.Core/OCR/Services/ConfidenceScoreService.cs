using System;
using System.Collections.Generic;
using System.Linq;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Service for managing confidence score thresholds and calculations.
    /// </summary>
    public interface IConfidenceScoreService
    {
        /// <summary>
        /// Gets or sets the threshold for high confidence.
        /// </summary>
        double HighConfidenceThreshold { get; set; }

        /// <summary>
        /// Gets or sets the threshold for medium confidence.
        /// </summary>
        double MediumConfidenceThreshold { get; set; }

        /// <summary>
        /// Calculates the overall confidence score for a document.
        /// </summary>
        double CalculateOverallConfidence(ProcessedDocument document, Dictionary<string, double>? fieldConfidences = null);

        /// <summary>
        /// Extracts field confidence scores from a document.
        /// </summary>
        Dictionary<string, double> ExtractFieldConfidences(ProcessedDocument document);

        /// <summary>
        /// Determines if a document meets the minimum confidence threshold.
        /// </summary>
        bool MeetsMinimumConfidence(ProcessedDocument document, double minimumThreshold);

        /// <summary>
        /// Gets confidence statistics for a document.
        /// </summary>
        ConfidenceStatistics GetConfidenceStatistics(ProcessedDocument document);

        /// <summary>
        /// Applies confidence-based validation rules.
        /// </summary>
        ValidationResult ValidateConfidence(ProcessedDocument document);
    }

    /// <summary>
    /// Default implementation of the confidence score service.
    /// </summary>
    public class ConfidenceScoreService : IConfidenceScoreService
    {
        private double _highConfidenceThreshold = 0.80;
        private double _mediumConfidenceThreshold = 0.60;

        /// <summary>
        /// Gets or sets the threshold for high confidence.
        /// </summary>
        public double HighConfidenceThreshold
        {
            get => _highConfidenceThreshold;
            set => _highConfidenceThreshold = Math.Max(0, Math.Min(1, value));
        }

        /// <summary>
        /// Gets or sets the threshold for medium confidence.
        /// </summary>
        public double MediumConfidenceThreshold
        {
            get => _mediumConfidenceThreshold;
            set => _mediumConfidenceThreshold = Math.Max(0, Math.Min(1, value));
        }

        /// <summary>
        /// Calculates the overall confidence score for a document.
        /// </summary>
        public double CalculateOverallConfidence(ProcessedDocument document, Dictionary<string, double>? fieldConfidences = null)
        {
            if (document == null)
                return 0.0;

            // If field confidences are not provided, extract them
            fieldConfidences ??= ExtractFieldConfidences(document);

            // If we have field-level confidences, calculate weighted average
            if (fieldConfidences.Any())
            {
                // Define weights for different field types
                var fieldWeights = GetFieldWeights(document.DocumentType);
                
                var weightedScores = new List<(double score, double weight)>();
                
                foreach (var field in fieldConfidences)
                {
                    var weight = fieldWeights.GetValueOrDefault(field.Key, 1.0);
                    weightedScores.Add((field.Value, weight));
                }

                // Include document-level confidence with higher weight
                weightedScores.Add((document.ConfidenceScore, 2.0));

                return ConfidenceHelper.CalculateWeightedConfidence(weightedScores.ToArray());
            }

            // Fall back to document-level confidence
            return document.ConfidenceScore;
        }

        /// <summary>
        /// Extracts field confidence scores from a document.
        /// </summary>
        public Dictionary<string, double> ExtractFieldConfidences(ProcessedDocument document)
        {
            var fieldConfidences = new Dictionary<string, double>();

            if (document == null)
                return fieldConfidences;

            // Extract based on document type
            switch (document)
            {
                case ProcessedReceipt receipt:
                    ExtractReceiptFieldConfidences(receipt, fieldConfidences);
                    break;
                case ProcessedCheck check:
                    ExtractCheckFieldConfidences(check, fieldConfidences);
                    break;
            }

            return fieldConfidences;
        }

        /// <summary>
        /// Determines if a document meets the minimum confidence threshold.
        /// </summary>
        public bool MeetsMinimumConfidence(ProcessedDocument document, double minimumThreshold)
        {
            if (document == null)
                return false;

            var overallConfidence = CalculateOverallConfidence(document);
            return overallConfidence >= minimumThreshold;
        }

        /// <summary>
        /// Gets confidence statistics for a document.
        /// </summary>
        public ConfidenceStatistics GetConfidenceStatistics(ProcessedDocument document)
        {
            var stats = new ConfidenceStatistics();

            if (document == null)
                return stats;

            var fieldConfidences = ExtractFieldConfidences(document);
            stats.OverallConfidence = CalculateOverallConfidence(document, fieldConfidences);
            stats.DocumentConfidence = document.ConfidenceScore;
            stats.TotalFields = fieldConfidences.Count;

            if (fieldConfidences.Any())
            {
                var confidenceValues = fieldConfidences.Values.ToList();
                stats.AverageFieldConfidence = confidenceValues.Average();
                stats.MinFieldConfidence = confidenceValues.Min();
                stats.MaxFieldConfidence = confidenceValues.Max();

                foreach (var confidence in confidenceValues)
                {
                    var level = GetConfidenceLevel(confidence);
                    switch (level)
                    {
                        case ConfidenceLevel.High:
                            stats.HighConfidenceFields++;
                            break;
                        case ConfidenceLevel.Medium:
                            stats.MediumConfidenceFields++;
                            break;
                        case ConfidenceLevel.Low:
                            stats.LowConfidenceFields++;
                            break;
                    }
                }

                // Find fields needing review (below medium threshold)
                stats.FieldsNeedingReview = fieldConfidences
                    .Where(f => f.Value < MediumConfidenceThreshold)
                    .Select(f => f.Key)
                    .ToList();
            }

            return stats;
        }

        /// <summary>
        /// Applies confidence-based validation rules.
        /// </summary>
        public ValidationResult ValidateConfidence(ProcessedDocument document)
        {
            var result = new ValidationResult { IsValid = true };

            if (document == null)
            {
                result.IsValid = false;
                result.Errors.Add("Document is null");
                return result;
            }

            var stats = GetConfidenceStatistics(document);

            // Check overall confidence
            if (stats.OverallConfidence < 0.5)
            {
                result.IsValid = false;
                result.Errors.Add($"Overall confidence ({stats.OverallConfidence:P0}) is below minimum threshold");
            }

            // Check if too many fields need review
            if (stats.FieldsNeedingReview.Count > stats.TotalFields * 0.5)
            {
                result.Warnings.Add($"More than 50% of fields need review");
            }

            // Check for critical fields with low confidence
            var criticalFields = GetCriticalFields(document.DocumentType);
            var fieldConfidences = ExtractFieldConfidences(document);
            
            foreach (var criticalField in criticalFields)
            {
                if (fieldConfidences.TryGetValue(criticalField, out var confidence))
                {
                    if (confidence < MediumConfidenceThreshold)
                    {
                        result.Warnings.Add($"Critical field '{criticalField}' has low confidence ({confidence:P0})");
                    }
                }
            }

            // Add recommendations
            if (stats.LowConfidenceFields > 0)
            {
                result.Recommendations.Add($"Review {stats.LowConfidenceFields} low-confidence fields");
            }

            if (stats.AverageFieldConfidence < HighConfidenceThreshold)
            {
                result.Recommendations.Add("Consider re-scanning document with better quality");
            }

            return result;
        }

        /// <summary>
        /// Gets the confidence level based on thresholds.
        /// </summary>
        private ConfidenceLevel GetConfidenceLevel(double score)
        {
            if (score >= HighConfidenceThreshold)
                return ConfidenceLevel.High;
            if (score >= MediumConfidenceThreshold)
                return ConfidenceLevel.Medium;
            return ConfidenceLevel.Low;
        }

        /// <summary>
        /// Gets field weights based on document type.
        /// </summary>
        private Dictionary<string, double> GetFieldWeights(string documentType)
        {
            return documentType switch
            {
                "Receipt" => new Dictionary<string, double>
                {
                    { "Total", 3.0 },
                    { "Subtotal", 2.5 },
                    { "TaxAmount", 2.0 },
                    { "StoreName", 1.5 },
                    { "TransactionDate", 1.5 }
                },
                "Check" => new Dictionary<string, double>
                {
                    { "Amount", 3.0 },
                    { "Payee", 2.5 },
                    { "RoutingNumber", 2.0 },
                    { "AccountNumber", 2.0 },
                    { "CheckNumber", 1.5 }
                },
                "W4" => new Dictionary<string, double>
                {
                    { "EmployeeName", 3.0 },
                    { "SSN", 3.0 },
                    { "FilingStatus", 2.0 },
                    { "Allowances", 2.0 }
                },
                _ => new Dictionary<string, double>()
            };
        }

        /// <summary>
        /// Gets critical fields based on document type.
        /// </summary>
        private List<string> GetCriticalFields(string documentType)
        {
            return documentType switch
            {
                "Receipt" => new List<string> { "Total", "Subtotal", "StoreName" },
                "Check" => new List<string> { "Amount", "Payee", "RoutingNumber" },
                "W4" => new List<string> { "EmployeeName", "SSN", "FilingStatus" },
                _ => new List<string>()
            };
        }

        /// <summary>
        /// Extracts receipt field confidences.
        /// </summary>
        private void ExtractReceiptFieldConfidences(ProcessedReceipt receipt, Dictionary<string, double> fieldConfidences)
        {
            if (receipt.ReceiptData == null)
                return;

            // Simulate field-level confidences based on data presence and document confidence
            var baseConfidence = receipt.ConfidenceScore;
            
            // Store name
            if (!string.IsNullOrWhiteSpace(receipt.ReceiptData.StoreName))
                fieldConfidences["StoreName"] = CalculateFieldConfidence(baseConfidence, 0.95);

            // Transaction date
            if (receipt.ReceiptData.TransactionDate.HasValue)
                fieldConfidences["TransactionDate"] = CalculateFieldConfidence(baseConfidence, 0.90);

            // Amounts
            if (receipt.ReceiptData.Total > 0)
                fieldConfidences["Total"] = CalculateFieldConfidence(baseConfidence, 0.92);
            
            if (receipt.ReceiptData.Subtotal > 0)
                fieldConfidences["Subtotal"] = CalculateFieldConfidence(baseConfidence, 0.90);
            
            if (receipt.ReceiptData.TaxAmount > 0)
                fieldConfidences["TaxAmount"] = CalculateFieldConfidence(baseConfidence, 0.88);

            // Receipt number
            if (!string.IsNullOrWhiteSpace(receipt.ReceiptData.ReceiptNumber))
                fieldConfidences["ReceiptNumber"] = CalculateFieldConfidence(baseConfidence, 0.85);

            // Payment method
            if (!string.IsNullOrWhiteSpace(receipt.ReceiptData.PaymentMethod))
                fieldConfidences["PaymentMethod"] = CalculateFieldConfidence(baseConfidence, 0.87);
        }

        /// <summary>
        /// Extracts check field confidences.
        /// </summary>
        private void ExtractCheckFieldConfidences(ProcessedCheck check, Dictionary<string, double> fieldConfidences)
        {
            if (check.CheckData == null)
                return;

            var baseConfidence = check.ConfidenceScore;

            // Amount
            if (check.CheckData.AmountNumeric.HasValue && check.CheckData.AmountNumeric.Value > 0)
                fieldConfidences["Amount"] = CalculateFieldConfidence(baseConfidence, 0.93);

            // Payee name
            if (!string.IsNullOrWhiteSpace(check.CheckData.Payee))
                fieldConfidences["Payee"] = CalculateFieldConfidence(baseConfidence, 0.90);

            // Check number
            if (!string.IsNullOrWhiteSpace(check.CheckData.CheckNumber))
                fieldConfidences["CheckNumber"] = CalculateFieldConfidence(baseConfidence, 0.95);

            // Routing number
            if (!string.IsNullOrWhiteSpace(check.CheckData.RoutingNumber))
                fieldConfidences["RoutingNumber"] = CalculateFieldConfidence(baseConfidence, 
                    check.CheckData.IsRoutingNumberValid ? 0.98 : 0.70);

            // Account number
            if (!string.IsNullOrWhiteSpace(check.CheckData.AccountNumber))
                fieldConfidences["AccountNumber"] = CalculateFieldConfidence(baseConfidence, 0.88);

            // Date
            if (check.CheckData.Date.HasValue)
                fieldConfidences["Date"] = CalculateFieldConfidence(baseConfidence, 0.91);
        }

        /// <summary>
        /// Calculates field confidence based on document confidence and field factor.
        /// </summary>
        private double CalculateFieldConfidence(double baseConfidence, double fieldFactor)
        {
            // Apply some variance to simulate real field-level confidence
            var variance = (Random.Shared.NextDouble() - 0.5) * 0.1; // ±5% variance
            var confidence = baseConfidence * fieldFactor + variance;
            return Math.Max(0, Math.Min(1, confidence));
        }
    }

    /// <summary>
    /// Represents confidence statistics for a document.
    /// </summary>
    public class ConfidenceStatistics
    {
        public double OverallConfidence { get; set; }
        public double DocumentConfidence { get; set; }
        public double AverageFieldConfidence { get; set; }
        public double MinFieldConfidence { get; set; }
        public double MaxFieldConfidence { get; set; }
        public int TotalFields { get; set; }
        public int HighConfidenceFields { get; set; }
        public int MediumConfidenceFields { get; set; }
        public int LowConfidenceFields { get; set; }
        public List<string> FieldsNeedingReview { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents the result of confidence validation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();
    }
}