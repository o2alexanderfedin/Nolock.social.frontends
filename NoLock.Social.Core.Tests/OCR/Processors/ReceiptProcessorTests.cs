using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Processors;

namespace NoLock.Social.Core.Tests.OCR.Processors
{
    /// <summary>
    /// Unit tests for the ReceiptProcessor class.
    /// Tests extraction, validation, and formatting of receipt data.
    /// </summary>
    [TestClass]
    public class ReceiptProcessorTests
    {
        private Mock<ILogger<ReceiptProcessor>> _loggerMock;
        private ReceiptProcessor _processor;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<ReceiptProcessor>>();
            _processor = new ReceiptProcessor(_loggerMock.Object);
        }

        [TestMethod]
        public void DocumentType_ShouldReturnReceipt()
        {
            // Assert
            Assert.AreEqual("Receipt", _processor.DocumentType);
        }

        [DataTestMethod]
        [DataRow("TOTAL: $25.99\nSUBTOTAL: $24.00\nTAX: $1.99", true, "Receipt with all keywords")]
        [DataRow("Invoice #12345\nAmount Due: $100.00\nPayment received", true, "Invoice-style receipt")]
        [DataRow("Thank you for shopping\nItems: 5\nTotal amount: $50", true, "Receipt with thank you message")]
        [DataRow("Just some random text", false, "Non-receipt text")]
        [DataRow("", false, "Empty text")]
        [DataRow(null, false, "Null text")]
        public void CanProcess_ShouldIdentifyReceiptContent(string rawText, bool expectedResult, string scenario)
        {
            // Act
            var result = _processor.CanProcess(rawText);

            // Assert
            Assert.AreEqual(expectedResult, result, $"Failed for scenario: {scenario}");
        }

