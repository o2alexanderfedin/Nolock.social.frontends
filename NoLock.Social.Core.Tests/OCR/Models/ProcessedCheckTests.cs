using NoLock.Social.Core.OCR.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Models
{
    /// <summary>
    /// Unit tests for the ProcessedCheck model focusing on validation and business logic.
    /// </summary>
    public class ProcessedCheckTests
    {
        [Fact]
        public void Constructor_SetsDocumentTypeToCheck()
        {
            // Arrange & Act
            var processedCheck = new ProcessedCheck();

            // Assert
            Assert.Equal("Check", processedCheck.DocumentType);
        }

        [Fact]
        public void CheckData_InitializedByDefault()
        {
            // Arrange & Act
            var processedCheck = new ProcessedCheck();

            // Assert
            Assert.NotNull(processedCheck.CheckData);
        }

        [Theory]
        [InlineData(true, true, true, true, "All validations pass")]
        [InlineData(false, true, true, false, "Invalid routing number")]
        [InlineData(true, false, true, true, "Amounts don't match")]
        [InlineData(true, true, false, true, "No signature detected")]
        [InlineData(false, false, false, false, "All validations fail")]
        public void Validate_CheckSpecificValidations(
            bool isRoutingValid, 
            bool amountsMatch, 
            bool signatureDetected, 
            bool expectedValid,
            string scenario)
        {
            // Arrange
            var processedCheck = new ProcessedCheck
            {
                DocumentType = "Check",
                ConfidenceScore = 0.8,
                CheckData = new CheckData
                {
                    IsRoutingNumberValid = isRoutingValid,
                    AmountsMatch = amountsMatch,
                    SignatureDetected = signatureDetected,
                    RoutingNumber = "123456789",
                    AccountNumber = "987654321",
                    CheckNumber = "1001",
                    AmountNumeric = 100.00m,
                    Payee = "John Doe",
                    Date = DateTime.UtcNow
                }
            };

            // Act
            var result = processedCheck.Validate();

            // Assert
            Assert.Equal(expectedValid, result);
            
            if (!isRoutingValid)
            {
                Assert.Contains("Invalid routing number checksum.", processedCheck.ValidationErrors);
            }
            
            if (!amountsMatch)
            {
                Assert.Contains("Written and numeric amounts do not match", processedCheck.Warnings);
            }
            
            if (!signatureDetected)
            {
                Assert.Contains("No signature detected on check", processedCheck.Warnings);
            }
        }

        [Fact]
        public void Validate_WithNullCheckData_ReturnsFalse()
        {
            // Arrange
            var processedCheck = new ProcessedCheck
            {
                DocumentType = "Check",
                ConfidenceScore = 0.8,
                CheckData = null
            };

            // Act
            var result = processedCheck.Validate();

            // Assert
            Assert.False(result);
            Assert.Contains("Check data is required.", processedCheck.ValidationErrors);
        }

        [Fact]
        public void Validate_InheritsBaseValidation()
        {
            // Arrange
            var processedCheck = new ProcessedCheck
            {
                DocumentType = "", // Invalid
                ConfidenceScore = 1.5, // Invalid
                ProcessedAt = DateTime.UtcNow.AddHours(1), // Future date
                CheckData = new CheckData
                {
                    RoutingNumber = "123456789",
                    AccountNumber = "987654321",
                    CheckNumber = "1001",
                    AmountNumeric = 100.00m,
                    Payee = "John Doe",
                    Date = DateTime.UtcNow,
                    IsRoutingNumberValid = true,
                    AmountsMatch = true,
                    SignatureDetected = true
                }
            };

            // Act
            var result = processedCheck.Validate();

            // Assert
            Assert.False(result);
            Assert.Contains("Document type is required.", processedCheck.ValidationErrors);
            Assert.Contains("Confidence score must be between 0 and 1.", processedCheck.ValidationErrors);
            Assert.Contains("Processed date cannot be in the future.", processedCheck.ValidationErrors);
        }

        [Fact]
        public void Validate_PropagatesCheckDataValidationErrors()
        {
            // Arrange
            var processedCheck = new ProcessedCheck
            {
                DocumentType = "Check",
                ConfidenceScore = 0.8,
                CheckData = new CheckData
                {
                    // Missing required fields
                    RoutingNumber = "",
                    AccountNumber = "",
                    CheckNumber = "",
                    AmountNumeric = null,
                    Payee = "",
                    Date = null
                }
            };

            // Act
            var result = processedCheck.Validate();

            // Assert
            Assert.False(result);
            Assert.Contains("Routing number is missing", processedCheck.ValidationErrors);
            Assert.Contains("Account number is missing", processedCheck.ValidationErrors);
            Assert.Contains("Check number is missing", processedCheck.ValidationErrors);
            Assert.Contains("Numeric amount is missing or invalid", processedCheck.ValidationErrors);
            Assert.Contains("Payee is missing", processedCheck.ValidationErrors);
            Assert.Contains("Date is missing", processedCheck.ValidationErrors);
        }

        [Theory]
        [InlineData(0.0, "Low confidence")]
        [InlineData(0.5, "Medium confidence")]
        [InlineData(0.95, "High confidence")]
        [InlineData(1.0, "Perfect confidence")]
        public void ConfidenceScore_VariousValues(double confidence, string scenario)
        {
            // Arrange
            var processedCheck = new ProcessedCheck
            {
                ConfidenceScore = confidence,
                CheckData = CreateValidCheckData()
            };

            // Act
            var result = processedCheck.Validate();

            // Assert
            Assert.True(result);
            Assert.Equal(confidence, processedCheck.ConfidenceScore);
        }

        [Fact]
        public void Warnings_AccumulateCorrectly()
        {
            // Arrange
            var processedCheck = new ProcessedCheck
            {
                DocumentType = "Check",
                ConfidenceScore = 0.8,
                CheckData = new CheckData
                {
                    RoutingNumber = "123456789",
                    AccountNumber = "987654321",
                    CheckNumber = "1001",
                    AmountNumeric = 100.00m,
                    AmountWrittenParsed = 150.00m, // Mismatch
                    Payee = "John Doe",
                    Date = DateTime.UtcNow,
                    IsRoutingNumberValid = true,
                    AmountsMatch = false,
                    SignatureDetected = false
                }
            };

            processedCheck.Warnings.Add("Pre-existing warning");

            // Act
            processedCheck.Validate();

            // Assert
            Assert.Equal(3, processedCheck.Warnings.Count);
            Assert.Contains("Pre-existing warning", processedCheck.Warnings);
            Assert.Contains("Written and numeric amounts do not match", processedCheck.Warnings);
            Assert.Contains("No signature detected on check", processedCheck.Warnings);
        }

        [Fact]
        public void ValidationErrors_ClearedOnValidate()
        {
            // Arrange
            var processedCheck = new ProcessedCheck
            {
                DocumentType = "Check",
                ConfidenceScore = 0.8,
                CheckData = CreateValidCheckData()
            };
            
            processedCheck.ValidationErrors.Add("Old error");

            // Act
            processedCheck.Validate();

            // Assert
            Assert.Empty(processedCheck.ValidationErrors);
        }

        [Fact]
        public void Metadata_InitializedAsEmptyDictionary()
        {
            // Arrange & Act
            var processedCheck = new ProcessedCheck();

            // Assert
            Assert.NotNull(processedCheck.Metadata);
            Assert.Empty(processedCheck.Metadata);
        }

        [Fact]
        public void DigitalSignature_PropertiesWork()
        {
            // Arrange
            var processedCheck = new ProcessedCheck
            {
                IsSigned = true,
                DigitalSignature = "signature_hash_123",
                SignaturePublicKey = "public_key_456"
            };

            // Assert
            Assert.True(processedCheck.IsSigned);
            Assert.Equal("signature_hash_123", processedCheck.DigitalSignature);
            Assert.Equal("public_key_456", processedCheck.SignaturePublicKey);
        }

        [Theory]
        [InlineData("application/pdf", 1024, "PDF document")]
        [InlineData("image/jpeg", 2048576, "JPEG image")]
        [InlineData("image/png", 512000, "PNG image")]
        public void FileProperties_SetCorrectly(string mimeType, long fileSize, string scenario)
        {
            // Arrange & Act
            var processedCheck = new ProcessedCheck
            {
                MimeType = mimeType,
                FileSizeBytes = fileSize,
                OriginalFileName = $"check_{scenario}.ext"
            };

            // Assert
            Assert.Equal(mimeType, processedCheck.MimeType);
            Assert.Equal(fileSize, processedCheck.FileSizeBytes);
            Assert.Contains(scenario, processedCheck.OriginalFileName);
        }

        [Fact]
        public void ProcessedAt_DefaultsToUtcNow()
        {
            // Arrange
            var before = DateTime.UtcNow;
            var processedCheck = new ProcessedCheck();
            var after = DateTime.UtcNow;

            // Assert
            Assert.InRange(processedCheck.ProcessedAt, before.AddSeconds(-1), after.AddSeconds(1));
        }

        [Fact]
        public void RawOcrText_CanStoreExtractedText()
        {
            // Arrange
            var ocrText = "Pay to the order of John Doe\n$100.00\nOne hundred dollars";
            var processedCheck = new ProcessedCheck
            {
                RawOcrText = ocrText
            };

            // Assert
            Assert.Equal(ocrText, processedCheck.RawOcrText);
        }

        private CheckData CreateValidCheckData()
        {
            return new CheckData
            {
                RoutingNumber = "123456789",
                AccountNumber = "987654321",
                CheckNumber = "1001",
                AmountNumeric = 100.00m,
                AmountWrittenParsed = 100.00m,
                Payee = "John Doe",
                Date = DateTime.UtcNow,
                IsRoutingNumberValid = true,
                AmountsMatch = true,
                SignatureDetected = true
            };
        }
    }
}