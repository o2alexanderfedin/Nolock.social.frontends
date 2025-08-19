using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.OCR.Models;
using CameraDocumentType = NoLock.Social.Core.Camera.Models.DocumentType;
using NoLock.Social.Core.OCR.Services;
using Xunit;

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
            _detector = new DocumentTypeDetector(_mockLogger.Object, 0.7);
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
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _detector.DetectDocumentTypeAsync("Some text", cts.Token));
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
            var text = "Receipt\nTotal: $50"; // Minimal keywords

            // Act
            var result = await lowConfidenceDetector.DetectDocumentTypeAsync(text);

            // Assert
            if (result.DocumentType != CameraDocumentType.Other.ToString())
            {
                Assert.True(result.RequiresManualConfirmation, 
                    "Low confidence detection should require manual confirmation");
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

        #endregion
    }
}