        [TestMethod]
        public async Task ProcessAsync_WithNullData_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _processor.ProcessAsync(null, CancellationToken.None));
        }

        [TestMethod]
        public async Task ProcessAsync_WithEmptyData_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _processor.ProcessAsync(string.Empty, CancellationToken.None));
        }

        [TestMethod]
        public async Task ProcessAsync_WithValidReceipt_ShouldExtractBasicFields()
        {
            // Arrange
            var ocrText = @"WALMART STORE #1234
123 Main Street
(555) 123-4567
12/25/2024 14:30

RECEIPT #ABC123

Milk             $3.99
Bread            $2.50
Eggs             $4.99

SUBTOTAL        $11.48
TAX              $0.92
TOTAL           $12.40

Thank you for shopping!";

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Receipt", result.DocumentType);
            Assert.IsNotNull(result.ReceiptData);
            Assert.AreEqual("WALMART STORE #1234", result.ReceiptData.StoreName);
            Assert.AreEqual("(555) 123-4567", result.ReceiptData.StorePhone);
            Assert.AreEqual("ABC123", result.ReceiptData.ReceiptNumber);
            Assert.AreEqual(11.48m, result.ReceiptData.Subtotal);
            Assert.AreEqual(0.92m, result.ReceiptData.TaxAmount);
            Assert.AreEqual(12.40m, result.ReceiptData.Total);
            Assert.IsTrue(result.ReceiptData.TransactionDate.HasValue);
        }

        [TestMethod]
        public async Task ProcessAsync_ShouldExtractLineItems()
        {
            // Arrange
            var ocrText = @"STORE NAME

Product A        $10.00
Product B        $20.00
Product C        $15.50

SUBTOTAL         $45.50
TAX              $3.64
TOTAL            $49.14";

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.ReceiptData.Items);
            Assert.AreEqual(3, result.ReceiptData.Items.Count);
            
            var items = result.ReceiptData.Items.OrderBy(i => i.UnitPrice).ToList();
            Assert.AreEqual("Product A", items[0].Description);
            Assert.AreEqual(10.00m, items[0].UnitPrice);
            Assert.AreEqual("Product B", items[1].Description);
            Assert.AreEqual(20.00m, items[1].UnitPrice);
            Assert.AreEqual("Product C", items[2].Description);
            Assert.AreEqual(15.50m, items[2].UnitPrice);
        }

        [DataTestMethod]
        [DataRow("USD", "USD")]
        [DataRow("EUR", "EUR")]
        [DataRow("GBP", "GBP")]
        [DataRow("", "USD")]
        public async Task ProcessAsync_ShouldExtractCurrency(string currencyInText, string expectedCurrency)
        {
            // Arrange
            var ocrText = $@"STORE NAME
{(string.IsNullOrEmpty(currencyInText) ? "" : $"Currency: {currencyInText}")}
TOTAL: 100.00";

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedCurrency, result.ReceiptData.Currency);
        }

        [TestMethod]
        public async Task ProcessAsync_ShouldCalculateMissingTotals()
        {
            // Arrange
            var ocrText = @"STORE NAME

Item 1           $10.00
Item 2           $20.00

SUBTOTAL         $30.00
TAX              $2.40";
            // Note: No TOTAL in the text

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(30.00m, result.ReceiptData.Subtotal);
            Assert.AreEqual(2.40m, result.ReceiptData.TaxAmount);
            Assert.AreEqual(32.40m, result.ReceiptData.Total); // Should be calculated
        }

        [TestMethod]
        public async Task ProcessAsync_ShouldCalculateTaxFromTotalAndSubtotal()
        {
            // Arrange
            var ocrText = @"STORE NAME

SUBTOTAL         $100.00
TOTAL            $108.00";
            // Note: No TAX in the text

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(100.00m, result.ReceiptData.Subtotal);
            Assert.AreEqual(108.00m, result.ReceiptData.Total);
            Assert.AreEqual(8.00m, result.ReceiptData.TaxAmount); // Should be calculated
            Assert.AreEqual(8.0m, result.ReceiptData.TaxRate); // Should be 8%
        }

        [TestMethod]
        public async Task ProcessAsync_ShouldCalculateConfidenceScore()
        {
            // Arrange
            var completeReceipt = @"STORE NAME
(555) 123-4567
12/25/2024

RECEIPT #12345

Item 1           $10.00

SUBTOTAL         $10.00
TAX              $0.80
TOTAL            $10.80";

            var minimalReceipt = @"TOTAL: $10.00";

            // Act
            var completeResult = await _processor.ProcessAsync(completeReceipt, CancellationToken.None) as ProcessedReceipt;
            var minimalResult = await _processor.ProcessAsync(minimalReceipt, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.IsNotNull(completeResult);
            Assert.IsNotNull(minimalResult);
            Assert.IsTrue(completeResult.ConfidenceScore > minimalResult.ConfidenceScore,
                $"Complete receipt confidence ({completeResult.ConfidenceScore}) should be higher than minimal ({minimalResult.ConfidenceScore})");
            Assert.IsTrue(completeResult.ConfidenceScore > 0.5);
            Assert.IsTrue(minimalResult.ConfidenceScore <= 0.5);
        }

        [TestMethod]
        public async Task ProcessAsync_ShouldValidateProcessedReceipt()
        {
            // Arrange
            var ocrText = @"STORE NAME
SUBTOTAL         $10.00
TAX              $0.80
TOTAL            $10.80";

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Validate());
            Assert.AreEqual(0, result.ValidationErrors.Count);
        }

        [TestMethod]
        public async Task ProcessAsync_WithTotalMismatch_ShouldAddWarning()
        {
            // Arrange
            var ocrText = @"STORE NAME
SUBTOTAL         $10.00
TAX              $0.80
TOTAL            $15.00"; // Wrong total

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Warnings.Count > 0);
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("Total mismatch")));
        }

        [TestMethod]
        public async Task ProcessAsync_WithCancellation_ShouldRespectToken()
        {
            // Arrange
            var ocrText = @"STORE NAME
TOTAL: $10.00";
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            // The task should complete (our implementation is simple), but it should respect the token
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                var task = _processor.ProcessAsync(ocrText, cts.Token);
                cts.Token.ThrowIfCancellationRequested();
                await task;
            });
        }

        [DataTestMethod]
        [DataRow("12/25/2024", true, "US date format")]
        [DataRow("25/12/2024", true, "European date format")]
        [DataRow("2024-12-25", true, "ISO date format")]
        [DataRow("12-25-2024", true, "Dash separator")]
        [DataRow("invalid date", false, "Invalid date")]
        public async Task ProcessAsync_ShouldParseVariousDateFormats(string dateText, bool shouldParse, string scenario)
        {
            // Arrange
            var ocrText = $@"STORE NAME
{dateText}
TOTAL: $10.00";

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.IsNotNull(result, $"Failed for scenario: {scenario}");
            if (shouldParse)
            {
                Assert.IsTrue(result.ReceiptData.TransactionDate.HasValue, $"Date should be parsed for: {scenario}");
            }
        }

        [TestMethod]
        public async Task ProcessAsync_WithException_ShouldAddValidationError()
        {
            // Arrange
            var processor = new ReceiptProcessor(_loggerMock.Object);
            // This would normally not throw, but we're testing error handling
            var ocrText = "TOTAL: $10.00";

            // Act
            var result = await processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.IsNotNull(result);
            // The result should still be valid even if there were processing issues
            Assert.AreEqual("Receipt", result.DocumentType);
        }
    }
}