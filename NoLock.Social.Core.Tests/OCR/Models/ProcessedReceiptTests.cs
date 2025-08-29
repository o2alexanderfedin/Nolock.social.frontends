using NoLock.Social.Core.OCR.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Models
{
    /// <summary>
    /// Unit tests for the ProcessedReceipt model focusing on validation and total calculations.
    /// </summary>
    public class ProcessedReceiptTests
    {
        [Fact]
        public void Constructor_SetsDocumentTypeToReceipt()
        {
            // Arrange & Act
            var processedReceipt = new ProcessedReceipt();

            // Assert
            Assert.Equal("Receipt", processedReceipt.DocumentType);
        }

        [Fact]
        public void ReceiptData_InitializedByDefault()
        {
            // Arrange & Act
            var processedReceipt = new ProcessedReceipt();

            // Assert
            Assert.NotNull(processedReceipt.ReceiptData);
        }

        [Theory]
        [InlineData(100.00, 8.00, 108.00, true, "Valid totals")]
        [InlineData(50.00, 5.00, 55.01, false, "Total mismatch by 1 cent")]
        [InlineData(200.00, 16.00, 215.99, false, "Total mismatch by 1 cent negative")]
        [InlineData(0.00, 0.00, 0.00, true, "Zero amounts")]
        [InlineData(99.99, 7.77, 107.76, true, "Valid with decimals")]
        public void Validate_TotalCalculation(
            double subtotal, 
            double tax, 
            double total, 
            bool expectValid,
            string scenario)
        {
            // Arrange
            var processedReceipt = new ProcessedReceipt
            {
                DocumentType = "Receipt",
                ConfidenceScore = 0.8,
                ReceiptData = new ReceiptData
                {
                    Subtotal = (decimal)subtotal,
                    TaxAmount = (decimal)tax,
                    Total = (decimal)total
                }
            };

            // Act
            var result = processedReceipt.Validate();

            // Assert
            Assert.Equal(expectValid, result);
            
            if (!expectValid)
            {
                var expectedTotal = subtotal + tax;
                Assert.Contains($"Total mismatch: Expected ${expectedTotal:C} but got ${total:C}", 
                    processedReceipt.Warnings);
            }
        }

        [Theory]
        [InlineData(-100.00, "Total amount cannot be negative.")]
        [InlineData(-50.00, "Subtotal amount cannot be negative.")]
        [InlineData(-10.00, "Tax amount cannot be negative.")]
        public void Validate_NegativeAmounts_AreInvalid(decimal negativeAmount, string expectedError)
        {
            // Arrange
            var processedReceipt = new ProcessedReceipt
            {
                DocumentType = "Receipt",
                ConfidenceScore = 0.8,
                ReceiptData = new ReceiptData
                {
                    Subtotal = expectedError.Contains("Subtotal") ? negativeAmount : 100m,
                    TaxAmount = expectedError.Contains("Tax") ? negativeAmount : 8m,
                    Total = expectedError.Contains("Total") && !expectedError.Contains("Subtotal") ? negativeAmount : 108m
                }
            };

            // Act
            var result = processedReceipt.Validate();

            // Assert
            Assert.False(result);
            Assert.Contains(expectedError, processedReceipt.ValidationErrors);
        }

        [Fact]
        public void Validate_WithNullReceiptData_ReturnsFalse()
        {
            // Arrange
            var processedReceipt = new ProcessedReceipt
            {
                DocumentType = "Receipt",
                ConfidenceScore = 0.8,
                ReceiptData = null
            };

            // Act
            var result = processedReceipt.Validate();

            // Assert
            Assert.False(result);
            Assert.Contains("Receipt data is required.", processedReceipt.ValidationErrors);
        }

        [Fact]
        public void Validate_InheritsBaseValidation()
        {
            // Arrange
            var processedReceipt = new ProcessedReceipt
            {
                DocumentType = "", // Invalid
                ConfidenceScore = 2.0, // Invalid
                ProcessedAt = DateTime.UtcNow.AddDays(1), // Future date
                ReceiptData = new ReceiptData
                {
                    Subtotal = 100m,
                    TaxAmount = 8m,
                    Total = 108m
                }
            };

            // Act
            var result = processedReceipt.Validate();

            // Assert
            Assert.False(result);
            Assert.Contains("Document type is required.", processedReceipt.ValidationErrors);
            Assert.Contains("Confidence score must be between 0 and 1.", processedReceipt.ValidationErrors);
            Assert.Contains("Processed date cannot be in the future.", processedReceipt.ValidationErrors);
        }

        [Theory]
        [InlineData(100.00, 8.00, 108.00, 0.00, "Exact match")]
        [InlineData(100.00, 8.00, 108.005, 0.005, "Half cent difference")]
        [InlineData(100.00, 8.00, 107.995, 0.005, "Half cent difference negative")]
        [InlineData(100.00, 8.00, 108.01, 0.01, "One cent difference")]
        public void TotalMismatch_ToleranceTest(
            decimal subtotal, 
            decimal tax, 
            decimal total, 
            decimal expectedDifference,
            string scenario)
        {
            // Arrange
            var processedReceipt = new ProcessedReceipt
            {
                DocumentType = "Receipt",
                ConfidenceScore = 0.8,
                ReceiptData = new ReceiptData
                {
                    Subtotal = subtotal,
                    TaxAmount = tax,
                    Total = total
                }
            };

            // Act
            processedReceipt.Validate();
            var calculatedTotal = subtotal + tax;
            var actualDifference = Math.Abs(calculatedTotal - total);

            // Assert
            Assert.Equal(expectedDifference, actualDifference);
            
            // Warning should only be added if difference > 0.01
            if (actualDifference > 0.01m)
            {
                Assert.Contains("Total mismatch:", processedReceipt.Warnings);
            }
            else
            {
                Assert.DoesNotContain("Total mismatch:", processedReceipt.Warnings);
            }
        }

        [Fact]
        public void Warnings_PreservedDuringValidation()
        {
            // Arrange
            var processedReceipt = new ProcessedReceipt
            {
                DocumentType = "Receipt",
                ConfidenceScore = 0.8,
                ReceiptData = new ReceiptData
                {
                    Subtotal = 100m,
                    TaxAmount = 8m,
                    Total = 109m // Mismatch to generate warning
                }
            };
            
            processedReceipt.Warnings.Add("Pre-existing warning");

            // Act
            processedReceipt.Validate();

            // Assert
            Assert.Equal(2, processedReceipt.Warnings.Count);
            Assert.Contains("Pre-existing warning", processedReceipt.Warnings);
            Assert.Contains("Total mismatch: Expected $108.00 but got $109.00", processedReceipt.Warnings);
        }

        [Fact]
        public void ValidationErrors_ClearedOnValidate()
        {
            // Arrange
            var processedReceipt = new ProcessedReceipt
            {
                DocumentType = "Receipt",
                ConfidenceScore = 0.8,
                ReceiptData = new ReceiptData
                {
                    Subtotal = 100m,
                    TaxAmount = 8m,
                    Total = 108m
                }
            };
            
            processedReceipt.ValidationErrors.Add("Old error");

            // Act
            processedReceipt.Validate();

            // Assert
            Assert.Empty(processedReceipt.ValidationErrors);
        }

        [Theory]
        [InlineData(0.0, "Zero confidence")]
        [InlineData(0.25, "Low confidence")]
        [InlineData(0.5, "Medium confidence")]
        [InlineData(0.75, "High confidence")]
        [InlineData(1.0, "Perfect confidence")]
        public void ConfidenceScore_VariousValues(double confidence, string scenario)
        {
            // Arrange
            var processedReceipt = new ProcessedReceipt
            {
                ConfidenceScore = confidence,
                ReceiptData = new ReceiptData
                {
                    Subtotal = 100m,
                    TaxAmount = 8m,
                    Total = 108m
                }
            };

            // Act
            var result = processedReceipt.Validate();

            // Assert
            Assert.True(result);
            Assert.Equal(confidence, processedReceipt.ConfidenceScore);
        }

        [Fact]
        public void Metadata_InitializedAsEmptyDictionary()
        {
            // Arrange & Act
            var processedReceipt = new ProcessedReceipt();

            // Assert
            Assert.NotNull(processedReceipt.Metadata);
            Assert.Empty(processedReceipt.Metadata);
        }

        [Theory]
        [InlineData("receipt_001.pdf", "application/pdf", 2048)]
        [InlineData("scan.jpg", "image/jpeg", 1048576)]
        [InlineData("photo.png", "image/png", 524288)]
        public void FileProperties_SetCorrectly(string fileName, string mimeType, long fileSize)
        {
            // Arrange & Act
            var processedReceipt = new ProcessedReceipt
            {
                OriginalFileName = fileName,
                MimeType = mimeType,
                FileSizeBytes = fileSize
            };

            // Assert
            Assert.Equal(fileName, processedReceipt.OriginalFileName);
            Assert.Equal(mimeType, processedReceipt.MimeType);
            Assert.Equal(fileSize, processedReceipt.FileSizeBytes);
        }

        [Fact]
        public void ProcessedAt_DefaultsToUtcNow()
        {
            // Arrange
            var before = DateTime.UtcNow;
            var processedReceipt = new ProcessedReceipt();
            var after = DateTime.UtcNow;

            // Assert
            Assert.InRange(processedReceipt.ProcessedAt, before.AddSeconds(-1), after.AddSeconds(1));
        }

        [Fact]
        public void RawOcrText_CanStoreExtractedText()
        {
            // Arrange
            var ocrText = "Store Name\n123 Main St\nSubtotal: $100.00\nTax: $8.00\nTotal: $108.00";
            var processedReceipt = new ProcessedReceipt
            {
                RawOcrText = ocrText
            };

            // Assert
            Assert.Equal(ocrText, processedReceipt.RawOcrText);
        }

        [Fact]
        public void DigitalSignature_PropertiesWork()
        {
            // Arrange
            var processedReceipt = new ProcessedReceipt
            {
                IsSigned = true,
                DigitalSignature = "signature_hash_abc",
                SignaturePublicKey = "public_key_xyz"
            };

            // Assert
            Assert.True(processedReceipt.IsSigned);
            Assert.Equal("signature_hash_abc", processedReceipt.DigitalSignature);
            Assert.Equal("public_key_xyz", processedReceipt.SignaturePublicKey);
        }

        [Theory]
        [InlineData("DOC123456", "Document ID")]
        [InlineData("", "Empty document ID")]
        [InlineData("SHA256_HASH_VALUE", "SHA-256 hash")]
        public void DocumentId_VariousValues(string documentId, string scenario)
        {
            // Arrange & Act
            var processedReceipt = new ProcessedReceipt
            {
                DocumentId = documentId
            };

            // Assert
            Assert.Equal(documentId, processedReceipt.DocumentId);
        }

        [Fact]
        public void MultipleValidationErrors_AllCaptured()
        {
            // Arrange
            var processedReceipt = new ProcessedReceipt
            {
                DocumentType = "", // Invalid
                ConfidenceScore = -0.5, // Invalid
                ProcessedAt = DateTime.UtcNow.AddHours(2), // Future
                ReceiptData = new ReceiptData
                {
                    Subtotal = -100m, // Negative
                    TaxAmount = -8m, // Negative
                    Total = -108m // Negative
                }
            };

            // Act
            var result = processedReceipt.Validate();

            // Assert
            Assert.False(result);
            Assert.Contains("Document type is required.", processedReceipt.ValidationErrors);
            Assert.Contains("Confidence score must be between 0 and 1.", processedReceipt.ValidationErrors);
            Assert.Contains("Processed date cannot be in the future.", processedReceipt.ValidationErrors);
            Assert.Contains("Total amount cannot be negative.", processedReceipt.ValidationErrors);
            Assert.Contains("Subtotal amount cannot be negative.", processedReceipt.ValidationErrors);
            Assert.Contains("Tax amount cannot be negative.", processedReceipt.ValidationErrors);
        }
    }
}