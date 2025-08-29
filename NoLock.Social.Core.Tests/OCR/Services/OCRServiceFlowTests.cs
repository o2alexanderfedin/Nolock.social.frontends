using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.OCR.Generated;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    public class OCRServiceFlowTests
    {
        private readonly Mock<ILogger<ReceiptOCRService>> _mockLogger;
        private readonly Mock<Func<byte[], CancellationToken, Task<TestOcrResponse>>> _mockInvokeEndpoint;
        private readonly OCRServiceFlow<TestOcrResponse> _service;

        public OCRServiceFlowTests()
        {
            _mockLogger = new Mock<ILogger<ReceiptOCRService>>();
            _mockInvokeEndpoint = new Mock<Func<byte[], CancellationToken, Task<TestOcrResponse>>>();
            _service = new OCRServiceFlow<TestOcrResponse>(_mockInvokeEndpoint.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullInvokeEndpoint_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new OCRServiceFlow<TestOcrResponse>(null!, _mockLogger.Object));
            Assert.Equal("invokeOcrEndpoint", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new OCRServiceFlow<TestOcrResponse>(_mockInvokeEndpoint.Object, null!));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange & Act
            var service = new OCRServiceFlow<TestOcrResponse>(_mockInvokeEndpoint.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(service);
        }

        #endregion

        #region Request Validation Tests

        [Fact]
        public async Task SubmitDocumentAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                () => _service.SubmitDocumentAsync(null!));
            Assert.Contains("request", ex.Message);
        }

        [Theory]
        [MemberData(nameof(InvalidImageDataTestCases))]
        public async Task SubmitDocumentAsync_InvalidImageData_ThrowsArgumentException(
            byte[] imageData, string expectedMessage)
        {
            // Arrange
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData!, 
                DocumentType = DocumentType.Receipt 
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.SubmitDocumentAsync(request));
            Assert.Contains(expectedMessage, ex.Message);
            Assert.Equal("request", ex.ParamName);
        }

        public static IEnumerable<object[]> InvalidImageDataTestCases()
        {
            yield return new object[] { null!, "Image data cannot be empty" };
            yield return new object[] { new byte[0], "Image data cannot be empty" };
        }

        #endregion

        #region Successful Processing Tests

        [Theory]
        [InlineData(DocumentType.Receipt)]
        [InlineData(DocumentType.Check)]
        public async Task SubmitDocumentAsync_SuccessfulResponse_ProcessesCorrectly(DocumentType documentType)
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = documentType 
            };
            var expectedResponse = new TestOcrResponse { IsSuccess = true };

            _mockInvokeEndpoint.Setup(x => x(imageData, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            _mockInvokeEndpoint.Verify(x => x(imageData, It.IsAny<CancellationToken>()), Times.Once);
            VerifyLogMessage(LogLevel.Information, $"Processing document {documentType}");
            VerifyLogMessage(LogLevel.Debug, "Processing image");
            VerifyLogMessage(LogLevel.Information, "OCR processing completed for image:");
            VerifyLogMessage(LogLevel.Debug, "OCR processing completed successfully");
        }

        [Fact]
        public async Task SubmitDocumentAsync_LargeImageData_ProcessesSuccessfully()
        {
            // Arrange
            var largeImageData = new byte[1024 * 1024]; // 1MB
            new Random().NextBytes(largeImageData);
            var request = new OCRSubmissionRequest 
            { 
                ImageData = largeImageData, 
                DocumentType = DocumentType.Receipt 
            };
            var expectedResponse = new TestOcrResponse { IsSuccess = true };

            _mockInvokeEndpoint.Setup(x => x(largeImageData, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            _mockInvokeEndpoint.Verify(x => x(largeImageData, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Error Response Tests

        [Theory]
        [InlineData("OCR processing failed", DocumentType.Receipt)]
        [InlineData("Invalid document format", DocumentType.Check)]
        [InlineData("API quota exceeded", DocumentType.Receipt)]
        public async Task SubmitDocumentAsync_OcrResponseWithError_LogsError(
            string errorMessage, DocumentType documentType)
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = documentType 
            };
            var expectedResponse = new TestOcrResponse { Error = errorMessage };

            _mockInvokeEndpoint.Setup(x => x(imageData, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            VerifyLogMessage(LogLevel.Error, $"OCR processing failed for receipt. Error: {errorMessage}");
        }

        [Fact]
        public async Task SubmitDocumentAsync_OcrResponseNotSuccessful_LogsWarning()
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = DocumentType.Receipt 
            };
            var expectedResponse = new TestOcrResponse { IsSuccess = false };

            _mockInvokeEndpoint.Setup(x => x(imageData, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            VerifyLogMessage(LogLevel.Warning, "Received empty result from OCR API for receipt");
        }

        [Fact]
        public async Task SubmitDocumentAsync_NullOcrResponse_LogsWarning()
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = DocumentType.Receipt 
            };

            _mockInvokeEndpoint.Setup(x => x(imageData, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TestOcrResponse)null!);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            VerifyLogMessage(LogLevel.Warning, "Received empty result from OCR API for receipt");
        }

        #endregion

        #region Exception Handling Tests

        [Theory]
        [MemberData(nameof(ExceptionTestCases))]
        public async Task SubmitDocumentAsync_ExceptionHandling_ThrowsInvalidOperationException(
            System.Type exceptionType, string expectedMessage, int statusCode, string response)
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = DocumentType.Receipt 
            };

            Exception innerException = CreateException(exceptionType, statusCode, response);

            _mockInvokeEndpoint.Setup(x => x(imageData, It.IsAny<CancellationToken>()))
                .ThrowsAsync(innerException);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.SubmitDocumentAsync(request));
            
            // Verify exception message
            if (exceptionType == typeof(MistralOCRException))
            {
                Assert.Equal($"OCR API error: {innerException.Message}", ex.Message);
            }
            else
            {
                Assert.Contains(expectedMessage, ex.Message);
            }
            Assert.Equal(innerException, ex.InnerException);

            // Verify appropriate logging
            VerifyExceptionLogging(exceptionType, statusCode, response);
        }

        public static IEnumerable<object[]> ExceptionTestCases()
        {
            yield return new object[] { typeof(MistralOCRException), "OCR API error", 400, "Bad Request" };
            yield return new object[] { typeof(MistralOCRException), "OCR API error", 401, "Unauthorized" };
            yield return new object[] { typeof(MistralOCRException), "OCR API error", 429, "Too Many Requests" };
            yield return new object[] { typeof(MistralOCRException), "OCR API error", 500, "Internal Server Error" };
            yield return new object[] { typeof(HttpRequestException), "Network error occurred while submitting document", 0, null! };
            yield return new object[] { typeof(TaskCanceledException), "OCR submission was cancelled or timed out", 0, null! };
            yield return new object[] { typeof(InvalidOperationException), "An unexpected error occurred during OCR submission", 0, null! };
            yield return new object[] { typeof(ArgumentException), "An unexpected error occurred during OCR submission", 0, null! };
        }

        private static Exception CreateException(System.Type exceptionType, int statusCode, string response)
        {
            return exceptionType.Name switch
            {
                nameof(MistralOCRException) => new MistralOCRException("API Error", statusCode, response, 
                    new Dictionary<string, IEnumerable<string>>(), null),
                nameof(HttpRequestException) => new HttpRequestException("Network error"),
                nameof(TaskCanceledException) => new TaskCanceledException("Operation cancelled"),
                nameof(ArgumentException) => new ArgumentException("Invalid argument"),
                _ => new InvalidOperationException("Unexpected error")
            };
        }

        private void VerifyExceptionLogging(System.Type exceptionType, int statusCode, string response)
        {
            if (exceptionType == typeof(MistralOCRException))
            {
                VerifyLogMessage(LogLevel.Error, $"Mistral OCR API error. Status: {statusCode}, Response: {response}");
            }
            else if (exceptionType == typeof(HttpRequestException))
            {
                VerifyLogMessage(LogLevel.Error, "Network error calling OCR API");
            }
            else if (exceptionType == typeof(TaskCanceledException))
            {
                VerifyLogMessage(LogLevel.Warning, "OCR submission cancelled or timed out");
            }
            else
            {
                VerifyLogMessage(LogLevel.Error, "Unexpected error during OCR submission");
            }
        }

        #endregion

        #region Cancellation Token Tests

        [Fact]
        public async Task SubmitDocumentAsync_CancellationTokenPropagated_PassesToEndpoint()
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = DocumentType.Receipt 
            };
            using var cts = new CancellationTokenSource();
            var expectedResponse = new TestOcrResponse { IsSuccess = true };

            _mockInvokeEndpoint.Setup(x => x(imageData, cts.Token))
                .ReturnsAsync(expectedResponse);

            // Act
            await _service.SubmitDocumentAsync(request, cts.Token);

            // Assert
            _mockInvokeEndpoint.Verify(x => x(imageData, cts.Token), Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_CancelledToken_ThrowsTaskCanceledException()
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = DocumentType.Receipt 
            };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockInvokeEndpoint.Setup(x => x(imageData, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TaskCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.SubmitDocumentAsync(request, cts.Token));
        }

        #endregion

        #region Concurrent Execution Tests

        [Fact]
        public async Task SubmitDocumentAsync_ConcurrentRequests_HandledIndependently()
        {
            // Arrange
            var imageData1 = new byte[] { 1, 2, 3, 4 };
            var imageData2 = new byte[] { 5, 6, 7, 8 };
            var request1 = new OCRSubmissionRequest { ImageData = imageData1, DocumentType = DocumentType.Receipt };
            var request2 = new OCRSubmissionRequest { ImageData = imageData2, DocumentType = DocumentType.Check };

            var response1 = new TestOcrResponse { IsSuccess = true };
            var response2 = new TestOcrResponse { IsSuccess = true };

            _mockInvokeEndpoint.Setup(x => x(imageData1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(response1);
            _mockInvokeEndpoint.Setup(x => x(imageData2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(response2);

            // Act
            var task1 = _service.SubmitDocumentAsync(request1);
            var task2 = _service.SubmitDocumentAsync(request2);
            await Task.WhenAll(task1, task2);

            // Assert
            _mockInvokeEndpoint.Verify(x => x(imageData1, It.IsAny<CancellationToken>()), Times.Once);
            _mockInvokeEndpoint.Verify(x => x(imageData2, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Logging Verification Tests

        [Theory]
        [InlineData(LogLevel.Debug, "Processing image")]
        [InlineData(LogLevel.Debug, "OCR processing completed successfully")]
        public async Task SubmitDocumentAsync_SuccessfulFlow_LogsDebugMessages(LogLevel level, string message)
        {
            // Arrange
            var imageData = new byte[] { 1, 2, 3, 4 };
            var request = new OCRSubmissionRequest 
            { 
                ImageData = imageData, 
                DocumentType = DocumentType.Receipt 
            };
            var expectedResponse = new TestOcrResponse { IsSuccess = true };

            _mockInvokeEndpoint.Setup(x => x(imageData, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _service.SubmitDocumentAsync(request);

            // Assert
            VerifyLogMessage(level, message);
        }

        #endregion

        #region Helper Methods

        private void VerifyLogMessage(LogLevel level, string message)
        {
            _mockLogger.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyNoLogMessage(LogLevel level, string message)
        {
            _mockLogger.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        #endregion

        #region Test Models

        public class TestOcrResponse : IModelOcrResponse
        {
            public bool IsSuccess { get; set; }
            public string? Error { get; set; }
        }

        #endregion
    }
}