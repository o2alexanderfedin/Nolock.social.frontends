using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Implements keyword-based document type detection with confidence scoring.
    /// </summary>
    public class DocumentTypeDetector : IDocumentTypeDetector
    {
        private readonly ILogger<DocumentTypeDetector> _logger;
        private readonly Dictionary<string, List<string>> _documentKeywords;
        private readonly Dictionary<string, double> _keywordWeights;
        private readonly Dictionary<string, DocumentTypeDetectionResult> _cache;
        private readonly object _cacheLock = new object();

        /// <inheritdoc />
        public double MinimumConfidenceThreshold { get; }

        /// <summary>
        /// Initializes a new instance of the DocumentTypeDetector class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="minimumConfidenceThreshold">Minimum confidence threshold for automatic detection.</param>
        public DocumentTypeDetector(
            ILogger<DocumentTypeDetector> logger,
            double minimumConfidenceThreshold = 0.7)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            MinimumConfidenceThreshold = minimumConfidenceThreshold;
            _cache = new Dictionary<string, DocumentTypeDetectionResult>();

            // Initialize document keywords
            _documentKeywords = new Dictionary<string, List<string>>
            {
                ["Receipt"] = new List<string>
                {
                    "total", "subtotal", "tax", "receipt", "invoice",
                    "amount due", "payment", "thank you", "items", "qty",
                    "price", "purchase", "sale", "discount", "cashier"
                },
                ["Check"] = new List<string>
                {
                    "pay to", "dollars", "memo", "routing number", "account number",
                    "check number", "date", "signature", "bank", "void after",
                    "endorse", "deposit", "negotiable"
                },
                ["W4"] = new List<string>
                {
                    "w-4", "employee's withholding", "withholding certificate",
                    "social security", "marital status", "dependents", "allowances",
                    "single", "married", "head of household", "exemptions",
                    "federal income tax", "employer", "employee"
                },
                ["W2"] = new List<string>
                {
                    "w-2", "wage and tax statement", "wages tips", "federal income tax",
                    "social security wages", "medicare wages", "employer identification",
                    "ein", "employee's ssn", "state wages", "local wages"
                },
                ["Invoice"] = new List<string>
                {
                    "invoice", "bill to", "ship to", "invoice number", "due date",
                    "terms", "net 30", "purchase order", "quantity", "unit price",
                    "line total", "balance due", "remit to", "amount due"
                },
                ["PayStub"] = new List<string>
                {
                    "pay stub", "earnings statement", "gross pay", "net pay",
                    "deductions", "ytd", "pay period", "hourly rate", "overtime",
                    "federal tax", "state tax", "fica", "401k"
                }
            };

            // Initialize keyword weights (higher weight = more important)
            _keywordWeights = new Dictionary<string, double>
            {
                // Document type specific identifiers (highest weight)
                ["w-4"] = 3.0,
                ["w-2"] = 3.0,
                ["employee's withholding"] = 2.5,
                ["wage and tax statement"] = 2.5,
                
                // Strong indicators
                ["routing number"] = 2.0,
                ["account number"] = 2.0,
                ["pay to"] = 2.0,
                ["invoice number"] = 2.0,
                ["pay stub"] = 2.0,
                
                // Medium indicators
                ["total"] = 1.5,
                ["subtotal"] = 1.5,
                ["tax"] = 1.5,
                ["gross pay"] = 1.5,
                ["net pay"] = 1.5,
                
                // Weak indicators (common across multiple document types)
                ["date"] = 0.5,
                ["signature"] = 0.5,
                ["amount"] = 0.5
            };
        }

        /// <inheritdoc />
        public async Task<DocumentTypeDetectionResult> DetectDocumentTypeAsync(
            string rawOcrData,
            CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                _logger.LogWarning("Empty OCR data provided for document type detection");
                return DocumentTypeDetectionResult.Unknown();
            }

            // Check cache
            var cacheKey = GetCacheKey(rawOcrData);
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey, out var cachedResult))
                {
                    _logger.LogDebug("Returning cached detection result for document");
                    return cachedResult;
                }
            }

            var results = await DetectMultipleDocumentTypesAsync(rawOcrData, 1, cancellation);
            var result = results.FirstOrDefault() ?? DocumentTypeDetectionResult.Unknown();

            // Cache the result
            lock (_cacheLock)
            {
                _cache[cacheKey] = result;
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<DocumentTypeDetectionResult[]> DetectMultipleDocumentTypesAsync(
            string rawOcrData,
            int maxResults = 3,
            CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                _logger.LogWarning("Empty OCR data provided for document type detection");
                return new[] { DocumentTypeDetectionResult.Unknown() };
            }

            return await Task.Run(() =>
            {
                var lowerText = rawOcrData.ToLowerInvariant();
                var results = new List<DocumentTypeDetectionResult>();

                foreach (var documentType in _documentKeywords)
                {
                    cancellation.ThrowIfCancellationRequested();

                    var detectionResult = AnalyzeDocumentType(
                        lowerText,
                        documentType.Key,
                        documentType.Value);

                    if (detectionResult.ConfidenceScore > 0)
                    {
                        results.Add(detectionResult);
                    }
                }

                // Sort by confidence score (descending)
                results.Sort((a, b) => b.ConfidenceScore.CompareTo(a.ConfidenceScore));

                // Check for ambiguous detection
                if (results.Count >= 2)
                {
                    var topScore = results[0].ConfidenceScore;
                    var secondScore = results[1].ConfidenceScore;

                    // If top two scores are very close, it's ambiguous
                    if (Math.Abs(topScore - secondScore) < 0.1)
                    {
                        var ambiguousTypes = results
                            .Where(r => Math.Abs(r.ConfidenceScore - topScore) < 0.1)
                            .Select(r => r.DocumentType)
                            .ToArray();

                        _logger.LogWarning("Ambiguous document type detection: {Types}", 
                            string.Join(", ", ambiguousTypes));

                        var ambiguousResult = DocumentTypeDetectionResult.Ambiguous(ambiguousTypes);
                        return new[] { ambiguousResult };
                    }
                }

                // Take only the requested number of results
                var finalResults = results.Take(maxResults).ToArray();

                // If no results or very low confidence, return unknown
                if (finalResults.Length == 0 || finalResults[0].ConfidenceScore < 0.2)
                {
                    return new[] { DocumentTypeDetectionResult.Unknown() };
                }

                _logger.LogInformation("Detected {Count} document type(s) with top match: {Type} ({Confidence:P})",
                    finalResults.Length,
                    finalResults[0].DocumentType,
                    finalResults[0].ConfidenceScore);

                return finalResults;
            }, cancellation);
        }

        /// <inheritdoc />
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
                _logger.LogDebug("Document type detection cache cleared");
            }
        }

        /// <summary>
        /// Analyzes text for a specific document type and returns detection result.
        /// </summary>
        private DocumentTypeDetectionResult AnalyzeDocumentType(
            string lowerText,
            string documentType,
            List<string> keywords)
        {
            var matchedKeywords = new List<string>();
            double totalWeight = 0;
            double maxPossibleWeight = 0;

            foreach (var keyword in keywords)
            {
                var weight = _keywordWeights.ContainsKey(keyword) 
                    ? _keywordWeights[keyword] 
                    : 1.0;

                maxPossibleWeight += weight;

                if (lowerText.Contains(keyword))
                {
                    matchedKeywords.Add(keyword);
                    totalWeight += weight;
                }
            }

            // Calculate confidence score based on weighted matches
            var confidenceScore = maxPossibleWeight > 0 
                ? totalWeight / maxPossibleWeight 
                : 0;

            // Apply bonus for high match count
            if (matchedKeywords.Count >= 5)
            {
                confidenceScore = Math.Min(1.0, confidenceScore * 1.1);
            }

            var result = new DocumentTypeDetectionResult
            {
                DocumentType = documentType,
                ConfidenceScore = Math.Round(confidenceScore, 3),
                MatchedKeywords = matchedKeywords,
                KeywordMatchCount = matchedKeywords.Count,
                DetectionMethod = "Keyword",
                IsConfident = confidenceScore >= MinimumConfidenceThreshold,
                RequiresManualConfirmation = confidenceScore < MinimumConfidenceThreshold,
                DetectedAt = DateTime.UtcNow
            };

            if (result.RequiresManualConfirmation)
            {
                result.ManualConfirmationReason = $"Confidence score {confidenceScore:P} is below threshold {MinimumConfidenceThreshold:P}";
            }

            return result;
        }

        /// <summary>
        /// Generates a cache key for the given OCR text.
        /// </summary>
        private string GetCacheKey(string rawOcrData)
        {
            // Use first 100 chars and length as simple cache key
            var prefix = rawOcrData.Length > 100 
                ? rawOcrData.Substring(0, 100) 
                : rawOcrData;
            return $"{prefix.GetHashCode()}_{rawOcrData.Length}";
        }
    }
}