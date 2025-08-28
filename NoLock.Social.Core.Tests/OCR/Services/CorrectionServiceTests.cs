using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using NoLock.Social.Core.OCR.Services;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Interfaces;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Comprehensive unit tests for CorrectionService with extensive coverage.
    /// Tests field correction, validation integration, and confidence score calculation.
    /// </summary>
    public class CorrectionServiceTests : IDisposable
    {
        private readonly Mock<FieldValidationService> _mockValidationService;
        private readonly CorrectionService _correctionService;
        private readonly ProcessedDocument _sampleDocument;

        public CorrectionServiceTests()
        {
            _mockValidationService = new Mock<FieldValidationService>();
            _correctionService = new CorrectionService(_mockValidationService.Object);
            
            _sampleDocument = new ProcessedReceipt
            {
                DocumentId = Guid.NewGuid().ToString(),
                DocumentType = "Receipt",
                ProcessedAt = DateTime.UtcNow,
                ConfidenceScore = 0.85,
                ReceiptData = new ReceiptData
                {
                    StoreName = "Test Store",
                    Total = 99.99m,
                    TransactionDate = DateTime.Now
                }
            };
        }

        #region CreateCorrectableDocumentAsync Tests

        [Fact]
        public async Task CreateCorrectableDocumentAsync_ValidDocument_CreatesWrapper()
        {
            // Act
            var correctedDoc = await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);

            // Assert
            correctedDoc.Should().NotBeNull();
            correctedDoc.OriginalDocument.Should().BeSameAs(_sampleDocument);
            correctedDoc.HasUnsavedChanges.Should().BeFalse();
            correctedDoc.FieldCorrections.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateCorrectableDocumentAsync_NullDocument_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _correctionService.CreateCorrectableDocumentAsync(null));
        }

        [Fact]
        public async Task CreateCorrectableDocumentAsync_IdentifiesLowConfidenceFields()
        {
            // Arrange
            var lowConfidenceDoc = new ProcessedReceipt
            {
                DocumentId = Guid.NewGuid().ToString(),
                DocumentType = "Receipt",
                ConfidenceScore = 0.5 // Below default threshold
            };

            // Act
            var correctedDoc = await _correctionService.CreateCorrectableDocumentAsync(lowConfidenceDoc);

            // Assert
            correctedDoc.LowConfidenceFields.Should().NotBeEmpty();
            correctedDoc.LowConfidenceFields.Should().Contain("Total");
            correctedDoc.LowConfidenceFields.Should().Contain("StoreName");
        }

        [Theory]
        [InlineData(0.3, true, "very low confidence")]
        [InlineData(0.6, true, "below threshold")]
        [InlineData(0.7, false, "at threshold")]
        [InlineData(0.9, false, "high confidence")]
        public async Task CreateCorrectableDocumentAsync_ConfidenceThreshold_WorksCorrectly(
            double confidence, bool expectLowConfidenceFields, string scenario)
        {
            // Arrange
            _sampleDocument.ConfidenceScore = confidence;

            // Act
            var correctedDoc = await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);

            // Assert
            if (expectLowConfidenceFields)
            {
                correctedDoc.LowConfidenceFields.Should().NotBeEmpty($"Failed for {scenario}");
            }
            else
            {
                correctedDoc.LowConfidenceFields.Should().BeEmpty($"Failed for {scenario}");
            }
        }

        #endregion

        #region ApplyFieldCorrectionAsync Tests

        [Fact]
        public async Task ApplyFieldCorrectionAsync_ValidCorrection_AppliesSuccessfully()
        {
            // Arrange
            await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(FieldValidationResult.Success());

            // Act
            var result = await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId,
                "Total",
                "150.00",
                "100.00",
                0.75);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData(null, "fieldName", "null document ID")]
        [InlineData("", "fieldName", "empty document ID")]
        [InlineData("docId", null, "null field name")]
        [InlineData("docId", "", "empty field name")]
        public async Task ApplyFieldCorrectionAsync_InvalidParameters_ThrowsArgumentException(
            string documentId, string fieldName, string scenario)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _correctionService.ApplyFieldCorrectionAsync(
                    documentId, fieldName, "newValue", "oldValue", 0.8));
        }

        [Fact]
        public async Task ApplyFieldCorrectionAsync_NonExistentSession_ThrowsInvalidOperationException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _correctionService.ApplyFieldCorrectionAsync(
                    "nonexistent", "field", "value", "oldValue", 0.8));
        }

        [Fact]
        public async Task ApplyFieldCorrectionAsync_InvalidValue_ReturnsValidationErrors()
        {
            // Arrange
            await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            var validationResult = new FieldValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "Invalid email format" }
            };
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(validationResult);

            // Act
            var result = await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId,
                "Email",
                "invalid-email",
                "old@email.com",
                0.9);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain("Invalid email format");
        }

        [Fact]
        public async Task ApplyFieldCorrectionAsync_UpdatesHasUnsavedChanges()
        {
            // Arrange
            var correctedDoc = await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(FieldValidationResult.Success());

            // Act
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId,
                "Total",
                "200.00",
                "100.00",
                0.75);

            // Assert
            correctedDoc.HasUnsavedChanges.Should().BeTrue();
            correctedDoc.FieldCorrections.Should().ContainKey("Total");
        }

        [Fact]
        public async Task ApplyFieldCorrectionAsync_MultipleCorrectionsSameField_OverwritesPrevious()
        {
            // Arrange
            await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(FieldValidationResult.Success());

            // Act
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Total", "150.00", "100.00", 0.75);
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Total", "200.00", "100.00", 0.75);

            // Assert
            var corrections = await _correctionService.GetCorrectionsAsync(_sampleDocument.DocumentId);
            corrections.Should().HaveCount(1);
            corrections.First().CorrectedValue.Should().Be("200.00");
        }

        #endregion

        #region CalculateUpdatedConfidence Tests

        [Theory]
        [InlineData(0.5, true, true, 0.9, "low confidence, changed to valid")]
        [InlineData(0.7, true, true, 0.9, "medium confidence, changed to valid")]
        [InlineData(0.8, true, false, 0.4, "high confidence, changed to invalid")]
        [InlineData(0.6, false, true, 0.8, "user confirmed, valid")]
        [InlineData(0.9, false, true, 1.0, "user confirmed high confidence")]
        public void CalculateUpdatedConfidence_VariousScenarios_CalculatesCorrectly(
            double originalConfidence, bool wasChanged, bool isValid, 
            double expectedMinConfidence, string scenario)
        {
            // Arrange
            var validationResult = new FieldValidationResult { IsValid = isValid };

            // Act
            var updatedConfidence = _correctionService.CalculateUpdatedConfidence(
                originalConfidence, wasChanged, validationResult);

            // Assert
            updatedConfidence.Should().BeGreaterThanOrEqualTo(expectedMinConfidence, 
                $"Failed for {scenario}");
            updatedConfidence.Should().BeInRange(0.0, 1.0);
        }

        [Fact]
        public void CalculateUpdatedConfidence_WithWarnings_ReducesConfidence()
        {
            // Arrange
            var validationResult = new FieldValidationResult
            {
                IsValid = true,
                Warnings = new List<string> { "Value seems unusual" }
            };

            // Act
            var updatedConfidence = _correctionService.CalculateUpdatedConfidence(
                0.8, false, validationResult);

            // Assert
            updatedConfidence.Should().BeLessThan(1.0);
        }

        [Theory]
        [InlineData(-0.1, "negative confidence")]
        [InlineData(1.5, "confidence above 1")]
        public void CalculateUpdatedConfidence_BoundsConfidenceCorrectly(
            double originalConfidence, string scenario)
        {
            // Arrange
            var validationResult = FieldValidationResult.Success();

            // Act
            var updatedConfidence = _correctionService.CalculateUpdatedConfidence(
                originalConfidence, false, validationResult);

            // Assert
            updatedConfidence.Should().BeInRange(0.0, 1.0, $"Failed for {scenario}");
        }

        #endregion

        #region SaveCorrectionsAsync Tests

        [Fact]
        public async Task SaveCorrectionsAsync_ValidCorrections_SavesSuccessfully()
        {
            // Arrange
            await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(FieldValidationResult.Success());

            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Total", "150.00", "100.00", 0.75);

            // Act
            var updatedDocument = await _correctionService.SaveCorrectionsAsync(_sampleDocument.DocumentId);

            // Assert
            updatedDocument.Should().NotBeNull();
            var corrections = await _correctionService.GetCorrectionsAsync(_sampleDocument.DocumentId);
            corrections.All(c => c.IsSaved).Should().BeTrue();
        }

        [Fact]
        public async Task SaveCorrectionsAsync_NoSession_ThrowsInvalidOperationException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _correctionService.SaveCorrectionsAsync("nonexistent"));
        }

        [Fact]
        public async Task SaveCorrectionsAsync_WithValidationErrors_ThrowsInvalidOperationException()
        {
            // Arrange
            var correctedDoc = await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            // Add an invalid correction directly
            correctedDoc.FieldCorrections["Email"] = new FieldCorrection
            {
                FieldName = "Email",
                IsValid = false,
                ValidationErrors = new List<string> { "Invalid email" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _correctionService.SaveCorrectionsAsync(_sampleDocument.DocumentId));
        }

        [Fact]
        public async Task SaveCorrectionsAsync_ClearsPendingCorrections()
        {
            // Arrange
            await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(FieldValidationResult.Success());

            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Total", "150.00", "100.00", 0.75);

            // Act
            await _correctionService.SaveCorrectionsAsync(_sampleDocument.DocumentId);
            
            // Apply a new correction
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "StoreName", "New Store", "Old Store", 0.8);

            // Assert
            var corrections = await _correctionService.GetCorrectionsAsync(_sampleDocument.DocumentId);
            corrections.Should().HaveCount(2);
        }

        [Fact]
        public async Task SaveCorrectionsAsync_UpdatesDocumentConfidence()
        {
            // Arrange
            var correctedDoc = await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(FieldValidationResult.Success());

            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Total", "150.00", "100.00", 0.95);

            // Act
            var updatedDocument = await _correctionService.SaveCorrectionsAsync(_sampleDocument.DocumentId);

            // Assert
            updatedDocument.ConfidenceScore.Should().BeGreaterThan(0);
        }

        #endregion

        #region CancelCorrectionsAsync Tests

        [Fact]
        public async Task CancelCorrectionsAsync_RemovesUnsavedCorrections()
        {
            // Arrange
            var correctedDoc = await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(FieldValidationResult.Success());

            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Total", "150.00", "100.00", 0.75);

            // Act
            await _correctionService.CancelCorrectionsAsync(_sampleDocument.DocumentId);

            // Assert
            correctedDoc.FieldCorrections.Should().BeEmpty();
            correctedDoc.HasUnsavedChanges.Should().BeFalse();
        }

        [Fact]
        public async Task CancelCorrectionsAsync_KeepsSavedCorrections()
        {
            // Arrange
            await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(FieldValidationResult.Success());

            // Apply and save first correction
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Total", "150.00", "100.00", 0.75);
            await _correctionService.SaveCorrectionsAsync(_sampleDocument.DocumentId);
            
            // Apply new unsaved correction
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "StoreName", "New Store", "Old Store", 0.8);

            // Act
            await _correctionService.CancelCorrectionsAsync(_sampleDocument.DocumentId);

            // Assert
            var corrections = await _correctionService.GetCorrectionsAsync(_sampleDocument.DocumentId);
            corrections.Should().HaveCount(1);
            corrections.First().FieldName.Should().Be("Total");
        }

        [Theory]
        [InlineData(null, "null document ID")]
        [InlineData("", "empty document ID")]
        [InlineData("   ", "whitespace document ID")]
        public async Task CancelCorrectionsAsync_InvalidDocumentId_ThrowsArgumentException(
            string documentId, string scenario)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _correctionService.CancelCorrectionsAsync(documentId));
        }

        #endregion

        #region RevertFieldCorrectionAsync Tests

        [Fact]
        public async Task RevertFieldCorrectionAsync_RemovesSpecificCorrection()
        {
            // Arrange
            await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(FieldValidationResult.Success());

            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Total", "150.00", "100.00", 0.75);
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "StoreName", "New Store", "Old Store", 0.8);

            // Act
            await _correctionService.RevertFieldCorrectionAsync(_sampleDocument.DocumentId, "Total");

            // Assert
            var corrections = await _correctionService.GetCorrectionsAsync(_sampleDocument.DocumentId);
            corrections.Should().HaveCount(1);
            corrections.First().FieldName.Should().Be("StoreName");
        }

        [Fact]
        public async Task RevertFieldCorrectionAsync_NonExistentField_DoesNotThrow()
        {
            // Arrange
            await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);

            // Act & Assert - Should not throw
            await _correctionService.RevertFieldCorrectionAsync(_sampleDocument.DocumentId, "NonExistent");
        }

        [Theory]
        [InlineData("docId", null, "null field name")]
        [InlineData("docId", "", "empty field name")]
        [InlineData(null, "field", "null document ID")]
        public async Task RevertFieldCorrectionAsync_InvalidParameters_ThrowsArgumentException(
            string documentId, string fieldName, string scenario)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _correctionService.RevertFieldCorrectionAsync(documentId, fieldName));
        }

        #endregion

        #region GetLowConfidenceFieldsAsync Tests

        [Theory]
        [InlineData("Receipt", 6, "receipt fields")]
        [InlineData("Check", 6, "check fields")]
        [InlineData("W4", 5, "W4 fields")]
        [InlineData("Unknown", 0, "unknown document type")]
        public async Task GetLowConfidenceFieldsAsync_ReturnsCorrectFieldsForDocumentType(
            string documentType, int expectedFieldCount, string scenario)
        {
            // Arrange
            ProcessedDocument document = documentType == "Receipt" ? 
                new ProcessedReceipt
                {
                    DocumentId = Guid.NewGuid().ToString(),
                    DocumentType = documentType,
                    ConfidenceScore = 0.5 // Below threshold
                } : 
                (ProcessedDocument)new ProcessedCheck
                {
                    DocumentId = Guid.NewGuid().ToString(),
                    DocumentType = documentType,
                    ConfidenceScore = 0.5 // Below threshold
                };

            // Act
            var lowConfidenceFields = await _correctionService.GetLowConfidenceFieldsAsync(document);

            // Assert
            lowConfidenceFields.Should().HaveCount(expectedFieldCount, $"Failed for {scenario}");
        }

        [Fact]
        public async Task GetLowConfidenceFieldsAsync_NullDocument_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _correctionService.GetLowConfidenceFieldsAsync(null));
        }

        [Theory]
        [InlineData(0.5, 0.7, true, "confidence below threshold")]
        [InlineData(0.8, 0.7, false, "confidence above threshold")]
        [InlineData(0.7, 0.7, false, "confidence at threshold")]
        public async Task GetLowConfidenceFieldsAsync_RespectsThreshold(
            double documentConfidence, double threshold, bool expectFields, string scenario)
        {
            // Arrange
            _sampleDocument.ConfidenceScore = documentConfidence;

            // Act
            var fields = await _correctionService.GetLowConfidenceFieldsAsync(_sampleDocument, threshold);

            // Assert
            if (expectFields)
            {
                fields.Should().NotBeEmpty($"Failed for {scenario}");
            }
            else
            {
                fields.Should().BeEmpty($"Failed for {scenario}");
            }
        }

        #endregion

        #region GetFieldValidationRulesAsync Tests

        [Fact]
        public async Task GetFieldValidationRulesAsync_DelegatesToValidationService()
        {
            // Arrange
            var expectedRules = new FieldValidationRules
            {
                FieldName = "Email",
                FieldType = FieldType.Email
            };
            
            _mockValidationService.Setup(x => x.GetFieldValidationRulesAsync("Email", "Receipt"))
                .ReturnsAsync(expectedRules);

            // Act
            var rules = await _correctionService.GetFieldValidationRulesAsync("Email", "Receipt");

            // Assert
            rules.Should().BeSameAs(expectedRules);
            _mockValidationService.Verify(x => x.GetFieldValidationRulesAsync("Email", "Receipt"), Times.Once);
        }

        #endregion

        #region ValidateFieldAsync Tests

        [Fact]
        public async Task ValidateFieldAsync_DelegatesToValidationService()
        {
            // Arrange
            var expectedResult = FieldValidationResult.Success();
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                "Email", "test@example.com", "Receipt", null))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _correctionService.ValidateFieldAsync("Email", "test@example.com", "Receipt");

            // Assert
            result.Should().BeSameAs(expectedResult);
            _mockValidationService.Verify(x => x.ValidateFieldAsync(
                "Email", "test@example.com", "Receipt", null), Times.Once);
        }

        #endregion

        #region GetCorrectionsAsync Tests

        [Fact]
        public async Task GetCorrectionsAsync_ReturnsAllCorrections()
        {
            // Arrange
            await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(FieldValidationResult.Success());

            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Total", "150.00", "100.00", 0.75);
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "StoreName", "New Store", "Old Store", 0.8);

            // Act
            var corrections = await _correctionService.GetCorrectionsAsync(_sampleDocument.DocumentId);

            // Assert
            corrections.Should().HaveCount(2);
            corrections.Select(c => c.FieldName).Should().Contain(new[] { "Total", "StoreName" });
        }

        [Fact]
        public async Task GetCorrectionsAsync_NonExistentDocument_ReturnsEmpty()
        {
            // Act
            var corrections = await _correctionService.GetCorrectionsAsync("nonexistent");

            // Assert
            corrections.Should().BeEmpty();
        }

        [Theory]
        [InlineData(null, "null document ID")]
        [InlineData("", "empty document ID")]
        [InlineData("   ", "whitespace document ID")]
        public async Task GetCorrectionsAsync_InvalidDocumentId_ThrowsArgumentException(
            string documentId, string scenario)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _correctionService.GetCorrectionsAsync(documentId));
        }

        #endregion

        #region Complex Workflow Tests

        [Fact]
        public async Task CompleteWorkflow_CreateApplySaveLoad_WorksCorrectly()
        {
            // Create correctable document
            var correctedDoc = await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(FieldValidationResult.Success());

            // Apply multiple corrections
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Total", "250.00", "100.00", 0.75);
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "StoreName", "Updated Store", "Test Store", 0.8);
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Email", "new@email.com", "old@email.com", 0.6);

            // Save corrections
            var savedDocument = await _correctionService.SaveCorrectionsAsync(_sampleDocument.DocumentId);
            
            // Load corrections
            var corrections = await _correctionService.GetCorrectionsAsync(_sampleDocument.DocumentId);

            // Assert
            corrections.Should().HaveCount(3);
            corrections.All(c => c.IsSaved).Should().BeTrue();
            correctedDoc.HasUnsavedChanges.Should().BeFalse();
            savedDocument.Should().NotBeNull();
        }

        [Fact]
        public async Task CompleteWorkflow_WithPartialRevert_HandlesCorrectly()
        {
            // Setup
            await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);
            
            _mockValidationService.Setup(x => x.ValidateFieldAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<FieldValidationRules>()))
                .ReturnsAsync(FieldValidationResult.Success());

            // Apply corrections
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Total", "150.00", "100.00", 0.75);
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "StoreName", "New Store", "Old Store", 0.8);
            
            // Save first batch
            await _correctionService.SaveCorrectionsAsync(_sampleDocument.DocumentId);
            
            // Apply more corrections
            await _correctionService.ApplyFieldCorrectionAsync(
                _sampleDocument.DocumentId, "Email", "test@example.com", "", 0.5);
            
            // Revert one saved correction
            await _correctionService.RevertFieldCorrectionAsync(_sampleDocument.DocumentId, "Total");
            
            // Cancel unsaved changes
            await _correctionService.CancelCorrectionsAsync(_sampleDocument.DocumentId);
            
            // Check final state
            var corrections = await _correctionService.GetCorrectionsAsync(_sampleDocument.DocumentId);
            
            // Assert
            corrections.Should().HaveCount(1);
            corrections.First().FieldName.Should().Be("StoreName");
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public async Task Dispose_ClearsAllSessions()
        {
            // Arrange
            await _correctionService.CreateCorrectableDocumentAsync(_sampleDocument);

            // Act
            _correctionService.Dispose();

            // Assert - Session should be cleared, so this should throw
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _correctionService.ApplyFieldCorrectionAsync(
                    _sampleDocument.DocumentId, "field", "value", "old", 0.5));
        }

        #endregion

        public void Dispose()
        {
            _correctionService?.Dispose();
        }
    }
}