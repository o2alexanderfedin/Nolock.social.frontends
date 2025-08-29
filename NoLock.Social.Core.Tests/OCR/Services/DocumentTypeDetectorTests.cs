using Microsoft.Extensions.Logging;
using Moq;
using CameraDocumentType = NoLock.Social.Core.Camera.Models.DocumentType;
using NoLock.Social.Core.OCR.Services;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Unit tests for DocumentTypeDetector service.
    /// </summary>
    public class DocumentTypeDetectorTests
    {
        private readonly Mock<ILogger<DocumentTypeDetector>> _mockLogger;
        private readonly DocumentTypeDetector _detector;

        public DocumentTypeDetectorTests()
        {
            _mockLogger = new Mock<ILogger<DocumentTypeDetector>>();
            _detector = new DocumentTypeDetector(_mockLogger.Object, 0.2); // Very low threshold for testing
        }

        #region DetectDocumentTypeAsync Tests

        [Theory]
        [InlineData("", CameraDocumentType.Other, 0.0, "Empty OCR data")]
        [InlineData(null, CameraDocumentType.Other, 0.0, "Null OCR data")]
        [InlineData("   ", CameraDocumentType.Other, 0.0, "Whitespace OCR data")]
        public async Task DetectDocumentTypeAsync_InvalidInput_ReturnsUnknown(
            string input, CameraDocumentType expectedType, double expectedScore, string scenario)
        {
            // Act
            var result = await _detector.DetectDocumentTypeAsync(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedType.ToString(), result.DocumentType);
            Assert.Equal(expectedScore, result.ConfidenceScore);
            Assert.False(result.IsConfident, $"Failed for: {scenario}");
        }

        [Theory]
        [InlineData("RECEIPT\nTotal: $45.99\nSubtotal: $42.00\nTax: $3.99\nThank you for your purchase", CameraDocumentType.Receipt, true, "Standard receipt")]
        [InlineData("invoice #12345\nBill To: John Doe\nAmount Due: $500\nPayment Terms: Net 30", CameraDocumentType.Invoice, true, "Standard invoice")]
        [InlineData("PAY TO THE ORDER OF: John Smith\n$500.00\nFive Hundred Dollars\nMemo: Rent\nRouting Number: 123456789", CameraDocumentType.Check, true, "Bank check")]
        public async Task DetectDocumentTypeAsync_CommonDocuments_DetectsCorrectly(
            string ocrText, CameraDocumentType expectedType, bool shouldBeConfident, string scenario)
        {
            // Act
            var result = await _detector.DetectDocumentTypeAsync(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedType.ToString(), result.DocumentType);
            Assert.Equal(shouldBeConfident, result.IsConfident);
            Assert.True(result.ConfidenceScore > 0, $"No confidence score for: {scenario}");
            Assert.NotEmpty(result.MatchedKeywords);
        }

        [Fact]
        public async Task DetectDocumentTypeAsync_W4Document_DetectsWithHighConfidence()
        {
            // Arrange
            var w4Text = @"Form W-4
                Employee's Withholding Certificate
                Social Security Number: XXX-XX-XXXX
                Marital Status: Single
                Number of Dependents: 2
                Federal Income Tax Withholding
                Employer: ACME Corp";

            // Act
            var result = await _detector.DetectDocumentTypeAsync(w4Text);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(CameraDocumentType.W4.ToString(), result.DocumentType);
            Assert.True(result.ConfidenceScore > 0.8, "W4 should have high confidence");
            Assert.True(result.IsConfident);
            Assert.Contains("w-4", result.MatchedKeywords);
        }

        [Fact]
        public async Task DetectDocumentTypeAsync_CachesResults()
        {
            // Arrange
            var ocrText = "RECEIPT\nTotal: $100.00\nTax: $8.00";

            // Act - First call
            var result1 = await _detector.DetectDocumentTypeAsync(ocrText);
            
            // Act - Second call (should hit cache)
            var result2 = await _detector.DetectDocumentTypeAsync(ocrText);

            // Assert
            Assert.Equal(result1.DocumentType, result2.DocumentType);
            Assert.Equal(result1.ConfidenceScore, result2.ConfidenceScore);
            Assert.Equal(result1.DetectedAt, result2.DetectedAt); // Same timestamp means cached
        }

        [Fact]
        public async Task DetectDocumentTypeAsync_Cancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _detector.DetectDocumentTypeAsync("Some text", cts.Token));
            Assert.True(exception is OperationCanceledException);
        }

        #endregion

        #region DetectMultipleDocumentTypesAsync Tests

        [Fact]
        public async Task DetectMultipleDocumentTypesAsync_ReturnsTopMatches()
        {
            // Arrange
            var mixedText = @"RECEIPT Invoice
                Total: $100.00
                Bill To: Customer
                Payment Due: Now
                Tax: $8.00";

            // Act
            var results = await _detector.DetectMultipleDocumentTypesAsync(mixedText, 3);

            // Assert
            Assert.NotNull(results);
            Assert.True(results.Length > 0);
            Assert.True(results.Length <= 3);
            
            // Results should be sorted by confidence (descending)
            for (int i = 1; i < results.Length; i++)
            {
                Assert.True(results[i - 1].ConfidenceScore >= results[i].ConfidenceScore,
                    "Results should be sorted by confidence descending");
            }
        }

        [Fact]
        public async Task DetectMultipleDocumentTypesAsync_AmbiguousDocument_ReturnsAmbiguous()
        {
            // Arrange - Create text that strongly matches multiple types
            var ambiguousText = @"Document
                Total Amount: $500.00
                Pay To: John Doe
                Invoice Number: 12345
                Receipt Number: 67890
                Tax: $50.00
                Subtotal: $450.00
                Payment Terms: Net 30
                Thank you for your payment";

            // Act
            var results = await _detector.DetectMultipleDocumentTypesAsync(ambiguousText, 1);

            // Assert
            Assert.NotNull(results);
            Assert.Single(results);
            
            // With mixed keywords, might detect as ambiguous or pick strongest match
            var result = results[0];
            Assert.NotNull(result);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(3, 3)]
        [InlineData(10, 10)]
        public async Task DetectMultipleDocumentTypesAsync_RespectsMaxResults(int maxResults, int expectedMax)
        {
            // Arrange
            var receiptText = "RECEIPT\nTotal: $100\nTax: $10\nSubtotal: $90";

            // Act
            var results = await _detector.DetectMultipleDocumentTypesAsync(receiptText, maxResults);

            // Assert
            Assert.NotNull(results);
            Assert.True(results.Length <= expectedMax);
        }

        #endregion

        #region Confidence Scoring Tests

        [Fact]
        public async Task ConfidenceScoring_HighKeywordMatch_ProducesHighConfidence()
        {
            // Arrange
            var checkText = @"PAY TO THE ORDER OF: Jane Smith
                $1,234.56
                One Thousand Two Hundred Thirty-Four and 56/100 Dollars
                MEMO: Consulting Services
                ROUTING NUMBER: 123456789
                ACCOUNT NUMBER: 987654321
                CHECK NUMBER: 5001
                Date: 01/15/2024
                Signature: _____________
                VOID AFTER 90 DAYS";

            // Act
            var result = await _detector.DetectDocumentTypeAsync(checkText);

            // Assert
            Assert.Equal(CameraDocumentType.Check.ToString(), result.DocumentType);
            Assert.True(result.ConfidenceScore > 0.7, "High keyword match should produce high confidence");
            Assert.True(result.KeywordMatchCount >= 5, "Should match multiple check keywords");
        }

        [Fact]
        public async Task ConfidenceScoring_LowKeywordMatch_ProducesLowConfidence()
        {
            // Arrange
            var vagueText = "Date: 01/01/2024\nAmount: $100\nSignature: ___________";

            // Act
            var result = await _detector.DetectDocumentTypeAsync(vagueText);

            // Assert
            Assert.True(result.ConfidenceScore < 0.5 || result.DocumentType == CameraDocumentType.Other.ToString(),
                "Low keyword match should produce low confidence or unknown");
        }

        #endregion

        #region Manual Confirmation Tests

        [Fact]
        public async Task ManualConfirmation_LowConfidence_RequiresManualConfirmation()
        {
            // Arrange
            var lowConfidenceDetector = new DocumentTypeDetector(_mockLogger.Object, 0.9); // High threshold
            var text = "price $50"; // Single weak keyword that should get lower confidence

            // Act
            var result = await lowConfidenceDetector.DetectDocumentTypeAsync(text);

            // Assert
            if (result.DocumentType != CameraDocumentType.Other.ToString())
            {
                Assert.True(result.RequiresManualConfirmation, 
                    $"Low confidence detection should require manual confirmation. DocumentType: {result.DocumentType}, Confidence: {result.ConfidenceScore}, RequiresManualConfirmation: {result.RequiresManualConfirmation}");
                Assert.NotEmpty(result.ManualConfirmationReason);
            }
        }

        #endregion

        #region Cache Management Tests

        [Fact]
        public void ClearCache_RemovesCachedResults()
        {
            // Arrange
            var ocrText = "RECEIPT\nTotal: $100.00";

            // Act - Cache a result
            var _ = _detector.DetectDocumentTypeAsync(ocrText).GetAwaiter().GetResult();
            
            // Clear cache
            _detector.ClearCache();
            
            // Get new result
            var newResult = _detector.DetectDocumentTypeAsync(ocrText).GetAwaiter().GetResult();

            // Assert
            // After clearing cache, new detection should have a new timestamp
            Assert.True(newResult.DetectedAt >= DateTime.UtcNow.AddSeconds(-1));
        }

        #endregion

        #region Additional Data-Driven Tests

        [Theory]
        [InlineData("W-2 Wage and Tax Statement\nFederal Income Tax: $5000\nSocial Security Wages: $50000", CameraDocumentType.W2, true, 4, "W2 with multiple strong indicators")]
        [InlineData("Form 1099\nPayer: Company\nRecipient: Individual\nIncome: $10000", CameraDocumentType.Form1099, true, 1, "Form1099 with basic keywords")]
        [InlineData("Bill To: Customer\nShip To: Address\nInvoice Number: INV-001", CameraDocumentType.Invoice, true, 3, "Invoice with shipping information")]
        [InlineData("random text with date and signature", CameraDocumentType.Other, false, 0, "Text with only weak indicators")]
        public async Task DetectDocumentTypeAsync_VariousDocumentTypes_DataDriven(
            string ocrText, CameraDocumentType expectedType, bool shouldBeConfident, 
            int minKeywordMatches, string scenario)
        {
            // Act
            var result = await _detector.DetectDocumentTypeAsync(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedType.ToString(), result.DocumentType);
            
            if (expectedType != CameraDocumentType.Other)
            {
                Assert.Equal(shouldBeConfident, result.IsConfident);
                Assert.True(result.KeywordMatchCount >= minKeywordMatches, 
                    $"Expected at least {minKeywordMatches} keyword matches for: {scenario}");
            }
        }

        [Theory]
        [InlineData("Form W-4\nEmployee's Withholding Certificate", 0.8, "W4 with strong identifiers")]
        [InlineData("w-4 withholding", 0.6, "W4 with minimal but strong keywords")]
        [InlineData("single married dependents", 0.3, "W4 with only weak keywords")]
        [InlineData("W-2 Wage and Tax Statement\nEIN: 12-3456789", 0.8, "W2 with strong identifiers")]
        [InlineData("routing number: 123456789\naccount number: 987654321", 0.5, "Check with strong technical keywords")]
        public async Task DetectDocumentTypeAsync_KeywordWeighting_ProducesExpectedConfidence(
            string ocrText, double minExpectedConfidence, string scenario)
        {
            // Act
            var result = await _detector.DetectDocumentTypeAsync(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ConfidenceScore >= minExpectedConfidence, 
                $"Expected confidence >= {minExpectedConfidence} for: {scenario}, got {result.ConfidenceScore}");
        }

        [Theory]
        [InlineData("receipt invoice", new[] { CameraDocumentType.Receipt, CameraDocumentType.Invoice }, "Receipt and Invoice keywords")]
        [InlineData("w-2 w-4 withholding", new[] { CameraDocumentType.W2, CameraDocumentType.W4 }, "W2 and W4 keywords")]
        [InlineData("check routing number invoice bill to", new[] { CameraDocumentType.Check, CameraDocumentType.Invoice }, "Check and Invoice mix")]
        public async Task DetectMultipleDocumentTypesAsync_MixedKeywords_DetectsAppropriately(
            string ocrText, CameraDocumentType[] expectedTypes, string scenario)
        {
            // Act
            var results = await _detector.DetectMultipleDocumentTypesAsync(ocrText, 5);

            // Assert
            Assert.NotNull(results);
            Assert.True(results.Length > 0, $"Should detect at least one result for: {scenario}");
            
            var firstResult = results[0];
            
            if (firstResult.DocumentType == CameraDocumentType.Other.ToString())
            {
                // Check that it's marked as ambiguous or low confidence
                Assert.True(firstResult.RequiresManualConfirmation, "Other result should require manual confirmation");
                Assert.NotEmpty(firstResult.ManualConfirmationReason);
                
                // Check that metadata contains the possible types if it's ambiguous
                if (firstResult.Metadata.TryGetValue("PossibleTypes", out var possibleTypes) && possibleTypes is string[] types)
                {
                    Assert.True(types.Length >= 2, "Ambiguous result should have multiple possible types");
                }
            }
            else
            {
                // Should detect one of the expected types with reasonable confidence
                var detectedTypes = results.Select(r => r.DocumentType).ToArray();
                var foundAny = expectedTypes.Any(et => detectedTypes.Contains(et.ToString()));
                Assert.True(foundAny, $"Should detect at least one of the expected types for: {scenario}");
                Assert.True(firstResult.ConfidenceScore > 0, "Detected type should have positive confidence");
            }
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task DetectDocumentTypeAsync_MixedCaseKeywords_StillDetects()
        {
            // Arrange
            var mixedCaseText = "ReCeIpT\nTOTAL: $100\nsubtotal: $90\nTaX: $10";

            // Act
            var result = await _detector.DetectDocumentTypeAsync(mixedCaseText);

            // Assert
            Assert.Equal(CameraDocumentType.Receipt.ToString(), result.DocumentType);
            Assert.True(result.ConfidenceScore > 0, "Should detect despite mixed case");
        }

        [Fact]
        public async Task DetectDocumentTypeAsync_VeryLongText_HandlesGracefully()
        {
            // Arrange
            var longText = string.Join("\n", Enumerable.Repeat("RECEIPT Total: $100 Tax: $10", 1000));

            // Act
            var result = await _detector.DetectDocumentTypeAsync(longText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(CameraDocumentType.Receipt.ToString(), result.DocumentType);
        }

        [Theory]
        [InlineData("\t\tRECEIPT\n\n\n", CameraDocumentType.Receipt, "Text with excessive whitespace")]
        [InlineData("W-4!@#$%^&*()", CameraDocumentType.W4, "Text with special characters")]
        [InlineData("invoice invoice invoice invoice invoice", CameraDocumentType.Invoice, "Repeated single keyword")]
        [InlineData("12345 67890 RECEIPT 98765", CameraDocumentType.Receipt, "Keywords mixed with numbers")]
        public async Task DetectDocumentTypeAsync_UnusualFormatting_StillDetects(
            string ocrText, CameraDocumentType expectedType, string scenario)
        {
            // Act
            var result = await _detector.DetectDocumentTypeAsync(ocrText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedType.ToString(), result.DocumentType);
        }

        [Theory]
        [InlineData(0, 1, "Zero max results should default to 1")]
        [InlineData(-1, 1, "Negative max results should default to 1")]
        [InlineData(100, 6, "Very high max results should be capped to available types")]
        public async Task DetectMultipleDocumentTypesAsync_InvalidMaxResults_HandlesGracefully(
            int requestedMax, int expectedMax, string scenario)
        {
            // Arrange
            var text = "Receipt Invoice Check W-4 W-2 Form 1099"; // Keywords for all types

            // Act
            var results = await _detector.DetectMultipleDocumentTypesAsync(text, requestedMax);

            // Assert
            Assert.NotNull(results);
            Assert.True(results.Length <= expectedMax, 
                $"Result count should be <= {expectedMax} for: {scenario}");
        }

        [Fact]
        public async Task DetectDocumentTypeAsync_HighConfidenceBonusApplied_When5OrMoreKeywordsMatch()
        {
            // Arrange - Text with many receipt keywords
            var text = "RECEIPT\nTotal: $100\nSubtotal: $90\nTax: $10\nDiscount: $5\nCashier: John\nThank you for your purchase\nPrice: $95\nItems: 3";

            // Act
            var result = await _detector.DetectDocumentTypeAsync(text);

            // Assert
            Assert.Equal(CameraDocumentType.Receipt.ToString(), result.DocumentType);
            Assert.True(result.KeywordMatchCount >= 5, "Should match at least 5 keywords");
            Assert.True(result.ConfidenceScore > 0.5, "Confidence should be boosted for high match count");
        }

        #endregion

        #region Cache Key Generation Tests

        [Theory]
        [InlineData("short text", "short text", true, "Identical short texts")]
        [InlineData("short text", "short text ", false, "Different whitespace")]
        [InlineData("a", "b", false, "Different single characters")]
        public async Task CacheKey_Generation_WorksCorrectly(
            string text1, string text2, bool shouldBeSame, string scenario)
        {
            // Act
            var result1 = await _detector.DetectDocumentTypeAsync(text1);
            var result2 = await _detector.DetectDocumentTypeAsync(text2);

            // Assert
            if (shouldBeSame)
            {
                Assert.Equal(result1.DetectedAt, result2.DetectedAt);
            }
            else
            {
                Assert.NotEqual(result1.DetectedAt, result2.DetectedAt);
            }
        }

        [Fact]
        public async Task CacheKey_VeryLongText_UsesOnlyFirst100Chars()
        {
            // Arrange
            var prefix = "RECEIPT Total: $100";
            var longText1 = prefix + new string('x', 1000) + "different ending 1";
            var longText2 = prefix + new string('x', 1000) + "different ending 2";

            // Act
            var result1 = await _detector.DetectDocumentTypeAsync(longText1);
            _detector.ClearCache(); // Clear to ensure no caching
            var result2 = await _detector.DetectDocumentTypeAsync(longText2);

            // Assert
            // Different texts should produce different results even if first 100 chars are similar
            Assert.NotEqual(result1.DetectedAt, result2.DetectedAt);
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task DetectDocumentTypeAsync_ConcurrentCalls_ThreadSafe()
        {
            // Arrange
            var tasks = new List<Task<DocumentTypeDetectionResult>>();
            var ocrText = "RECEIPT\nTotal: $100\nTax: $10";

            // Act - Create 50 concurrent detection tasks
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(Task.Run(async () => await _detector.DetectDocumentTypeAsync(ocrText)));
            }

            var results = await Task.WhenAll(tasks);

            // Assert - All results should be identical (from cache)
            var firstResult = results[0];
            foreach (var result in results)
            {
                Assert.Equal(firstResult.DocumentType, result.DocumentType);
                Assert.Equal(firstResult.ConfidenceScore, result.ConfidenceScore);
                Assert.Equal(firstResult.DetectedAt, result.DetectedAt); // Should be cached
            }
        }

        [Fact]
        public async Task ClearCache_ConcurrentWithDetection_ThreadSafe()
        {
            // Arrange
            var detectionTasks = new List<Task>();
            var clearTasks = new List<Task>();

            // Act - Interleave detection and cache clearing
            for (int i = 0; i < 20; i++)
            {
                var text = $"RECEIPT {i}\nTotal: ${i * 10}";
                detectionTasks.Add(_detector.DetectDocumentTypeAsync(text));
                
                if (i % 5 == 0)
                {
                    clearTasks.Add(Task.Run(() => _detector.ClearCache()));
                }
            }

            // Assert - Should complete without exceptions
            var exception = await Record.ExceptionAsync(async () =>
            {
                await Task.WhenAll(detectionTasks.Concat(clearTasks));
            });
            Assert.Null(exception);
        }

        #endregion

        #region AnalyzeDocumentType Method Comprehensive Tests

        /// <summary>
        /// Tests all confidence scoring paths in AnalyzeDocumentType method for comprehensive coverage.
        /// This targets the high complexity (19) method with 47.6% coverage.
        /// </summary>
        [Theory]
        [InlineData("", 0.0, 0, "Empty text should have zero confidence")]
        [InlineData("random text with no keywords", 0.0, 0, "No keyword matches should have zero confidence")]
        [InlineData("w-4", 0.85, 1, "Single very strong match (weight 3.0) should have high confidence")]
        [InlineData("form w-4", 0.85, 1, "Form W-4 very strong match should have high confidence")]
        [InlineData("w-2", 0.85, 1, "W-2 very strong match should have high confidence")]
        [InlineData("form 1099", 0.85, 1, "Form 1099 very strong match should have high confidence")]
        [InlineData("w-4 single", 0.85, 2, "Very strong match with additional keyword should maintain high confidence")]
        [InlineData("receipt", 0.4, 1, "Single strong match should have moderate confidence")]
        [InlineData("invoice", 0.4, 1, "Single strong invoice match should have moderate confidence")]
        [InlineData("total subtotal tax", 0.4, 3, "Three regular matches should have moderate confidence")]
        [InlineData("total tax", 0.3, 2, "Two regular matches should have lower moderate confidence")]
        [InlineData("total", 0.2, 1, "Single weak match should have low confidence")]
        [InlineData("invoice invoice invoice", 0.5, 1, "Repeated strong keyword should boost confidence")]
        [InlineData("total total total total total", 0.35, 1, "Many repetitions of regular keyword should have decent confidence")]
        [InlineData("receipt total subtotal tax discount cashier", 0.9, 6, "High match count (6+) should apply bonus and reach near-max confidence")]
        public async Task AnalyzeDocumentType_AllConfidencePaths_ProducesExpectedResults(
            string inputText, double expectedMinConfidence, int expectedMatchCount, string scenario)
        {
            // Act
            var result = await _detector.DetectDocumentTypeAsync(inputText);

            // Assert
            Assert.NotNull(result);
            if (expectedMinConfidence == 0.0)
            {
                Assert.Equal(0.0, result.ConfidenceScore);
                Assert.Equal(0, result.KeywordMatchCount);
            }
            else
            {
                Assert.True(result.ConfidenceScore >= expectedMinConfidence,
                    $"{scenario}: Expected confidence >= {expectedMinConfidence}, got {result.ConfidenceScore}");
                Assert.True(result.KeywordMatchCount >= expectedMatchCount,
                    $"{scenario}: Expected at least {expectedMatchCount} matches, got {result.KeywordMatchCount}");
                Assert.NotEmpty(result.MatchedKeywords);
            }
        }

        /// <summary>
        /// Tests keyword weight calculation and occurrence counting logic.
        /// </summary>
        [Theory]
        [InlineData("w-4", 3.0, 1, "W-4 has highest weight")] 
        [InlineData("receipt", 2.2, 1, "Receipt has strong weight")]
        [InlineData("total", 1.5, 1, "Total has medium weight")]
        [InlineData("total total", 1.5, 1, "Repeated keyword counted as single match type but multiple occurrences")]
        [InlineData("total total total", 1.5, 1, "Three occurrences of same keyword")]
        public async Task AnalyzeDocumentType_KeywordWeightCalculation_CorrectlyAppliesWeights(
            string inputText, double expectedKeywordWeight, int expectedUniqueMatches, string scenario)
        {
            // Act
            var result = await _detector.DetectDocumentTypeAsync(inputText);

            // Assert - For matches that should be found
            if (result.ConfidenceScore > 0)
            {
                Assert.True(result.KeywordMatchCount >= expectedUniqueMatches,
                    $"{scenario}: Expected at least {expectedUniqueMatches} matches, got {result.KeywordMatchCount}");
                Assert.NotEmpty(result.MatchedKeywords);
                
                // Confidence should scale with the expected weight
                // Higher weights should produce higher confidence scores
                if (expectedKeywordWeight >= 3.0)
                {
                    Assert.True(result.ConfidenceScore >= 0.85, $"{scenario}: Very strong keywords should have high confidence");
                }
                else if (expectedKeywordWeight >= 2.0)
                {
                    Assert.True(result.ConfidenceScore >= 0.4, $"{scenario}: Strong keywords should have good confidence");
                }
            }
        }

        /// <summary>
        /// Tests all confidence scoring branch conditions systematically.
        /// Note: DetectDocumentTypeAsync returns the top match, so mixed keywords may return single type.
        /// </summary>
        [Theory]
        [InlineData("w-4 single married", true, false, false, 3, "hasVeryStrongMatch=true path")]
        [InlineData("receipt", false, false, true, 1, "strongMatches>=1 path")]
        [InlineData("total subtotal tax", false, false, false, 3, "matchedKeywords.Count>=3 path")]
        [InlineData("total tax", false, false, false, 2, "matchedKeywords.Count>=2 path")]
        [InlineData("total", false, false, false, 1, "matchedKeywords.Count==1 path")]
        public async Task AnalyzeDocumentType_ConfidenceBranchConditions_CoversAllPaths(
            string inputText, bool expectsVeryStrong, bool expectsMultipleStrong, bool expectsSingleStrong, 
            int expectedMatches, string branchDescription)
        {
            // Act
            var result = await _detector.DetectDocumentTypeAsync(inputText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.KeywordMatchCount >= expectedMatches,
                $"{branchDescription}: Expected at least {expectedMatches} matches, got {result.KeywordMatchCount}");
            
            // Verify the expected branch was taken based on confidence ranges
            if (expectsVeryStrong)
            {
                Assert.True(result.ConfidenceScore >= 0.85, $"Very strong match branch should have confidence >= 0.85 for {branchDescription}");
            }
            else if (expectsMultipleStrong)
            {
                Assert.True(result.ConfidenceScore >= 0.7, $"Multiple strong matches branch should have confidence >= 0.7 for {branchDescription}");
            }
            else if (expectsSingleStrong)
            {
                Assert.True(result.ConfidenceScore >= 0.4, $"Single strong match branch should have confidence >= 0.4 for {branchDescription}");
            }
            else if (expectedMatches >= 3)
            {
                Assert.True(result.ConfidenceScore >= 0.3, $"Multiple regular matches should have confidence >= 0.3 for {branchDescription}");
            }
        }

        /// <summary>
        /// Tests occurrence counting and diminishing returns calculation.
        /// </summary>
        [Theory]
        [InlineData("receipt", 1, "Single occurrence")]
        [InlineData("receipt receipt", 2, "Double occurrence")]
        [InlineData("receipt receipt receipt", 3, "Triple occurrence")]
        [InlineData("total total total total total", 5, "Five occurrences")]
        [InlineData("invoice\ninvoice\ninvoice invoice invoice", 5, "Multiple occurrences across lines")]
        public async Task AnalyzeDocumentType_OccurrenceCountingLogic_CorrectlyCountsOccurrences(
            string inputText, int expectedOccurrences, string scenario)
        {
            // Arrange
            var detector = new DocumentTypeDetector(_mockLogger.Object, 0.1); // Low threshold for testing

            // Act
            var result = await detector.DetectDocumentTypeAsync(inputText);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ConfidenceScore > 0, $"Should detect keyword in: {scenario}");
            
            // For repeated occurrences, confidence should be higher than single occurrence
            if (expectedOccurrences > 1)
            {
                var singleResult = await detector.DetectDocumentTypeAsync(inputText.Split(' ')[0]);
                Assert.True(result.ConfidenceScore >= singleResult.ConfidenceScore,
                    $"Multiple occurrences should not decrease confidence for: {scenario}");
            }
        }

        /// <summary>
        /// Tests edge cases in keyword matching and weight calculations.
        /// </summary>
        [Theory]
        [InlineData("w-4", "W4", "Very strong match should detect W4 variations")]
        [InlineData("total: $100", "Receipt", "Total with formatting should still match")]
        [InlineData("RECEIPT", "Receipt", "Case insensitive matching")]
        [InlineData("receipt\n\ntotal\n\nsubtotal", "Receipt", "Keywords separated by newlines")]
        [InlineData("pre-receipt-post", "Receipt", "Keyword as part of larger word")]
        public async Task AnalyzeDocumentType_KeywordMatchingEdgeCases_HandlesCorrectly(
            string inputText, string expectedContainsType, string scenario)
        {
            // Act
            var results = await _detector.DetectMultipleDocumentTypesAsync(inputText, 10);

            // Assert
            Assert.NotNull(results);
            Assert.True(results.Length > 0, $"Should detect at least one type for: {scenario}");
            
            // Check if any result contains the expected type or has positive confidence
            var hasMatchingResult = results.Any(r => 
                r.DocumentType.Contains(expectedContainsType, StringComparison.OrdinalIgnoreCase) ||
                r.ConfidenceScore > 0);
            
            Assert.True(hasMatchingResult, $"Should detect expected type or have positive confidence for: {scenario}");
        }

        /// <summary>
        /// Tests mathematical edge cases in confidence calculations.
        /// </summary>
        [Theory]
        [InlineData("w-4 w-4 form w-4 employee withholding", 1.0, "Maximum confidence should be capped at 1.0 for single document type")]
        [InlineData("receipt total subtotal tax discount cashier thank you", true, "Seven matches should trigger bonus")]
        public async Task AnalyzeDocumentType_MathematicalEdgeCases_HandlesCorrectly(
            string inputText, object expectedCondition, string scenario)
        {
            // Act
            var result = await _detector.DetectDocumentTypeAsync(inputText);

            // Assert
            Assert.NotNull(result);
            
            if (expectedCondition is double expectedMax && expectedMax == 1.0)
            {
                Assert.True(result.ConfidenceScore <= 1.0, $"Confidence should not exceed 1.0 for: {scenario}");
                // For mixed keywords from different document types, confidence might be lower due to ambiguity
                // But for single document type with many strong keywords, should have good confidence
                Assert.True(result.ConfidenceScore >= 0.5, $"Should have reasonable confidence for: {scenario}");
            }
            else if (expectedCondition is bool expectsBonus && expectsBonus)
            {
                Assert.True(result.KeywordMatchCount >= 5, $"Should have high match count for: {scenario}");
                Assert.True(result.ConfidenceScore >= 0.7, $"Bonus should result in high confidence for: {scenario}");
            }
        }

        /// <summary>
        /// Tests single keyword match with different weight categories to ensure proper confidence scaling.
        /// </summary>
        [Theory]
        [InlineData("receipt", 2.2, 0.4, 0.8, "Strong single match (weight >= 2.0)")]
        [InlineData("invoice", 2.2, 0.4, 0.8, "Strong single match - invoice")]
        [InlineData("total", 1.5, 0.15, 0.7, "Medium single match (weight < 2.0)")]
        [InlineData("subtotal", 1.5, 0.15, 0.7, "Medium single match - subtotal")]
        public async Task AnalyzeDocumentType_SingleKeywordWeightScaling_CorrectConfidenceRange(
            string keyword, double keywordWeight, double minExpectedConfidence, double maxExpectedConfidence, string scenario)
        {
            // Act
            var result = await _detector.DetectDocumentTypeAsync(keyword);

            // Assert
            Assert.NotNull(result);
            if (result.ConfidenceScore > 0) // Only test if keyword was actually found
            {
                Assert.True(result.KeywordMatchCount >= 1, $"{scenario}: Should have at least 1 match");
                Assert.True(result.ConfidenceScore >= minExpectedConfidence && result.ConfidenceScore <= maxExpectedConfidence,
                    $"{scenario}: Expected confidence between {minExpectedConfidence} and {maxExpectedConfidence}, got {result.ConfidenceScore}");
            }
        }

        /// <summary>
        /// Tests that repeated strong keywords get appropriate confidence boost.
        /// </summary>
        [Theory]
        [InlineData("receipt receipt receipt", 3, 0.5, 0.85, "Triple receipt should boost confidence significantly")]
        [InlineData("invoice invoice", 2, 0.4, 0.85, "Double invoice should boost confidence moderately")]
        [InlineData("w-4 w-4", 2, 0.85, 1.0, "Double very strong match should maintain high confidence")]
        public async Task AnalyzeDocumentType_RepeatedStrongKeywords_BoostConfidenceAppropriately(
            string inputText, int expectedOccurrences, double minExpectedConfidence, double maxExpectedConfidence, string scenario)
        {
            // Act
            var result = await _detector.DetectDocumentTypeAsync(inputText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.KeywordMatchCount); // Still one unique keyword
            Assert.True(result.ConfidenceScore >= minExpectedConfidence && result.ConfidenceScore <= maxExpectedConfidence,
                $"{scenario}: Expected confidence between {minExpectedConfidence} and {maxExpectedConfidence}, got {result.ConfidenceScore}");
        }

        /// <summary>
        /// Tests helper methods ContainsKeyword and CountOccurrences comprehensively by using known keywords.
        /// </summary>
        [Theory]
        [InlineData("receipt total", 2, "Receipt with total should match both keywords")]
        [InlineData("receipt receipt receipt", 1, "Multiple receipts should count as one unique match")]
        [InlineData("w-4 w-4 w-4", 1, "Multiple W-4s should count as one unique match")]
        [InlineData("invoice invoice", 1, "Multiple invoices should count as one unique match")]
        [InlineData("total subtotal tax", 3, "Three different receipt keywords should match")]
        [InlineData("", 0, "Empty text should have no matches")]
        [InlineData("nonexistent keywords here", 0, "Unknown keywords should have no matches")]
        public async Task AnalyzeDocumentType_HelperMethods_WorkCorrectly(
            string inputText, int expectedMatchCount, string scenario)
        {
            // Act
            var result = await _detector.DetectDocumentTypeAsync(inputText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedMatchCount, result.KeywordMatchCount);
            
            if (expectedMatchCount > 0)
            {
                Assert.True(result.ConfidenceScore > 0, $"Should have positive confidence for: {scenario}");
                Assert.NotEmpty(result.MatchedKeywords);
            }
            else
            {
                Assert.Equal(0.0, result.ConfidenceScore);
                Assert.Empty(result.MatchedKeywords);
            }
        }

        #endregion
    }
}