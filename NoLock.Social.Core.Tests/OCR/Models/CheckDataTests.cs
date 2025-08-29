using NoLock.Social.Core.OCR.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Models
{
    /// <summary>
    /// Unit tests for the CheckData model focusing on validation and routing number logic.
    /// </summary>
    public class CheckDataTests
    {
        [Fact]
        public void CheckData_DefaultValues()
        {
            // Arrange & Act
            var checkData = new CheckData();

            // Assert
            Assert.Equal(string.Empty, checkData.BankName);
            Assert.Equal(string.Empty, checkData.RoutingNumber);
            Assert.Equal(string.Empty, checkData.AccountNumber);
            Assert.Equal(string.Empty, checkData.CheckNumber);
            Assert.Null(checkData.AmountNumeric);
            Assert.Equal(string.Empty, checkData.AmountWritten);
            Assert.Null(checkData.AmountWrittenParsed);
            Assert.Equal(string.Empty, checkData.Payee);
            Assert.Null(checkData.Date);
            Assert.Equal(string.Empty, checkData.Memo);
            Assert.Equal(string.Empty, checkData.PayerName);
            Assert.Equal(string.Empty, checkData.PayerAddress);
            Assert.Equal(string.Empty, checkData.MicrLine);
            Assert.False(checkData.SignatureDetected);
            Assert.Equal(0, checkData.SignatureConfidence);
            Assert.False(checkData.IsRoutingNumberValid);
            Assert.False(checkData.AmountsMatch);
            Assert.NotNull(checkData.ValidationErrors);
            Assert.Empty(checkData.ValidationErrors);
            Assert.Equal(string.Empty, checkData.Notes);
        }

        [Fact]
        public void Validate_WithAllRequiredFields_ReturnsTrue()
        {
            // Arrange
            var checkData = CreateValidCheckData();

            // Act
            var isValid = checkData.Validate();

            // Assert
            Assert.True(isValid);
            Assert.Empty(checkData.ValidationErrors);
        }

        [Theory]
        [InlineData("", "Routing number is missing")]
        [InlineData("12345678", "Routing number must be 9 digits")]
        [InlineData("1234567890", "Routing number must be 9 digits")]
        [InlineData("abcdefghi", "Routing number must be 9 digits")]
        public void Validate_InvalidRoutingNumber(string routingNumber, string expectedError)
        {
            // Arrange
            var checkData = CreateValidCheckData();
            checkData.RoutingNumber = routingNumber;

            // Act
            var isValid = checkData.Validate();

            // Assert
            Assert.False(isValid);
            Assert.Contains(expectedError, checkData.ValidationErrors);
        }

        [Fact]
        public void Validate_MissingAccountNumber_ReturnsFalse()
        {
            // Arrange
            var checkData = CreateValidCheckData();
            checkData.AccountNumber = "";

            // Act
            var isValid = checkData.Validate();

            // Assert
            Assert.False(isValid);
            Assert.Contains("Account number is missing", checkData.ValidationErrors);
        }

        [Fact]
        public void Validate_MissingCheckNumber_ReturnsFalse()
        {
            // Arrange
            var checkData = CreateValidCheckData();
            checkData.CheckNumber = "";

            // Act
            var isValid = checkData.Validate();

            // Assert
            Assert.False(isValid);
            Assert.Contains("Check number is missing", checkData.ValidationErrors);
        }

        [Theory]
        [InlineData(null, "Numeric amount is missing or invalid")]
        [InlineData(0, "Numeric amount is missing or invalid")]
        [InlineData(-100, "Numeric amount is missing or invalid")]
        public void Validate_InvalidAmountNumeric(decimal? amount, string expectedError)
        {
            // Arrange
            var checkData = CreateValidCheckData();
            checkData.AmountNumeric = amount;

            // Act
            var isValid = checkData.Validate();

            // Assert
            Assert.False(isValid);
            Assert.Contains(expectedError, checkData.ValidationErrors);
        }

        [Fact]
        public void Validate_MissingPayee_ReturnsFalse()
        {
            // Arrange
            var checkData = CreateValidCheckData();
            checkData.Payee = "";

            // Act
            var isValid = checkData.Validate();

            // Assert
            Assert.False(isValid);
            Assert.Contains("Payee is missing", checkData.ValidationErrors);
        }

        [Fact]
        public void Validate_MissingDate_ReturnsFalse()
        {
            // Arrange
            var checkData = CreateValidCheckData();
            checkData.Date = null;

            // Act
            var isValid = checkData.Validate();

            // Assert
            Assert.False(isValid);
            Assert.Contains("Date is missing", checkData.ValidationErrors);
        }

        [Theory]
        [InlineData(100.00, 100.00, true, "Amounts match exactly")]
        [InlineData(100.00, 100.005, true, "Amounts match within tolerance")]
        [InlineData(100.00, 99.995, true, "Amounts match within tolerance negative")]
        [InlineData(100.00, 100.01, false, "Amounts don't match at tolerance boundary")]
        [InlineData(100.00, 100.02, false, "Amounts don't match beyond tolerance")]
        [InlineData(100.00, 99.98, false, "Amounts don't match beyond tolerance negative")]
        public void Validate_AmountMatching(
            double numericAmount, 
            double writtenAmount, 
            bool expectedMatch,
            string scenario)
        {
            // Arrange
            var checkData = CreateValidCheckData();
            checkData.AmountNumeric = (decimal)numericAmount;
            checkData.AmountWrittenParsed = (decimal)writtenAmount;

            // Act
            checkData.Validate();

            // Assert
            Assert.Equal(expectedMatch, checkData.AmountsMatch);
            
            if (!expectedMatch)
            {
                Assert.Contains($"Amount mismatch: Numeric=${numericAmount:F2}, Written=${writtenAmount:F2}", 
                    checkData.ValidationErrors);
            }
        }

        [Fact]
        public void Validate_WithoutWrittenAmount_SkipsAmountComparison()
        {
            // Arrange
            var checkData = CreateValidCheckData();
            checkData.AmountWrittenParsed = null;

            // Act
            var isValid = checkData.Validate();

            // Assert
            Assert.True(isValid);
            Assert.False(checkData.AmountsMatch);
            Assert.DoesNotContain("Amount mismatch", checkData.ValidationErrors);
        }

        [Fact]
        public void Validate_ClearsExistingErrors()
        {
            // Arrange
            var checkData = CreateValidCheckData();
            checkData.ValidationErrors.Add("Old error 1");
            checkData.ValidationErrors.Add("Old error 2");

            // Act
            var isValid = checkData.Validate();

            // Assert
            Assert.True(isValid);
            Assert.Empty(checkData.ValidationErrors);
        }

        [Fact]
        public void Validate_MultipleErrors_AllCaptured()
        {
            // Arrange
            var checkData = new CheckData
            {
                RoutingNumber = "123", // Wrong length
                AccountNumber = "", // Missing
                CheckNumber = "", // Missing
                AmountNumeric = -50, // Invalid
                Payee = "", // Missing
                Date = null // Missing
            };

            // Act
            var isValid = checkData.Validate();

            // Assert
            Assert.False(isValid);
            Assert.Equal(6, checkData.ValidationErrors.Count);
            Assert.Contains("Routing number must be 9 digits", checkData.ValidationErrors);
            Assert.Contains("Account number is missing", checkData.ValidationErrors);
            Assert.Contains("Check number is missing", checkData.ValidationErrors);
            Assert.Contains("Numeric amount is missing or invalid", checkData.ValidationErrors);
            Assert.Contains("Payee is missing", checkData.ValidationErrors);
            Assert.Contains("Date is missing", checkData.ValidationErrors);
        }

        [Theory]
        [InlineData("First National Bank", "Bank name")]
        [InlineData("", "Empty bank name")]
        [InlineData("Community Credit Union of Springfield", "Long bank name")]
        public void BankName_VariousValues(string bankName, string scenario)
        {
            // Arrange & Act
            var checkData = new CheckData { BankName = bankName };

            // Assert
            Assert.Equal(bankName, checkData.BankName);
        }

        [Theory]
        [InlineData("For rent payment", "Rent memo")]
        [InlineData("", "Empty memo")]
        [InlineData("Invoice #12345 - Q3 2024 Services", "Detailed memo")]
        public void Memo_VariousValues(string memo, string scenario)
        {
            // Arrange & Act
            var checkData = new CheckData { Memo = memo };

            // Assert
            Assert.Equal(memo, checkData.Memo);
        }

        [Theory]
        [InlineData("John Smith", "123 Main St, City, ST 12345", "Standard address")]
        [InlineData("Jane Doe", "", "Empty address")]
        [InlineData("Business Corp", "Suite 500, Tower Building, 999 Corporate Blvd", "Business address")]
        public void PayerInformation_SetCorrectly(string payerName, string payerAddress, string scenario)
        {
            // Arrange & Act
            var checkData = new CheckData
            {
                PayerName = payerName,
                PayerAddress = payerAddress
            };

            // Assert
            Assert.Equal(payerName, checkData.PayerName);
            Assert.Equal(payerAddress, checkData.PayerAddress);
        }

        [Theory]
        [InlineData("⑈123456789⑈ 987654321⑆ 1001", "Standard MICR")]
        [InlineData("", "Empty MICR")]
        [InlineData("INVALID_MICR_DATA", "Invalid MICR format")]
        public void MicrLine_VariousValues(string micrLine, string scenario)
        {
            // Arrange & Act
            var checkData = new CheckData { MicrLine = micrLine };

            // Assert
            Assert.Equal(micrLine, checkData.MicrLine);
        }

        [Theory]
        [InlineData(true, 0.95, "High confidence signature")]
        [InlineData(true, 0.50, "Low confidence signature")]
        [InlineData(false, 0.0, "No signature")]
        public void SignatureProperties_SetCorrectly(bool detected, double confidence, string scenario)
        {
            // Arrange & Act
            var checkData = new CheckData
            {
                SignatureDetected = detected,
                SignatureConfidence = confidence
            };

            // Assert
            Assert.Equal(detected, checkData.SignatureDetected);
            Assert.Equal(confidence, checkData.SignatureConfidence);
        }

        [Theory]
        [InlineData(true, "Valid routing number")]
        [InlineData(false, "Invalid routing number")]
        public void IsRoutingNumberValid_Property(bool isValid, string scenario)
        {
            // Arrange & Act
            var checkData = new CheckData { IsRoutingNumberValid = isValid };

            // Assert
            Assert.Equal(isValid, checkData.IsRoutingNumberValid);
        }

        [Fact]
        public void AmountWritten_CanStoreTextualAmount()
        {
            // Arrange
            var writtenAmount = "One hundred and fifty dollars and 25/100";
            
            // Act
            var checkData = new CheckData
            {
                AmountWritten = writtenAmount,
                AmountWrittenParsed = 150.25m
            };

            // Assert
            Assert.Equal(writtenAmount, checkData.AmountWritten);
            Assert.Equal(150.25m, checkData.AmountWrittenParsed);
        }

        [Fact]
        public void Notes_CanStoreAdditionalInformation()
        {
            // Arrange
            var notes = "Check image quality was poor. Manual review required.";
            
            // Act
            var checkData = new CheckData { Notes = notes };

            // Assert
            Assert.Equal(notes, checkData.Notes);
        }

        [Theory]
        [InlineData("1001", "Standard check number")]
        [InlineData("0001", "Leading zeros")]
        [InlineData("999999", "Large check number")]
        [InlineData("CK-2024-001", "Alphanumeric check number")]
        public void CheckNumber_VariousFormats(string checkNumber, string scenario)
        {
            // Arrange & Act
            var checkData = new CheckData { CheckNumber = checkNumber };

            // Assert
            Assert.Equal(checkNumber, checkData.CheckNumber);
        }

        [Theory]
        [InlineData("987654321", "Standard account")]
        [InlineData("000123456", "Leading zeros")]
        [InlineData("ACC-123-456-789", "Formatted account")]
        public void AccountNumber_VariousFormats(string accountNumber, string scenario)
        {
            // Arrange & Act
            var checkData = new CheckData { AccountNumber = accountNumber };

            // Assert
            Assert.Equal(accountNumber, checkData.AccountNumber);
        }

        [Fact]
        public void Date_CanBeSetToVariousDates()
        {
            // Arrange
            var pastDate = DateTime.UtcNow.AddDays(-30);
            var currentDate = DateTime.UtcNow;
            var futureDate = DateTime.UtcNow.AddDays(30);

            // Act & Assert
            var checkData1 = new CheckData { Date = pastDate };
            Assert.Equal(pastDate, checkData1.Date);

            var checkData2 = new CheckData { Date = currentDate };
            Assert.Equal(currentDate, checkData2.Date);

            var checkData3 = new CheckData { Date = futureDate };
            Assert.Equal(futureDate, checkData3.Date);
        }

        private CheckData CreateValidCheckData()
        {
            return new CheckData
            {
                BankName = "First National Bank",
                RoutingNumber = "123456789",
                AccountNumber = "987654321",
                CheckNumber = "1001",
                AmountNumeric = 150.50m,
                AmountWritten = "One hundred fifty and 50/100",
                AmountWrittenParsed = 150.50m,
                Payee = "John Doe",
                Date = DateTime.UtcNow,
                Memo = "Monthly payment",
                PayerName = "Jane Smith",
                PayerAddress = "123 Main St",
                MicrLine = "⑈123456789⑈ 987654321⑆ 1001",
                SignatureDetected = true,
                SignatureConfidence = 0.95,
                IsRoutingNumberValid = true,
                AmountsMatch = true
            };
        }
    }
}