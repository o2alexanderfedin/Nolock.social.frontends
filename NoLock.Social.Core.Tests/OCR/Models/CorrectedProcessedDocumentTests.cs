using NoLock.Social.Core.OCR.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Models
{
    /// <summary>
    /// Unit tests for the CorrectedProcessedDocument model focusing on correction tracking.
    /// </summary>
    public class CorrectedProcessedDocumentTests
    {
        [Fact]
        public void Constructor_WithDocument_InitializesCorrectly()
        {
            // Arrange
            var originalDoc = new ProcessedCheck();

            // Act
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);

            // Assert
            Assert.NotNull(correctedDoc.OriginalDocument);
            Assert.Same(originalDoc, correctedDoc.OriginalDocument);
            Assert.NotNull(correctedDoc.CorrectionSessionId);
            Assert.NotEmpty(correctedDoc.CorrectionSessionId);
            Assert.NotNull(correctedDoc.FieldCorrections);
            Assert.Empty(correctedDoc.FieldCorrections);
            Assert.NotNull(correctedDoc.LowConfidenceFields);
            Assert.Empty(correctedDoc.LowConfidenceFields);
            Assert.Equal(0.7, correctedDoc.ConfidenceThreshold);
            Assert.False(correctedDoc.HasUnsavedChanges);
        }

        [Fact]
        public void Constructor_WithNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new CorrectedProcessedDocument(null));
        }

        [Fact]
        public void ParameterlessConstructor_Works()
        {
            // Act
            var correctedDoc = new CorrectedProcessedDocument();

            // Assert
            Assert.Null(correctedDoc.OriginalDocument);
            Assert.NotNull(correctedDoc.FieldCorrections);
            Assert.NotNull(correctedDoc.LowConfidenceFields);
            Assert.NotNull(correctedDoc.Metadata);
        }

        [Fact]
        public void CreatedAt_SetToUtcNow()
        {
            // Arrange
            var before = DateTime.UtcNow;
            var originalDoc = new ProcessedCheck();

            // Act
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);
            var after = DateTime.UtcNow;

            // Assert
            Assert.InRange(correctedDoc.CreatedAt, before.AddSeconds(-1), after.AddSeconds(1));
            Assert.Equal(correctedDoc.CreatedAt, correctedDoc.LastModifiedAt);
        }

        [Theory]
        [InlineData("field1", "originalValue", "correctedValue", 0.95)]
        [InlineData("field2", 100, 150, 1.0)]
        [InlineData("field3", null, "newValue", 0.8)]
        public void GetEffectiveFieldValue_WithCorrections(
            string fieldName, 
            object originalValue, 
            object correctedValue, 
            double confidence)
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);
            var correction = new FieldCorrection
            {
                FieldName = fieldName,
                OriginalValue = originalValue,
                CorrectedValue = correctedValue,
                UpdatedConfidence = confidence
            };
            correctedDoc.FieldCorrections[fieldName] = correction;

            // Act
            var effectiveValue = correctedDoc.GetEffectiveFieldValue(fieldName);

            // Assert
            Assert.Equal(correctedValue, effectiveValue);
        }

        [Fact]
        public void GetEffectiveFieldValue_WithoutCorrection_ReturnsOriginal()
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);

            // Act
            var effectiveValue = correctedDoc.GetEffectiveFieldValue("uncorrectedField");

            // Assert
            Assert.Null(effectiveValue); // GetOriginalFieldValue returns null in base implementation
        }

        [Theory]
        [InlineData("field1", 0.5, 0.95)]
        [InlineData("field2", 0.3, 1.0)]
        [InlineData("field3", 0.7, 0.85)]
        public void GetEffectiveConfidenceScore_WithCorrections(
            string fieldName,
            double originalConfidence,
            double updatedConfidence)
        {
            // Arrange
            var originalDoc = new ProcessedCheck { ConfidenceScore = originalConfidence };
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);
            var correction = new FieldCorrection
            {
                FieldName = fieldName,
                OriginalConfidence = originalConfidence,
                UpdatedConfidence = updatedConfidence
            };
            correctedDoc.FieldCorrections[fieldName] = correction;

            // Act
            var effectiveConfidence = correctedDoc.GetEffectiveConfidenceScore(fieldName);

            // Assert
            Assert.Equal(updatedConfidence, effectiveConfidence);
        }

        [Fact]
        public void GetEffectiveConfidenceScore_WithoutCorrection_ReturnsDocumentConfidence()
        {
            // Arrange
            var originalDoc = new ProcessedCheck { ConfidenceScore = 0.75 };
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);

            // Act
            var effectiveConfidence = correctedDoc.GetEffectiveConfidenceScore("uncorrectedField");

            // Assert
            Assert.Equal(0.75, effectiveConfidence);
        }

        [Theory]
        [InlineData("field1", true)]
        [InlineData("field2", true)]
        [InlineData("uncorrectedField", false)]
        public void IsFieldCorrected_ReturnsCorrectStatus(string fieldName, bool shouldBeCorrected)
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);
            
            if (shouldBeCorrected)
            {
                correctedDoc.FieldCorrections[fieldName] = new FieldCorrection { FieldName = fieldName };
            }

            // Act
            var isCorrected = correctedDoc.IsFieldCorrected(fieldName);

            // Assert
            Assert.Equal(shouldBeCorrected, isCorrected);
        }

        [Theory]
        [InlineData("lowConfField1", true)]
        [InlineData("lowConfField2", true)]
        [InlineData("normalField", false)]
        public void ShouldHighlightField_ReturnsCorrectStatus(string fieldName, bool shouldHighlight)
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);
            
            if (shouldHighlight)
            {
                correctedDoc.LowConfidenceFields.Add(fieldName);
            }

            // Act
            var highlight = correctedDoc.ShouldHighlightField(fieldName);

            // Assert
            Assert.Equal(shouldHighlight, highlight);
        }

        [Fact]
        public void GetCorrectedFields_ReturnsAllCorrectedFieldNames()
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);
            correctedDoc.FieldCorrections["field1"] = new FieldCorrection();
            correctedDoc.FieldCorrections["field2"] = new FieldCorrection();
            correctedDoc.FieldCorrections["field3"] = new FieldCorrection();

            // Act
            var correctedFields = correctedDoc.GetCorrectedFields();

            // Assert
            Assert.Equal(3, correctedFields.Count);
            Assert.Contains("field1", correctedFields);
            Assert.Contains("field2", correctedFields);
            Assert.Contains("field3", correctedFields);
        }

        [Fact]
        public void GetFieldsWithErrors_ReturnsOnlyInvalidFields()
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);
            correctedDoc.FieldCorrections["validField"] = new FieldCorrection { IsValid = true };
            correctedDoc.FieldCorrections["invalidField1"] = new FieldCorrection { IsValid = false };
            correctedDoc.FieldCorrections["invalidField2"] = new FieldCorrection { IsValid = false };

            // Act
            var fieldsWithErrors = correctedDoc.GetFieldsWithErrors();

            // Assert
            Assert.Equal(2, fieldsWithErrors.Count);
            Assert.Contains("invalidField1", fieldsWithErrors);
            Assert.Contains("invalidField2", fieldsWithErrors);
            Assert.DoesNotContain("validField", fieldsWithErrors);
        }

        [Fact]
        public void GetFieldsWithWarnings_ReturnsFieldsWithWarnings()
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);
            
            var fieldWithWarning = new FieldCorrection();
            fieldWithWarning.ValidationWarnings.Add("Warning 1");
            correctedDoc.FieldCorrections["warningField"] = fieldWithWarning;
            
            correctedDoc.FieldCorrections["normalField"] = new FieldCorrection();

            // Act
            var fieldsWithWarnings = correctedDoc.GetFieldsWithWarnings();

            // Assert
            Assert.Single(fieldsWithWarnings);
            Assert.Contains("warningField", fieldsWithWarnings);
        }

        [Fact]
        public void GetUnsavedCorrectionsCount_ReturnsCorrectCount()
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);
            correctedDoc.FieldCorrections["saved1"] = new FieldCorrection { IsSaved = true };
            correctedDoc.FieldCorrections["saved2"] = new FieldCorrection { IsSaved = true };
            correctedDoc.FieldCorrections["unsaved1"] = new FieldCorrection { IsSaved = false };
            correctedDoc.FieldCorrections["unsaved2"] = new FieldCorrection { IsSaved = false };
            correctedDoc.FieldCorrections["unsaved3"] = new FieldCorrection { IsSaved = false };

            // Act
            var unsavedCount = correctedDoc.GetUnsavedCorrectionsCount();

            // Assert
            Assert.Equal(3, unsavedCount);
        }

        [Theory]
        [InlineData(true, true, true, true)]
        [InlineData(true, true, false, false)]
        [InlineData(false, false, false, false)]
        public void AreAllCorrectionsValid_ReturnsCorrectStatus(
            bool field1Valid, 
            bool field2Valid, 
            bool field3Valid, 
            bool expectedAllValid)
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);
            correctedDoc.FieldCorrections["field1"] = new FieldCorrection { IsValid = field1Valid };
            correctedDoc.FieldCorrections["field2"] = new FieldCorrection { IsValid = field2Valid };
            correctedDoc.FieldCorrections["field3"] = new FieldCorrection { IsValid = field3Valid };

            // Act
            var allValid = correctedDoc.AreAllCorrectionsValid();

            // Assert
            Assert.Equal(expectedAllValid, allValid);
        }

        [Fact]
        public void AreAllCorrectionsValid_WithNoCorrections_ReturnsTrue()
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);

            // Act
            var allValid = correctedDoc.AreAllCorrectionsValid();

            // Assert
            Assert.True(allValid);
        }

        [Theory]
        [InlineData(0.8, new double[] { }, 0.8)]
        [InlineData(0.5, new double[] { 0.9, 0.8, 0.7 }, 0.725)]
        [InlineData(0.6, new double[] { 1.0 }, 0.8)]
        [InlineData(0.4, new double[] { 0.8, 0.9, 1.0 }, 0.775)]
        public void CalculateOverallConfidence_ReturnsCorrectAverage(
            double originalConfidence,
            double[] correctionConfidences,
            double expectedOverall)
        {
            // Arrange
            var originalDoc = new ProcessedCheck { ConfidenceScore = originalConfidence };
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);
            
            for (int i = 0; i < correctionConfidences.Length; i++)
            {
                correctedDoc.FieldCorrections[$"field{i}"] = new FieldCorrection 
                { 
                    UpdatedConfidence = correctionConfidences[i] 
                };
            }

            // Act
            var overallConfidence = correctedDoc.CalculateOverallConfidence();

            // Assert
            Assert.Equal(expectedOverall, overallConfidence, 3);
        }

        [Fact]
        public void CalculateOverallConfidence_WithNullOriginalDocument_ReturnsZero()
        {
            // Arrange
            var correctedDoc = new CorrectedProcessedDocument();

            // Act
            var overallConfidence = correctedDoc.CalculateOverallConfidence();

            // Assert
            Assert.Equal(0.0, overallConfidence);
        }

        [Theory]
        [InlineData(0.5, "Low threshold")]
        [InlineData(0.7, "Default threshold")]
        [InlineData(0.9, "High threshold")]
        public void ConfidenceThreshold_CanBeModified(double threshold, string scenario)
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);

            // Act
            correctedDoc.ConfidenceThreshold = threshold;

            // Assert
            Assert.Equal(threshold, correctedDoc.ConfidenceThreshold);
        }

        [Fact]
        public void Metadata_CanStoreAdditionalInformation()
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);

            // Act
            correctedDoc.Metadata["userId"] = "user123";
            correctedDoc.Metadata["source"] = "manual_review";
            correctedDoc.Metadata["reviewCount"] = 3;

            // Assert
            Assert.Equal(3, correctedDoc.Metadata.Count);
            Assert.Equal("user123", correctedDoc.Metadata["userId"]);
            Assert.Equal("manual_review", correctedDoc.Metadata["source"]);
            Assert.Equal(3, correctedDoc.Metadata["reviewCount"]);
        }

        [Fact]
        public void HasUnsavedChanges_TracksModificationState()
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);

            // Act & Assert
            Assert.False(correctedDoc.HasUnsavedChanges);
            
            correctedDoc.HasUnsavedChanges = true;
            Assert.True(correctedDoc.HasUnsavedChanges);
            
            correctedDoc.HasUnsavedChanges = false;
            Assert.False(correctedDoc.HasUnsavedChanges);
        }

        [Fact]
        public void LastModifiedAt_CanBeUpdated()
        {
            // Arrange
            var originalDoc = new ProcessedCheck();
            var correctedDoc = new CorrectedProcessedDocument(originalDoc);
            var originalModified = correctedDoc.LastModifiedAt;

            // Act
            Thread.Sleep(10); // Small delay to ensure time difference
            correctedDoc.LastModifiedAt = DateTime.UtcNow;

            // Assert
            Assert.True(correctedDoc.LastModifiedAt > originalModified);
        }

        [Fact]
        public void CorrectionSessionId_IsUnique()
        {
            // Arrange
            var originalDoc = new ProcessedCheck();

            // Act
            var correctedDoc1 = new CorrectedProcessedDocument(originalDoc);
            var correctedDoc2 = new CorrectedProcessedDocument(originalDoc);

            // Assert
            Assert.NotEqual(correctedDoc1.CorrectionSessionId, correctedDoc2.CorrectionSessionId);
        }
    }
}