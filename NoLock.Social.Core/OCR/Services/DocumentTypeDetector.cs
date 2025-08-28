using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using CameraDocumentType = NoLock.Social.Core.Camera.Models.DocumentType;

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

            // Initialize document keywords - using enum names as keys
            _documentKeywords = new Dictionary<string, List<string>>
            {
                [CameraDocumentType.Receipt.ToString()] = new List<string>
                {
                    "total", "subtotal", "tax", "receipt",
                    "payment", "thank you", "items", "qty",
                    "price", "purchase", "sale", "discount", "cashier",
                    "transaction", "change"
                },
                [CameraDocumentType.Check.ToString()] = new List<string>
                {
                    "pay to", "pay to the order", "dollars", "memo", "routing number", "account number",
                    "check number", "bank", "void after",
                    "endorse", "deposit", "negotiable"
                },
                [CameraDocumentType.W4.ToString()] = new List<string>
                {
                    "w-4", "w4", "form w-4", "employee's withholding", "withholding certificate",
                    "withholding", "social security", "marital status", "dependents", "allowances",
                    "single", "married", "head of household", "exemptions",
                    "federal income tax", "employer", "employee"
                },
                [CameraDocumentType.W2.ToString()] = new List<string>
                {
                    "w-2", "w2", "form w-2", "wage and tax statement", "wages tips", 
                    "federal income tax", "social security wages", "medicare wages", 
                    "employer identification", "ein", "employee's ssn", "state wages", "local wages"
                },
                [CameraDocumentType.Form1099.ToString()] = new List<string>
                {
                    "1099", "form 1099", "1099-misc", "1099-int", "1099-div",
                    "nonemployee compensation", "payer", "recipient", "federal income tax withheld",
                    "rents", "royalties", "other income", "self-employment"
                },
                [CameraDocumentType.Invoice.ToString()] = new List<string>
                {
                    "invoice", "bill to", "ship to", "invoice number", "due date",
                    "terms", "net 30", "purchase order", "quantity", "unit price",
                    "line total", "balance due", "remit to", "amount due", "payment terms"
                }
            };

            // Initialize keyword weights (higher weight = more important)
            _keywordWeights = new Dictionary<string, double>
            {
                // Document type specific identifiers (highest weight)
                ["w-4"] = 3.0,
                ["w4"] = 3.0,
                ["form w-4"] = 3.0,
                ["w-2"] = 3.0,
                ["w2"] = 3.0,
                ["form w-2"] = 3.0,
                ["form 1099"] = 3.0,
                ["1099"] = 3.0,
                ["employee's withholding"] = 2.5,
                ["wage and tax statement"] = 2.5,
                ["withholding"] = 1.8,
                
                // Strong indicators
                ["receipt"] = 2.2,
                ["invoice"] = 2.2,
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
                return new DocumentTypeDetectionResult
                {
                    DocumentType = CameraDocumentType.Other.ToString(),
                    ConfidenceScore = 0.0,
                    MatchedKeywords = new List<string>(),
                    KeywordMatchCount = 0,
                    DetectionMethod = "None",
                    IsConfident = false,
                    RequiresManualConfirmation = true,
                    ManualConfirmationReason = "No OCR data provided",
                    DetectedAt = DateTime.UtcNow
                };
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
            var result = results.FirstOrDefault() ?? new DocumentTypeDetectionResult
            {
                DocumentType = CameraDocumentType.Other.ToString(),
                ConfidenceScore = 0.0,
                MatchedKeywords = new List<string>(),
                KeywordMatchCount = 0,
                DetectionMethod = "None",
                IsConfident = false,
                RequiresManualConfirmation = true,
                ManualConfirmationReason = "Unable to detect document type",
                DetectedAt = DateTime.UtcNow
            };

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
                return new[] { new DocumentTypeDetectionResult
                {
                    DocumentType = CameraDocumentType.Other.ToString(),
                    ConfidenceScore = 0.0,
                    MatchedKeywords = new List<string>(),
                    KeywordMatchCount = 0,
                    DetectionMethod = "None",
                    IsConfident = false,
                    RequiresManualConfirmation = true,
                    ManualConfirmationReason = "No OCR data provided",
                    DetectedAt = DateTime.UtcNow
                } };
            }

            // Ensure maxResults is at least 1
            if (maxResults <= 0)
                maxResults = 1;

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
                        _logger.LogDebug($"Added result: {detectionResult.DocumentType} with confidence {detectionResult.ConfidenceScore}");
                    }
                    else
                    {
                        _logger.LogDebug($"Skipped result: {detectionResult.DocumentType} with confidence {detectionResult.ConfidenceScore}");
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
                if (finalResults.Length == 0 || finalResults[0].ConfidenceScore < 0.1)
                {
                    return new[] { new DocumentTypeDetectionResult
                    {
                        DocumentType = CameraDocumentType.Other.ToString(),
                        ConfidenceScore = 0.0,
                        MatchedKeywords = new List<string>(),
                        KeywordMatchCount = 0,
                        DetectionMethod = "None",
                        IsConfident = false,
                        RequiresManualConfirmation = true,
                        ManualConfirmationReason = "Unable to detect document type with sufficient confidence",
                        DetectedAt = DateTime.UtcNow
                    } };
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
            double maxSingleWeight = 0;
            int strongMatches = 0;
            int totalOccurrences = 0;
            
            _logger.LogDebug($"Analyzing document type: {documentType} with text: {lowerText.Substring(0, Math.Min(50, lowerText.Length))}...");

            foreach (var keyword in keywords)
            {
                var weight = _keywordWeights.ContainsKey(keyword) 
                    ? _keywordWeights[keyword] 
                    : 1.0;

                // Track the maximum single keyword weight for this document type
                maxSingleWeight = Math.Max(maxSingleWeight, weight);
                
                _logger.LogDebug($"Checking keyword '{keyword}' against text...");

                if (ContainsKeyword(lowerText, keyword))
                {
                    _logger.LogDebug($"Matched keyword '{keyword}' for {documentType}");
                    matchedKeywords.Add(keyword);
                    
                    // Count occurrences for repeated keywords
                    int occurrences = CountOccurrences(lowerText, keyword);
                    totalOccurrences += occurrences;
                    
                    // Weight increases with occurrences but with diminishing returns
                    double occurrenceMultiplier = 1.0 + Math.Log10(Math.Max(1, occurrences)) * 0.3;
                    totalWeight += weight * occurrenceMultiplier;
                    
                    // Count strong matches (weight >= 2.0)
                    if (weight >= 2.0)
                    {
                        strongMatches++;
                    }
                }
            }

            // Calculate confidence score with a more balanced approach:
            // - Base score from total weight (normalized by a reasonable expectation, not all keywords)
            // - Consider that matching 3-4 keywords with good weights should give high confidence
            // - Strong document identifiers (weight >= 3) should give immediate high confidence
            double confidenceScore = 0;
            
            // Check if any matched keyword has very high weight (>= 3.0)
            bool hasVeryStrongMatch = false;
            double maxMatchedWeight = 0;
            foreach (var keyword in matchedKeywords)
            {
                var weight = _keywordWeights.ContainsKey(keyword) ? _keywordWeights[keyword] : 1.0;
                maxMatchedWeight = Math.Max(maxMatchedWeight, weight);
                if (weight >= 3.0)
                {
                    hasVeryStrongMatch = true;
                    break;
                }
            }

            if (matchedKeywords.Count == 0)
            {
                confidenceScore = 0;
            }
            else if (hasVeryStrongMatch)
            {
                // Has very strong identifier(s) like "w-4", "w-2", "form 1099"
                confidenceScore = Math.Min(1.0, 0.85 + (matchedKeywords.Count - 1) * 0.05);
            }
            else if (strongMatches >= 2)
            {
                // Multiple strong matches (weight >= 2.0)
                confidenceScore = Math.Min(1.0, 0.7 + (strongMatches * 0.1));
            }
            else if (strongMatches >= 1)
            {
                // At least one strong match
                confidenceScore = Math.Min(1.0, 0.5 + (totalWeight * 0.1) + (matchedKeywords.Count * 0.05));
            }
            else if (matchedKeywords.Count >= 3)
            {
                // Multiple regular matches
                confidenceScore = Math.Min(1.0, 0.4 + (totalWeight * 0.12));
            }
            else if (matchedKeywords.Count >= 2)
            {
                // Two matches
                confidenceScore = Math.Min(0.7, 0.3 + (totalWeight * 0.15));
            }
            else if (matchedKeywords.Count == 1)
            {
                // Single match, scale based on weight and repetitions
                // Give higher confidence for strong single matches like "invoice"
                if (maxMatchedWeight >= 2.0)
                {
                    // If repeated multiple times (like "invoice invoice invoice"), boost confidence
                    if (totalOccurrences >= 3)
                    {
                        confidenceScore = Math.Min(0.8, 0.5 + (totalWeight * 0.12));
                    }
                    else
                    {
                        confidenceScore = Math.Min(0.7, 0.4 + (totalWeight * 0.15));
                    }
                }
                else
                {
                    confidenceScore = Math.Min(0.6, 0.2 + (totalWeight * 0.2));
                }
            }

            // Apply bonus for high match count
            if (matchedKeywords.Count >= 5)
            {
                confidenceScore = Math.Min(1.0, confidenceScore * 1.15);
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
            
            _logger.LogDebug($"Result for {documentType}: Confidence={confidenceScore}, Matches={matchedKeywords.Count}");

            if (result.RequiresManualConfirmation)
            {
                result.ManualConfirmationReason = $"Confidence score {confidenceScore:P} is below threshold {MinimumConfidenceThreshold:P}";
            }

            return result;
        }

        /// <summary>
        /// Counts the number of occurrences of a keyword in the text.
        /// </summary>
        private int CountOccurrences(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
                return 0;
                
            int count = 0;
            int index = 0;
            
            while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                count++;
                index += keyword.Length;
            }
            
            return count;
        }
        
        /// <summary>
        /// Checks if the text contains the keyword.
        /// </summary>
        private bool ContainsKeyword(string text, string keyword)
        {
            // Both text and keywords are already lowercase, so we can do a simple contains check
            return text.Contains(keyword);
        }
        
        /// <summary>
        /// Generates a cache key for the given OCR text.
        /// </summary>
        private string GetCacheKey(string rawOcrData)
        {
            // Use full string hash for better accuracy in cache keys
            return $"{rawOcrData.GetHashCode()}_{rawOcrData.Length}";
        }
    }
}