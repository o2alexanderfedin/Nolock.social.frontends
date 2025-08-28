using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.OCR.Generated;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    public class CheckOCRServiceTests
    {
        private readonly Mock<IMistralOCRClient> _mockOcrClient;
        private readonly Mock<ILogger<CheckOCRService>> _mockLogger;
        private readonly CheckOCRService _service;

        public CheckOCRServiceTests()
        {
            _mockOcrClient = new Mock<IMistralOCRClient>();
            _mockLogger = new Mock<ILogger<CheckOCRService>>();
            _service = new CheckOCRService(_mockOcrClient.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_NullOcrClient_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new CheckOCRService(null!, _mockLogger.Object));
            Assert.Equal("ocrClient", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new CheckOCRService(_mockOcrClient.Object, null!));
            Assert.Equal("logger", ex.ParamName);
        }

        [Theory]
        [InlineData(null, "request")]
        public async Task SubmitDocumentAsync_NullRequest_ThrowsArgumentNullException(
            OCRSubmissionRequest request, string expectedParamName)
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.SubmitDocumentAsync(request));
            Assert.Equal(expectedParamName, ex.ParamName);
        }

        [Theory]
        [InlineData(null, "Image data cannot be empty")]
        [InlineData(new byte[0], "Image data cannot be empty")]
        public async Task SubmitDocumentAsync_InvalidImageData_ThrowsArgumentException(
            byte[] imageData, string expectedMessage)
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = imageData,
                DocumentType = DocumentType.Check
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.SubmitDocumentAsync(request));
            Assert.Contains(expectedMessage, ex.Message);
            Assert.Equal("request", ex.ParamName);
        }

        [Fact]
        public async Task SubmitDocumentAsync_WrongDocumentType_ThrowsArgumentException()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3 },
                DocumentType = DocumentType.Receipt
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.SubmitDocumentAsync(request));
            Assert.Contains("CheckOCRService can only process Check documents", ex.Message);
            Assert.Equal("request", ex.ParamName);
        }

        [Fact]
        public async Task SubmitDocumentAsync_SuccessfulProcessing_LogsCheckDetails()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3, 4, 5 },
                DocumentType = DocumentType.Check
            };

            var checkData = new Check
            {
                CheckNumber = "12345",
                Amount = 150.50,
                Payee = "John Doe",
                Payer = "Jane Smith",
                Date = DateTime.Now
            };

            var response = new CheckModelOcrResponse
            {
                ModelData = checkData,
                ProcessingTime = "1.5s",
                Error = null
            };

            _mockOcrClient
                .Setup(x => x.ProcessCheckOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            _mockOcrClient.Verify(x => x.ProcessCheckOcrAsync(
                It.Is<FileParameter>(fp => fp != null), 
                It.IsAny<CancellationToken>()), Times.Once);

            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing check document")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("12345") && v.ToString()!.Contains("150.5")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_ErrorInResponse_LogsError()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3, 4, 5 },
                DocumentType = DocumentType.Check
            };

            var response = new CheckModelOcrResponse
            {
                ModelData = null,
                ProcessingTime = "1.5s",
                Error = "Failed to process image"
            };

            _mockOcrClient
                .Setup(x => x.ProcessCheckOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to process image")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_EmptyResponse_LogsWarning()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3, 4, 5 },
                DocumentType = DocumentType.Check
            };

            var response = new CheckModelOcrResponse
            {
                ModelData = null,
                ProcessingTime = null,
                Error = null
            };

            _mockOcrClient
                .Setup(x => x.ProcessCheckOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("empty result")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [MemberData(nameof(GetExceptionTestData))]
        public async Task SubmitDocumentAsync_HandlesExceptions(
            Exception thrownException,
            System.Type expectedExceptionType,
            string expectedMessagePart,
            LogLevel expectedLogLevel)
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3, 4, 5 },
                DocumentType = DocumentType.Check
            };

            _mockOcrClient
                .Setup(x => x.ProcessCheckOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(thrownException);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.SubmitDocumentAsync(request));
            
            Assert.Contains(expectedMessagePart, ex.Message);
            Assert.IsType(expectedExceptionType, ex.InnerException);

            _mockLogger.Verify(x => x.Log(
                expectedLogLevel,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task SubmitDocumentAsync_WithCancellation_PropagatesCancellationToken()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3, 4, 5 },
                DocumentType = DocumentType.Check
            };

            var cancellationToken = new CancellationToken(true);

            _mockOcrClient
                .Setup(x => x.ProcessCheckOcrAsync(It.IsAny<FileParameter>(), cancellationToken))
                .ThrowsAsync(new TaskCanceledException());

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.SubmitDocumentAsync(request, cancellationToken));

            Assert.Contains("cancelled or timed out", ex.Message);
            Assert.IsType<TaskCanceledException>(ex.InnerException);
        }

        [Fact]
        public async Task SubmitDocumentAsync_CheckModelWithNullFields_HandlesGracefully()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = new byte[] { 1, 2, 3, 4, 5 },
                DocumentType = DocumentType.Check
            };

            var checkData = new Check
            {
                CheckNumber = null,
                Amount = null,
                Payee = null,
                Payer = null,
                Date = null
            };

            var response = new CheckModelOcrResponse
            {
                ModelData = checkData,
                ProcessingTime = "1.5s",
                Error = null
            };

            _mockOcrClient
                .Setup(x => x.ProcessCheckOcrAsync(It.IsAny<FileParameter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert - Should log with "Unknown" and 0 for null values
            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unknown") && v.ToString()!.Contains("0")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        public static IEnumerable<object[]> GetExceptionTestData()
        {
            var headers = new Dictionary<string, IEnumerable<string>>();
            
            yield return new object[]
            {
                new MistralOCRException("API Error", 500, "Server Error", headers, null),
                typeof(MistralOCRException),
                "OCR API error",
                LogLevel.Error
            };
            
            yield return new object[]
            {
                new HttpRequestException("Network failure"),
                typeof(HttpRequestException),
                "Network error occurred",
                LogLevel.Error
            };
            
            yield return new object[]
            {
                new TaskCanceledException("Timeout"),
                typeof(TaskCanceledException),
                "cancelled or timed out",
                LogLevel.Warning
            };
            
            yield return new object[]
            {
                new InvalidOperationException("Unexpected error"),
                typeof(InvalidOperationException),
                "unexpected error occurred",
                LogLevel.Error
            };
        }
    }
}