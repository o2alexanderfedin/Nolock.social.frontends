using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services;
using NoLock.Social.Core.Storage.Interfaces;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Unit tests for OCRRetryQueueProcessor with comprehensive coverage.
    /// </summary>
    public class OCRRetryQueueProcessorTests : IDisposable
    {
        private readonly Mock<IOCRService> _mockOCRService;
        private readonly Mock<IFailedRequestStore> _mockFailedRequestStore;
        private readonly Mock<IConnectivityService> _mockConnectivityService;
        private readonly Mock<IRetryPolicy> _mockRetryPolicy;
        private readonly Mock<IFailureClassifier> _mockFailureClassifier;
        private readonly Mock<ILogger<OCRRetryQueueProcessor>> _mockLogger;
        private readonly OCRRetryQueueProcessor _processor;

        public OCRRetryQueueProcessorTests()
        {
            _mockOCRService = new Mock<IOCRService>();
            _mockFailedRequestStore = new Mock<IFailedRequestStore>();
            _mockConnectivityService = new Mock<IConnectivityService>();
            _mockRetryPolicy = new Mock<IRetryPolicy>();
            _mockFailureClassifier = new Mock<IFailureClassifier>();
            _mockLogger = new Mock<ILogger<OCRRetryQueueProcessor>>();

            _mockRetryPolicy.SetupGet(p => p.MaxAttempts).Returns(3);
            
            // Default setup for failure classifier - return Transient by default
            _mockFailureClassifier.Setup(c => c.Classify(It.IsAny<Exception>()))
                                 .Returns(FailureType.Transient);

            _processor = new OCRRetryQueueProcessor(
                _mockOCRService.Object,
                _mockFailedRequestStore.Object,
                _mockConnectivityService.Object,
                _mockRetryPolicy.Object,
                _mockFailureClassifier.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task StartMonitoringAsync_StartsConnectivityMonitoring()
        {
            // Arrange
            _mockConnectivityService.Setup(c => c.IsOnlineAsync())
                                   .ReturnsAsync(false);

            // Act
            await _processor.StartMonitoringAsync();

            // Assert
            _mockConnectivityService.Verify(c => c.StartMonitoringAsync(), Times.Once);
        }

        [Fact]
        public async Task StartMonitoringAsync_ProcessesQueueWhenOnline()
        {
            // Arrange
            _mockConnectivityService.Setup(c => c.IsOnlineAsync())
                                   .ReturnsAsync(true);
            
            _mockFailedRequestStore.Setup(s => s.GetRetryableRequestsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(new List<FailedOCRRequest>());

            // Act
            await _processor.StartMonitoringAsync();
            await Task.Delay(100); // Allow async processing to complete

            // Assert
            _mockFailedRequestStore.Verify(s => s.GetRetryableRequestsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ProcessRetryQueueAsync_ProcessesAllRetryableRequests()
        {
            // Arrange
            var failedRequests = new List<FailedOCRRequest>
            {
                CreateFailedRequest("req1"),
                CreateFailedRequest("req2"),
                CreateFailedRequest("req3")
            };

            _mockConnectivityService.Setup(c => c.IsOnlineAsync())
                                   .ReturnsAsync(true);

            _mockFailedRequestStore.Setup(s => s.GetRetryableRequestsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(failedRequests);

            _mockFailedRequestStore.Setup(s => s.UpdateRetryStatusAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(true);

            _mockOCRService.Setup(o => o.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new OCRSubmissionResponse
                          {
                              TrackingId = "SUCCESS-123",
                              Status = OCRProcessingStatus.Queued,
                              SubmittedAt = DateTime.UtcNow
                          });

            _mockFailedRequestStore.Setup(s => s.RemoveRequestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(true);

            var successCount = 0;
            var failureCount = 0;
            _processor.ProcessingCompleted += (s, f) => { successCount = s; failureCount = f; };

            // Act
            await _processor.ProcessRetryQueueAsync();

            // Assert
            Assert.Equal(3, successCount);
            Assert.Equal(0, failureCount);
            _mockOCRService.Verify(o => o.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
            _mockFailedRequestStore.Verify(s => s.RemoveRequestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Theory]
        [InlineData(true, 1, 0, "Successful retry should increment success count")]
        [InlineData(false, 0, 1, "Failed retry should increment failure count")]
        public async Task ProcessRetryQueueAsync_TracksSuccessAndFailure(
            bool submitSuccess, int expectedSuccess, int expectedFailure, string scenario)
        {
            // Arrange
            var failedRequest = CreateFailedRequest("req1");
            _mockConnectivityService.Setup(c => c.IsOnlineAsync())
                                   .ReturnsAsync(true);

            _mockFailedRequestStore.Setup(s => s.GetRetryableRequestsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(new List<FailedOCRRequest> { failedRequest });

            if (submitSuccess)
            {
                _mockOCRService.Setup(o => o.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                              .ReturnsAsync(new OCRSubmissionResponse
                              {
                                  TrackingId = "SUCCESS-123",
                                  Status = OCRProcessingStatus.Queued
                              });
            }
            else
            {
                _mockOCRService.Setup(o => o.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new Exception("Submission failed"));
                              
                // Setup UpdateRetryStatusAsync to complete successfully
                _mockFailedRequestStore.Setup(s => s.UpdateRetryStatusAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            }

            var actualSuccess = 0;
            var actualFailure = 0;
            _processor.ProcessingCompleted += (s, f) => { actualSuccess = s; actualFailure = f; };

            // Act
            await _processor.ProcessRetryQueueAsync();

            // Assert
            Assert.Equal(expectedSuccess, actualSuccess);
            Assert.Equal(expectedFailure, actualFailure);
        }

        [Fact]
        public async Task ProcessRetryQueueAsync_StopsOnConnectivityLoss()
        {
            // Arrange
            var failedRequests = new List<FailedOCRRequest>
            {
                CreateFailedRequest("req1"),
                CreateFailedRequest("req2"),
                CreateFailedRequest("req3")
            };

            var callCount = 0;
            var actualCalls = new List<int>();
            _mockConnectivityService.Setup(c => c.IsOnlineAsync())
                                   .ReturnsAsync(() =>
                                   {
                                       callCount++;
                                       actualCalls.Add(callCount);
                                       // Online for first 2 calls to match the expected behavior
                                       return callCount <= 2; // Reverted to original
                                   });

            _mockFailedRequestStore.Setup(s => s.GetRetryableRequestsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(failedRequests);

            _mockOCRService.Setup(o => o.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new OCRSubmissionResponse
                          {
                              TrackingId = "SUCCESS-123",
                              Status = OCRProcessingStatus.Queued
                          });

            // Act
            await _processor.ProcessRetryQueueAsync();

            // Assert - Should process 2 requests before connectivity loss on 3rd check
            _mockOCRService.Verify(o => o.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task AddToRetryQueueAsync_StoresFailedRequest()
        {
            // Arrange
            var request = new OCRSubmissionRequest
            {
                ClientRequestId = "client-123",
                ImageData = "base64data",
                DocumentType = DocumentType.Receipt
            };

            _mockFailedRequestStore.Setup(s => s.StoreFailedRequestAsync(It.IsAny<FailedOCRRequest>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync("stored-id");

            // Act
            await _processor.AddToRetryQueueAsync(request, FailureType.Transient, "Network error");

            // Assert
            _mockFailedRequestStore.Verify(s => s.StoreFailedRequestAsync(
                It.Is<FailedOCRRequest>(r => 
                    r.OriginalRequest.ClientRequestId == "client-123" &&
                    r.FailureType == FailureType.Transient &&
                    r.LastErrorMessage == "Network error"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetQueueStatusAsync_ReturnsCorrectStatus()
        {
            // Arrange
            _mockFailedRequestStore.Setup(s => s.GetPendingCountAsync(It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(10);

            _mockFailedRequestStore.Setup(s => s.GetRetryableRequestsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(new List<FailedOCRRequest>
                                  {
                                      CreateFailedRequest("req1"),
                                      CreateFailedRequest("req2"),
                                      CreateFailedRequest("req3")
                                  });

            _mockConnectivityService.Setup(c => c.IsOnlineAsync())
                                   .ReturnsAsync(true);

            // Act
            var status = await _processor.GetQueueStatusAsync();

            // Assert
            Assert.Equal(10, status.TotalPending);
            Assert.Equal(3, status.RetryableCount);
            Assert.True(status.IsOnline);
            Assert.False(status.IsProcessing);
        }

        [Fact]
        public async Task ProcessRetryQueueAsync_RemovesExhaustedRequests()
        {
            // Arrange
            _mockConnectivityService.Setup(c => c.IsOnlineAsync())
                                   .ReturnsAsync(true);

            _mockFailedRequestStore.Setup(s => s.RemoveExhaustedRequestsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(5);

            _mockFailedRequestStore.Setup(s => s.GetRetryableRequestsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(new List<FailedOCRRequest>());

            // Act
            await _processor.ProcessRetryQueueAsync();

            // Assert
            _mockFailedRequestStore.Verify(s => s.RemoveExhaustedRequestsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StopMonitoringAsync_CancelsOngoingProcessing()
        {
            // Arrange
            var tcs = new TaskCompletionSource<bool>();
            
            _mockConnectivityService.Setup(c => c.IsOnlineAsync())
                                   .ReturnsAsync(true);

            _mockFailedRequestStore.Setup(s => s.GetRetryableRequestsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                                  .Returns(async (int max, CancellationToken ct) =>
                                  {
                                      await tcs.Task;
                                      ct.ThrowIfCancellationRequested();
                                      return new List<FailedOCRRequest>();
                                  });

            // Act
            var processingTask = _processor.ProcessRetryQueueAsync();
            await Task.Delay(50); // Let processing start
            
            var stopTask = _processor.StopMonitoringAsync();
            tcs.SetResult(true); // Unblock the processing
            
            await stopTask;

            // Assert
            _mockConnectivityService.Verify(c => c.StopMonitoringAsync(), Times.Once);
        }

        [Fact]
        public void Dispose_StopsMonitoring()
        {
            // Arrange
            _mockConnectivityService.Setup(c => c.IsOnlineAsync())
                                   .ReturnsAsync(false);
            _mockConnectivityService.Setup(c => c.StopMonitoringAsync())
                                   .Returns(Task.CompletedTask)
                                   .Verifiable();

            // Act
            _processor.Dispose();

            // Assert
            _mockConnectivityService.Verify(c => c.StopMonitoringAsync(), Times.Once);
        }

        [Theory]
        [InlineData(OCRProcessingStatus.Queued, true, "Queued status is success")]
        [InlineData(OCRProcessingStatus.Processing, true, "Processing status is success")]
        [InlineData(OCRProcessingStatus.Complete, true, "Complete status is success")]
        [InlineData(OCRProcessingStatus.Failed, false, "Failed status is not success")]
        public async Task ProcessSingleRequest_HandlesResponseStatus(
            OCRProcessingStatus status, bool expectedSuccess, string scenario)
        {
            // Arrange
            var failedRequest = CreateFailedRequest("req1");
            
            _mockConnectivityService.Setup(c => c.IsOnlineAsync())
                                   .ReturnsAsync(true);

            _mockFailedRequestStore.Setup(s => s.GetRetryableRequestsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(new List<FailedOCRRequest> { failedRequest });

            _mockOCRService.Setup(o => o.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new OCRSubmissionResponse
                          {
                              TrackingId = "RESULT-123",
                              Status = status,
                              SubmittedAt = DateTime.UtcNow
                          });

            var successInvoked = false;
            var failureInvoked = false;
            _processor.RequestSucceeded += (id) => successInvoked = true;
            _processor.RequestFailed += (id, msg) => failureInvoked = true;

            // Act
            await _processor.ProcessRetryQueueAsync();

            // Assert
            Assert.Equal(expectedSuccess, successInvoked);
            Assert.Equal(!expectedSuccess, failureInvoked);
        }

        private FailedOCRRequest CreateFailedRequest(string id)
        {
            return new FailedOCRRequest
            {
                RequestId = id,
                OriginalRequest = new OCRSubmissionRequest
                {
                    ClientRequestId = $"client-{id}",
                    ImageData = "base64data",
                    DocumentType = DocumentType.Receipt
                },
                FailedAt = DateTime.UtcNow.AddMinutes(-5),
                RetryCount = 1,
                FailureType = FailureType.Transient
            };
        }

        public void Dispose()
        {
            _processor?.Dispose();
        }
    }
}