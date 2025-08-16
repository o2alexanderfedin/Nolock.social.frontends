using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Unit tests for OCRServiceWithCache decorator.
    /// </summary>
    public class OCRServiceWithCacheTests
    {
        private readonly Mock<IOCRService> _mockInnerService;
        private readonly Mock<IOCRResultCache> _mockCache;
        private readonly Mock<ILogger<OCRServiceWithCache>> _mockLogger;
        private readonly OCRServiceWithCache _serviceWithCache;

        public OCRServiceWithCacheTests()
        {
            _mockInnerService = new Mock<IOCRService>();
            _mockCache = new Mock<IOCRResultCache>();
            _mockLogger = new Mock<ILogger<OCRServiceWithCache>>();
            _serviceWithCache = new OCRServiceWithCache(
                _mockInnerService.Object,
                _mockCache.Object,
                _mockLogger.Object,
                cacheOnlyCompleteResults: true,
                cacheExpirationMinutes: 60);
        }

        [Fact]
        public async Task SubmitDocumentAsync_CacheHit_ReturnsCachedResult()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                DocumentType = "Receipt",
                ClientRequestId = "test-123"
            };

            var cachedResponse = new OCRStatusResponse
            {
                TrackingId = "CACHED-123",
                Status = OCRProcessingStatus.Complete,
                SubmittedAt = DateTime.UtcNow.AddMinutes(-5),
                CompletedAt = DateTime.UtcNow.AddMinutes(-3),
                ResultData = new OCRResultData
                {
                    ExtractedText = "Cached text",
                    ConfidenceScore = 95.0
                }
            };

            _mockCache.Setup(c => c.GetResultAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(cachedResponse);

            // Act
            var result = await _serviceWithCache.SubmitDocumentAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("CACHED-123", result.TrackingId);
            Assert.Equal(OCRProcessingStatus.Complete, result.Status);
            _mockInnerService.Verify(s => s.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SubmitDocumentAsync_CacheMiss_CallsInnerService()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                DocumentType = "Receipt",
                ClientRequestId = "test-123"
            };

            var serviceResponse = new OCRSubmissionResponse
            {
                TrackingId = "NEW-123",
                Status = OCRProcessingStatus.Queued,
                SubmittedAt = DateTime.UtcNow,
                EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(2)
            };

            _mockCache.Setup(c => c.GetResultAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((OCRStatusResponse?)null);
            _mockInnerService.Setup(s => s.SubmitDocumentAsync(request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(serviceResponse);

            // Act
            var result = await _serviceWithCache.SubmitDocumentAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("NEW-123", result.TrackingId);
            Assert.Equal(OCRProcessingStatus.Queued, result.Status);
            _mockInnerService.Verify(s => s.SubmitDocumentAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_CompleteResult_CachesResult()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                DocumentType = "Receipt",
                ClientRequestId = "test-123"
            };

            var serviceResponse = new OCRSubmissionResponse
            {
                TrackingId = "NEW-123",
                Status = OCRProcessingStatus.Complete,
                SubmittedAt = DateTime.UtcNow,
                EstimatedCompletionTime = DateTime.UtcNow
            };

            _mockCache.Setup(c => c.GetResultAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((OCRStatusResponse?)null);
            _mockInnerService.Setup(s => s.SubmitDocumentAsync(request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(serviceResponse);
            _mockCache.Setup(c => c.StoreResultAsync(
                    It.IsAny<byte[]>(),
                    It.IsAny<OCRStatusResponse>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("cache-key-123");

            // Act
            var result = await _serviceWithCache.SubmitDocumentAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(OCRProcessingStatus.Complete, result.Status);
            _mockCache.Verify(c => c.StoreResultAsync(
                It.IsAny<byte[]>(),
                It.IsAny<OCRStatusResponse>(),
                60,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(OCRProcessingStatus.Queued, false)]
        [InlineData(OCRProcessingStatus.Processing, false)]
        [InlineData(OCRProcessingStatus.Complete, true)]
        [InlineData(OCRProcessingStatus.Failed, false)]
        public async Task SubmitDocumentAsync_CacheOnlyComplete_CachesOnlyWhenComplete(
            OCRProcessingStatus status,
            bool shouldCache)
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                DocumentType = "Receipt",
                ClientRequestId = "test-123"
            };

            var serviceResponse = new OCRSubmissionResponse
            {
                TrackingId = "NEW-123",
                Status = status,
                SubmittedAt = DateTime.UtcNow,
                EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(2)
            };

            _mockCache.Setup(c => c.GetResultAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((OCRStatusResponse?)null);
            _mockInnerService.Setup(s => s.SubmitDocumentAsync(request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(serviceResponse);

            // Act
            await _serviceWithCache.SubmitDocumentAsync(request);

            // Assert
            var expectedTimes = shouldCache ? Times.Once() : Times.Never();
            _mockCache.Verify(c => c.StoreResultAsync(
                It.IsAny<byte[]>(),
                It.IsAny<OCRStatusResponse>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()), expectedTimes);
        }

        [Fact]
        public async Task GetStatusAsync_PassesThrough_ToInnerService()
        {
            // Arrange
            var trackingId = "TEST-123";
            var statusResponse = new OCRStatusResponse
            {
                TrackingId = trackingId,
                Status = OCRProcessingStatus.Processing,
                ProgressPercentage = 50,
                SubmittedAt = DateTime.UtcNow.AddMinutes(-1)
            };

            _mockInnerService.Setup(s => s.GetStatusAsync(trackingId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(statusResponse);

            // Act
            var result = await _serviceWithCache.GetStatusAsync(trackingId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(trackingId, result.TrackingId);
            Assert.Equal(OCRProcessingStatus.Processing, result.Status);
            Assert.Equal(50, result.ProgressPercentage);
            _mockInnerService.Verify(s => s.GetStatusAsync(trackingId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SubmitDocumentAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _serviceWithCache.SubmitDocumentAsync(null!));
        }

        [Fact]
        public async Task GetStatusAsync_NullTrackingId_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _serviceWithCache.GetStatusAsync(null!));
        }

        [Fact]
        public async Task SubmitDocumentAsync_CachingFailure_ContinuesWithoutCache()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                DocumentType = "Receipt",
                ClientRequestId = "test-123"
            };

            var serviceResponse = new OCRSubmissionResponse
            {
                TrackingId = "NEW-123",
                Status = OCRProcessingStatus.Complete,
                SubmittedAt = DateTime.UtcNow
            };

            _mockCache.Setup(c => c.GetResultAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Cache read error"));
            _mockInnerService.Setup(s => s.SubmitDocumentAsync(request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(serviceResponse);

            // Act
            var result = await _serviceWithCache.SubmitDocumentAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("NEW-123", result.TrackingId);
            _mockInnerService.Verify(s => s.SubmitDocumentAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("data:image/png;base64,iVBORw0KGgo=", "iVBORw0KGgo=")]
        [InlineData("iVBORw0KGgo=", "iVBORw0KGgo=")]
        [InlineData("data:image/jpeg;base64,/9j/4AAQ", "/9j/4AAQ")]
        public async Task SubmitDocumentAsync_HandlesDataUrlPrefix_Correctly(
            string imageData,
            string expectedBase64)
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ImageData = imageData,
                DocumentType = "Receipt",
                ClientRequestId = "test-123"
            };

            _mockCache.Setup(c => c.GetResultAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((OCRStatusResponse?)null);
            _mockInnerService.Setup(s => s.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OCRSubmissionResponse
                {
                    TrackingId = "NEW-123",
                    Status = OCRProcessingStatus.Queued
                });

            // Act
            await _serviceWithCache.SubmitDocumentAsync(request);

            // Assert
            _mockCache.Verify(c => c.GetResultAsync(
                It.Is<byte[]>(bytes => Convert.ToBase64String(bytes) == expectedBase64 || 
                                      System.Text.Encoding.UTF8.GetString(bytes) == imageData),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}