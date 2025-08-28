using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.OCR.Generated;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    public class ReceiptOCRServiceTests
    {
        private readonly Mock<IMistralOCRClient> _mockOcrClient;
        private readonly Mock<ILogger<ReceiptOCRService>> _mockLogger;
        private readonly ReceiptOCRService _service;

        public ReceiptOCRServiceTests()
        {
            _mockOcrClient = new Mock<IMistralOCRClient>();
            _mockLogger = new Mock<ILogger<ReceiptOCRService>>();
            _service = new ReceiptOCRService(_mockOcrClient.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_NullOcrClient_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new ReceiptOCRService(null!, _mockLogger.Object));
            Assert.Equal("ocrClient", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new ReceiptOCRService(_mockOcrClient.Object, null!));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public async Task SubmitDocumentAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await _service.SubmitDocumentAsync(null!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new byte[0])]
        public async Task SubmitDocumentAsync_InvalidImageData_ThrowsArgumentException(byte[] imageData)
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = imageData,
                DocumentType = DocumentType.Receipt
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                async () => await _service.SubmitDocumentAsync(request));
            Assert.Equal("request", ex.ParamName);
            Assert.Contains("Image data cannot be empty", ex.Message);
        }

        [Fact]
        public async Task SubmitDocumentAsync_WrongDocumentType_ThrowsArgumentException()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3 },
                DocumentType = DocumentType.Check  // Receipt service should reject Check documents
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                async () => await _service.SubmitDocumentAsync(request));
            Assert.Equal("request", ex.ParamName);
            Assert.Contains($"ReceiptOCRService can only process Receipt documents, got {DocumentType.Check}", ex.Message);
        }

        [Fact]
        public async Task SubmitDocumentAsync_SuccessfulProcessing_CallsApiAndLogsInformation()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3, 4, 5 },
                DocumentType = DocumentType.Receipt
            };

            var receiptData = new Receipt
            {
                Merchant = new MerchantInfo { Name = "Test Store" },
                Totals = new ReceiptTotals { Total = 123.45 }
            };

            var expectedResponse = new ReceiptModelOcrResponse
            {
                ModelData = receiptData,
                ProcessingTime = "1.5s"
            };

            _mockOcrClient
                .Setup(x => x.ProcessReceiptOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            _mockOcrClient.Verify(x => x.ProcessReceiptOcrAsync(
                It.IsAny<FileParameter>(), 
                It.IsAny<CancellationToken>()), 
                Times.Once);

            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing receipt document")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Test Store") && v.ToString()!.Contains("123.45")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_ResponseWithError_LogsError()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3 },
                DocumentType = DocumentType.Receipt
            };

            var expectedResponse = new ReceiptModelOcrResponse
            {
                Error = "OCR processing failed"
            };

            _mockOcrClient
                .Setup(x => x.ProcessReceiptOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("OCR processing failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_EmptyResponse_LogsWarning()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3 },
                DocumentType = DocumentType.Receipt
            };

            var expectedResponse = new ReceiptModelOcrResponse
            {
                ModelData = null,
                Error = null
            };

            _mockOcrClient
                .Setup(x => x.ProcessReceiptOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Received empty result from OCR API")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_MistralOCRException_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3 },
                DocumentType = DocumentType.Receipt
            };

            var mistralException = new MistralOCRException("API Error", 500, null, null, null);

            _mockOcrClient
                .Setup(x => x.ProcessReceiptOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(mistralException);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.SubmitDocumentAsync(request));

            Assert.Contains("OCR API error", ex.Message);
            Assert.Equal(mistralException, ex.InnerException);

            _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Mistral OCR API error")),
                It.Is<Exception>(e => e == mistralException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_HttpRequestException_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3 },
                DocumentType = DocumentType.Receipt
            };

            var httpException = new HttpRequestException("Network error");

            _mockOcrClient
                .Setup(x => x.ProcessReceiptOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(httpException);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.SubmitDocumentAsync(request));

            Assert.Contains("Network error occurred while submitting document", ex.Message);
            Assert.Equal(httpException, ex.InnerException);

            _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Network error calling OCR API")),
                It.Is<Exception>(e => e == httpException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_TaskCanceledException_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3 },
                DocumentType = DocumentType.Receipt
            };

            var canceledException = new TaskCanceledException("Timeout");

            _mockOcrClient
                .Setup(x => x.ProcessReceiptOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(canceledException);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.SubmitDocumentAsync(request));

            Assert.Contains("OCR submission was cancelled or timed out", ex.Message);
            Assert.Equal(canceledException, ex.InnerException);

            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("OCR submission cancelled or timed out")),
                It.Is<Exception>(e => e == canceledException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_UnexpectedException_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3 },
                DocumentType = DocumentType.Receipt
            };

            var unexpectedException = new InvalidOperationException("Unexpected error");

            _mockOcrClient
                .Setup(x => x.ProcessReceiptOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(unexpectedException);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.SubmitDocumentAsync(request));

            Assert.Contains("An unexpected error occurred during OCR submission", ex.Message);
            Assert.Equal(unexpectedException, ex.InnerException);

            _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error during OCR submission")),
                It.Is<Exception>(e => e == unexpectedException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_CancellationToken_PropagatesToken()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3 },
                DocumentType = DocumentType.Receipt
            };

            var cts = new CancellationTokenSource();
            var token = cts.Token;

            _mockOcrClient
                .Setup(x => x.ProcessReceiptOcrAsync(It.IsAny<FileParameter>(), token))
                .ReturnsAsync(new ReceiptModelOcrResponse { ModelData = new Receipt() });

            // Act
            await _service.SubmitDocumentAsync(request, token);

            // Assert
            _mockOcrClient.Verify(x => x.ProcessReceiptOcrAsync(
                It.IsAny<FileParameter>(), 
                token), 
                Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_FileParameterCreation_UsesCorrectMetadata()
        {
            // Arrange
            var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header
            var request = new OCRSubmissionRequest
            {
                ImageData = imageData,
                DocumentType = DocumentType.Receipt
            };

            FileParameter? capturedFileParam = null;
            _mockOcrClient
                .Setup(x => x.ProcessReceiptOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .Callback<FileParameter, CancellationToken>((fp, ct) => capturedFileParam = fp)
                .ReturnsAsync(new ReceiptModelOcrResponse { ModelData = new Receipt() });

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            Assert.NotNull(capturedFileParam);
            Assert.Equal("document.jpg", capturedFileParam.FileName);
            Assert.Equal("image/jpeg", capturedFileParam.ContentType);
            // Note: Cannot validate stream data as FileParameter disposes the stream
        }

        [Fact]
        public async Task SubmitDocumentAsync_SuccessWithNullMerchant_HandlesGracefully()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3 },
                DocumentType = DocumentType.Receipt
            };

            var receiptData = new Receipt
            {
                Merchant = null,
                Totals = new ReceiptTotals { Total = 50.00 }
            };

            var expectedResponse = new ReceiptModelOcrResponse
            {
                ModelData = receiptData,
                ProcessingTime = "1.0s"
            };

            _mockOcrClient
                .Setup(x => x.ProcessReceiptOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unknown") && v.ToString()!.Contains("50")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_SuccessWithNullTotal_HandlesGracefully()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3 },
                DocumentType = DocumentType.Receipt
            };

            var receiptData = new Receipt
            {
                Merchant = new MerchantInfo { Name = "Test Store" },
                Totals = new ReceiptTotals { Total = null }
            };

            var expectedResponse = new ReceiptModelOcrResponse
            {
                ModelData = receiptData,
                ProcessingTime = "1.0s"
            };

            _mockOcrClient
                .Setup(x => x.ProcessReceiptOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Test Store") && v.ToString()!.Contains("0")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}