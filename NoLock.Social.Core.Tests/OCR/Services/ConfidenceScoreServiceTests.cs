using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Unit tests for the ConfidenceScoreService.
    /// </summary>
    public class ConfidenceScoreServiceTests
    {
        private readonly IConfidenceScoreService _service;

        public ConfidenceScoreServiceTests()
        {
            // Use a seeded Random for deterministic test behavior
            var seededRandom = new Random(42);
            _service = new ConfidenceScoreService(seededRandom);
        }

        #region ConfidenceLevel Tests

        [Theory]
        [InlineData(0.95, ConfidenceLevel.High, "High confidence - score above 0.80")]
        [InlineData(0.80, ConfidenceLevel.High, "High confidence - score at threshold")]
        [InlineData(0.70, ConfidenceLevel.Medium, "Medium confidence - score between 0.60 and 0.79")]
        [InlineData(0.60, ConfidenceLevel.Medium, "Medium confidence - score at threshold")]
        [InlineData(0.50, ConfidenceLevel.Low, "Low confidence - score below 0.60")]
        [InlineData(0.10, ConfidenceLevel.Low, "Low confidence - very low score")]
        public void GetConfidenceLevel_ReturnsCorrectLevel(double score, ConfidenceLevel expected, string scenario)
        {
            // Act
            var level = ConfidenceHelper.GetConfidenceLevel(score);

            // Assert
            Assert.Equal(expected, level);
        }

        [Theory]
        [InlineData(0.95, "confidence-high", "High confidence CSS class")]
        [InlineData(0.70, "confidence-medium", "Medium confidence CSS class")]
        [InlineData(0.40, "confidence-low", "Low confidence CSS class")]
        public void GetCssClass_ReturnsCorrectClass(double score, string expectedClass, string scenario)
        {
            // Act
            var cssClass = ConfidenceHelper.GetCssClass(score);

            // Assert
            Assert.Equal(expectedClass, cssClass);
        }

        [Theory]
        [InlineData(0.95, "#10b981", "High confidence color")]
        [InlineData(0.70, "#f59e0b", "Medium confidence color")]
        [InlineData(0.40, "#ef4444", "Low confidence color")]
        public void GetColorCode_ReturnsCorrectColor(double score, string expectedColor, string scenario)
        {
            // Act
            var color = ConfidenceHelper.GetColorCode(score);

            // Assert
            Assert.Equal(expectedColor, color);
        }

        [Theory]
        [InlineData(0.956, "96%", "Rounds to nearest percentage")]
        [InlineData(0.50, "50%", "Exact percentage")]
        [InlineData(0.333, "33%", "Rounds down")]
        [InlineData(1.0, "100%", "Maximum percentage")]
        [InlineData(0.0, "0%", "Minimum percentage")]
        public void FormatAsPercentage_FormatsCorrectly(double score, string expected, string scenario)
        {
            // Act
            var formatted = ConfidenceHelper.FormatAsPercentage(score);

            // Assert
            Assert.Equal(expected, formatted);
        }

        #endregion

        #region Calculation Tests

        [Theory]
        [InlineData(new double[] { 0.8, 0.9, 0.7 }, 0.8, "Average of three scores")]
        [InlineData(new double[] { 1.0, 0.0 }, 0.5, "Average of extremes")]
        [InlineData(new double[] { 0.5 }, 0.5, "Single score")]
        [InlineData(new double[] { }, 0.0, "Empty array")]
        [InlineData(null, 0.0, "Null array")]
        public void CalculateAverageConfidence_CalculatesCorrectly(double[] scores, double expected, string scenario)
        {
            // Act
            var average = ConfidenceHelper.CalculateAverageConfidence(scores);

            // Assert
            Assert.Equal(expected, average, 2);
        }

        [Fact]
        public void CalculateWeightedConfidence_CalculatesCorrectly()
        {
            // Arrange
            var scoresAndWeights = new[]
            {
                (score: 0.9, weight: 2.0),  // Heavy weight on high score
                (score: 0.6, weight: 1.0),  // Normal weight on medium score
                (score: 0.3, weight: 0.5)   // Light weight on low score
            };

            // Act
            var weighted = ConfidenceHelper.CalculateWeightedConfidence(scoresAndWeights);

            // Assert
            var expected = (0.9 * 2.0 + 0.6 * 1.0 + 0.3 * 0.5) / (2.0 + 1.0 + 0.5);
            Assert.Equal(expected, weighted, 2);
        }

        #endregion

        #region Service Tests

        [Fact]
        public void ThresholdProperties_ClampValues()
        {
            // Arrange & Act
            _service.HighConfidenceThreshold = 1.5;  // Should clamp to 1.0
            _service.MediumConfidenceThreshold = -0.5;  // Should clamp to 0.0

            // Assert
            Assert.Equal(1.0, _service.HighConfidenceThreshold);
            Assert.Equal(0.0, _service.MediumConfidenceThreshold);
        }

        [Fact]
        public void CalculateOverallConfidence_WithReceipt_CalculatesCorrectly()
        {
            // Arrange
            var receipt = new ProcessedReceipt
            {
                ConfidenceScore = 0.85,
                ReceiptData = new ReceiptData
                {
                    StoreName = "Test Store",
                    Total = 100m,
                    Subtotal = 90m,
                    TaxAmount = 10m,
                    TransactionDate = DateTime.Now
                }
            };

            // Act
            var confidence = _service.CalculateOverallConfidence(receipt);

            // Assert
            Assert.InRange(confidence, 0.7, 1.0); // Should be reasonable range
        }

        [Fact]
        public void ExtractFieldConfidences_WithReceipt_ExtractsFields()
        {
            // Arrange
            var receipt = new ProcessedReceipt
            {
                ConfidenceScore = 0.85,
                ReceiptData = new ReceiptData
                {
                    StoreName = "Test Store",
                    Total = 100m,
                    Subtotal = 90m,
                    TaxAmount = 10m,
                    TransactionDate = DateTime.Now,
                    ReceiptNumber = "12345",
                    PaymentMethod = "Credit Card"
                }
            };

            // Act
            var fieldConfidences = _service.ExtractFieldConfidences(receipt);

            // Assert
            Assert.Contains("StoreName", fieldConfidences.Keys);
            Assert.Contains("Total", fieldConfidences.Keys);
            Assert.Contains("Subtotal", fieldConfidences.Keys);
            Assert.Contains("TaxAmount", fieldConfidences.Keys);
            Assert.Contains("TransactionDate", fieldConfidences.Keys);
            Assert.Contains("ReceiptNumber", fieldConfidences.Keys);
            Assert.Contains("PaymentMethod", fieldConfidences.Keys);
            Assert.True(fieldConfidences.All(f => f.Value >= 0 && f.Value <= 1));
        }

        [Fact]
        public void ExtractFieldConfidences_WithCheck_ExtractsFields()
        {
            // Arrange
            var check = new ProcessedCheck
            {
                ConfidenceScore = 0.90,
                CheckData = new CheckData
                {
                    AmountNumeric = 500m,
                    Payee = "John Doe",
                    CheckNumber = "1001",
                    RoutingNumber = "123456789",
                    AccountNumber = "987654321",
                    Date = DateTime.Now,
                    IsRoutingNumberValid = true
                }
            };

            // Act
            var fieldConfidences = _service.ExtractFieldConfidences(check);

            // Assert
            Assert.Contains("Amount", fieldConfidences.Keys);
            Assert.Contains("Payee", fieldConfidences.Keys);
            Assert.Contains("CheckNumber", fieldConfidences.Keys);
            Assert.Contains("RoutingNumber", fieldConfidences.Keys);
            Assert.Contains("AccountNumber", fieldConfidences.Keys);
            Assert.Contains("Date", fieldConfidences.Keys);
        }

        [Fact]
        public void MeetsMinimumConfidence_EvaluatesCorrectly()
        {
            // Arrange
            var document = new ProcessedReceipt
            {
                ConfidenceScore = 0.75,
                ReceiptData = new ReceiptData { Total = 100m }
            };

            // Act & Assert
            Assert.True(_service.MeetsMinimumConfidence(document, 0.70));
            Assert.False(_service.MeetsMinimumConfidence(document, 0.80));
            Assert.False(_service.MeetsMinimumConfidence(null, 0.50));
        }

        [Fact]
        public void GetConfidenceStatistics_CalculatesCorrectly()
        {
            // Arrange
            var receipt = new ProcessedReceipt
            {
                ConfidenceScore = 0.85,
                ReceiptData = new ReceiptData
                {
                    StoreName = "Test Store",
                    Total = 100m,
                    Subtotal = 90m,
                    TaxAmount = 10m
                }
            };

            // Act
            var stats = _service.GetConfidenceStatistics(receipt);

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(0.85, stats.DocumentConfidence);
            Assert.True(stats.TotalFields > 0);
            Assert.InRange(stats.AverageFieldConfidence, 0, 1);
            Assert.InRange(stats.MinFieldConfidence, 0, 1);
            Assert.InRange(stats.MaxFieldConfidence, 0, 1);
            Assert.True(stats.MinFieldConfidence <= stats.MaxFieldConfidence);
        }

        [Fact]
        public void ValidateConfidence_WithLowConfidence_ReturnsInvalid()
        {
            // Arrange
            var document = new ProcessedReceipt
            {
                ConfidenceScore = 0.30,
                ReceiptData = new ReceiptData()
            };

            // Act
            var result = _service.ValidateConfidence(document);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Contains("below minimum threshold"));
        }

        [Fact]
        public void ValidateConfidence_WithHighConfidence_ReturnsValid()
        {
            // Arrange
            var document = new ProcessedReceipt
            {
                ConfidenceScore = 0.90,
                ReceiptData = new ReceiptData
                {
                    StoreName = "Test Store",
                    Total = 100m
                }
            };

            // Act
            var result = _service.ValidateConfidence(document);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidateConfidence_WithNull_ReturnsInvalid()
        {
            // Act
            var result = _service.ValidateConfidence(null);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains("Document is null", result.Errors);
        }

        #endregion

        #region Statistics Tests

        [Fact]
        public void ConfidenceStatistics_InitializesCorrectly()
        {
            // Arrange & Act
            var stats = new ConfidenceStatistics();

            // Assert
            Assert.Equal(0, stats.OverallConfidence);
            Assert.Equal(0, stats.TotalFields);
            Assert.NotNull(stats.FieldsNeedingReview);
            Assert.Empty(stats.FieldsNeedingReview);
        }

        [Fact]
        public void ValidationResult_InitializesCorrectly()
        {
            // Arrange & Act
            var result = new ValidationResult();

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.Errors);
            Assert.NotNull(result.Warnings);
            Assert.NotNull(result.Recommendations);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
            Assert.Empty(result.Recommendations);
        }

        #endregion
    }
}