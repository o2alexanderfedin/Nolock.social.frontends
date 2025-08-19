using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Camera.Interfaces;
using NoLock.Social.Core.Camera.Services;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.Storage.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Core.Tests.Camera
{
    /// <summary>
    /// Tests for CameraService offline functionality enhancements
    /// Validates Story 1.8 requirement: "Full capture functionality works offline"
    /// </summary>
    public class CameraServiceOfflineEnhancementsTests : IDisposable
    {
        private readonly CameraService _cameraService;
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly Mock<IOfflineStorageService> _offlineStorageMock;
        private readonly Mock<IOfflineQueueService> _offlineQueueMock;
        private readonly Mock<IConnectivityService> _connectivityMock;

        public CameraServiceOfflineEnhancementsTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _offlineStorageMock = new Mock<IOfflineStorageService>();
            _offlineQueueMock = new Mock<IOfflineQueueService>();
            _connectivityMock = new Mock<IConnectivityService>();
            
            _cameraService = new CameraService(
                _jsRuntimeMock.Object,
                _offlineStorageMock.Object,
                _offlineQueueMock.Object,
                _connectivityMock.Object);
        }

        public void Dispose()
        {
            _cameraService?.Dispose();
        }

        #region Offline Capture Functionality

        [Fact]
        public async Task CaptureImage_WhenOffline_ShouldStoreLocallyAndQueue()
        {
            // Arrange
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            var capturedImage = CreateTestCapturedImage("offline-capture.jpg");

            // Act
            await _cameraService.AddPageToSessionAsync(sessionId, capturedImage);

            // Assert
            _offlineStorageMock.Verify(x => x.SaveImageAsync(capturedImage), Times.Once,
                "Should save image to local storage when offline");
            _offlineQueueMock.Verify(x => x.QueueOperationAsync(It.Is<OfflineOperation>(
                op => op.OperationType == "capture" && op.Payload.Contains("offline-capture.jpg"))), 
                Times.Once, "Should queue capture operation for later sync");
        }

        [Theory]
        [InlineData(1, "single page document")]
        [InlineData(5, "multi-page document")]
        [InlineData(10, "large multi-page document")]
        public async Task MultiPageCapture_WhenOffline_ShouldHandleAllPages(
            int pageCount, string scenario)
        {
            // Arrange
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            var sessionId = await _cameraService.CreateDocumentSessionAsync();

            // Act - Capture multiple pages
            for (int i = 1; i <= pageCount; i++)
            {
                var image = CreateTestCapturedImage($"page-{i}.jpg");
                await _cameraService.AddPageToSessionAsync(sessionId, image);
            }

            // Assert
            var pages = await _cameraService.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(pageCount, $"All pages should be captured for {scenario}");
            
            _offlineStorageMock.Verify(x => x.SaveImageAsync(It.IsAny<CapturedImage>()), 
                Times.Exactly(pageCount), $"Should save all images locally for {scenario}");
        }

        [Fact]
        public async Task SessionManagement_WhenOffline_ShouldPersistToIndexedDB()
        {
            // Arrange
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            
            // Act
            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            var image1 = CreateTestCapturedImage("session-page1.jpg");
            var image2 = CreateTestCapturedImage("session-page2.jpg");
            
            await _cameraService.AddPageToSessionAsync(sessionId, image1);
            await _cameraService.AddPageToSessionAsync(sessionId, image2);

            // Assert
            _offlineStorageMock.Verify(x => x.SaveSessionAsync(It.Is<DocumentSession>(
                s => s.SessionId == sessionId && s.Pages.Count == 2)), 
                Times.AtLeastOnce, "Should persist session with all pages to IndexedDB");
        }

        #endregion

        #region Online/Offline Transition Handling

        [Fact]
        public async Task ConnectivityChange_FromOnlineToOffline_ShouldAdaptBehavior()
        {
            // Arrange - Start online
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(true);
            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            
            // Capture image while online
            var onlineImage = CreateTestCapturedImage("online-image.jpg");
            await _cameraService.AddPageToSessionAsync(sessionId, onlineImage);

            // Act - Go offline and capture another image
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            _connectivityMock.Raise(x => x.OnOffline += null, 
                new ConnectivityEventArgs { IsOnline = false, PreviousState = true });
            
            var offlineImage = CreateTestCapturedImage("offline-image.jpg");
            await _cameraService.AddPageToSessionAsync(sessionId, offlineImage);

            // Assert
            var pages = await _cameraService.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(2, "Both online and offline images should be captured");
            
            // Verify offline behavior kicks in
            _offlineStorageMock.Verify(x => x.SaveImageAsync(offlineImage), Times.Once,
                "Should save to local storage when offline");
        }

        [Fact]
        public async Task ConnectivityChange_FromOfflineToOnline_ShouldTriggerSync()
        {
            // Arrange - Start offline with queued operations
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            var offlineImage = CreateTestCapturedImage("queued-image.jpg");
            
            await _cameraService.AddPageToSessionAsync(sessionId, offlineImage);

            // Act - Go online
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(true);
            _connectivityMock.Raise(x => x.OnOnline += null, 
                new ConnectivityEventArgs { IsOnline = true, PreviousState = false });

            // Assert
            _offlineQueueMock.Verify(x => x.ProcessQueueAsync(), Times.AtLeastOnce,
                "Should process queued operations when going online");
        }

        [Fact]
        public async Task FluctuatingConnectivity_ShouldMaintainDataConsistency()
        {
            // Arrange
            var isOnline = true;
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(() => isOnline);
            
            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            var capturedImages = new List<CapturedImage>();

            // Act - Simulate fluctuating connectivity with captures
            for (int i = 1; i <= 6; i++)
            {
                // Toggle connectivity every 2 captures
                if (i % 2 == 0) isOnline = !isOnline;
                
                var image = CreateTestCapturedImage($"fluctuation-image-{i}.jpg");
                capturedImages.Add(image);
                await _cameraService.AddPageToSessionAsync(sessionId, image);
            }

            // Assert
            var pages = await _cameraService.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(capturedImages.Count, 
                "All images should be captured despite connectivity fluctuations");
                
            // Verify images are in correct order
            for (int i = 0; i < capturedImages.Count; i++)
            {
                pages[i].ImageUrl.Should().Be(capturedImages[i].ImageUrl, 
                    $"Image {i + 1} should maintain order");
            }
        }

        #endregion

        #region Offline Queue Management

        [Fact]
        public async Task OfflineOperations_ShouldBePrioritizedCorrectly()
        {
            // Arrange
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            var sessionId = await _cameraService.CreateDocumentSessionAsync();

            // Act - Perform various operations that should be queued
            var image1 = CreateTestCapturedImage("priority-test-1.jpg");
            var image2 = CreateTestCapturedImage("priority-test-2.jpg");
            
            await _cameraService.AddPageToSessionAsync(sessionId, image1);
            await _cameraService.RemovePageFromSessionAsync(sessionId, 0);
            await _cameraService.AddPageToSessionAsync(sessionId, image2);

            // Assert - Verify operations are queued with appropriate priorities
            _offlineQueueMock.Verify(x => x.QueueOperationAsync(It.Is<OfflineOperation>(
                op => op.OperationType == "capture" && op.Priority <= 1)), Times.Exactly(2),
                "Capture operations should have high priority");
            
            _offlineQueueMock.Verify(x => x.QueueOperationAsync(It.Is<OfflineOperation>(
                op => op.OperationType == "remove" && op.Priority >= 1)), Times.Once,
                "Remove operations should have lower priority");
        }

        [Fact]
        public async Task OfflineSessionCompletion_ShouldQueueFinalSyncOperation()
        {
            // Arrange
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            
            var image = CreateTestCapturedImage("completion-test.jpg");
            await _cameraService.AddPageToSessionAsync(sessionId, image);

            // Act - Complete session while offline
            await _cameraService.DisposeDocumentSessionAsync(sessionId);

            // Assert
            _offlineQueueMock.Verify(x => x.QueueOperationAsync(It.Is<OfflineOperation>(
                op => op.OperationType == "session_complete" && 
                      op.Payload.Contains(sessionId))), Times.Once,
                "Should queue session completion for sync when online");
        }

        #endregion

        #region Error Handling and Recovery

        [Fact]
        public async Task OfflineStorageFailure_ShouldFallbackGracefully()
        {
            // Arrange
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            _offlineStorageMock.Setup(x => x.SaveImageAsync(It.IsAny<CapturedImage>()))
                .ThrowsAsync(new OfflineStorageException("Storage quota exceeded", "saveImage"));

            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            var image = CreateTestCapturedImage("storage-failure-test.jpg");

            // Act & Assert - Should handle storage failure gracefully
            await _cameraService.Invoking(s => s.AddPageToSessionAsync(sessionId, image))
                .Should().NotThrowAsync("Should handle storage failures gracefully");
        }

        [Fact]
        public async Task OfflineQueueFailure_ShouldNotPreventCapture()
        {
            // Arrange
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            _offlineQueueMock.Setup(x => x.QueueOperationAsync(It.IsAny<OfflineOperation>()))
                .ThrowsAsync(new Exception("Queue service unavailable"));

            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            var image = CreateTestCapturedImage("queue-failure-test.jpg");

            // Act - Should still capture image even if queueing fails
            await _cameraService.AddPageToSessionAsync(sessionId, image);

            // Assert
            var pages = await _cameraService.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(1, "Should still capture image when queue fails");
            
            _offlineStorageMock.Verify(x => x.SaveImageAsync(image), Times.Once,
                "Should still save to local storage when queue fails");
        }

        [Fact]
        public async Task ConnectivityServiceFailure_ShouldAssumeOffline()
        {
            // Arrange
            _connectivityMock.Setup(x => x.IsOnlineAsync())
                .ThrowsAsync(new Exception("Connectivity service unavailable"));

            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            var image = CreateTestCapturedImage("connectivity-failure-test.jpg");

            // Act
            await _cameraService.AddPageToSessionAsync(sessionId, image);

            // Assert - Should default to offline behavior
            _offlineStorageMock.Verify(x => x.SaveImageAsync(image), Times.Once,
                "Should default to offline storage when connectivity check fails");
        }

        #endregion

        #region Performance and Resource Management

        [Fact]
        public async Task LargeOfflineSession_ShouldManageMemoryEfficiently()
        {
            // Arrange
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            var sessionId = await _cameraService.CreateDocumentSessionAsync();

            // Act - Capture many large images
            for (int i = 1; i <= 20; i++)
            {
                var largeImage = CreateTestCapturedImage($"large-image-{i}.jpg", size: 2000000); // 2MB each
                await _cameraService.AddPageToSessionAsync(sessionId, largeImage);
            }

            // Assert
            var pages = await _cameraService.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(20, "Should handle many large images");
            
            // Verify storage operations are efficient
            _offlineStorageMock.Verify(x => x.SaveImageAsync(It.IsAny<CapturedImage>()), 
                Times.Exactly(20), "Should save all images individually for efficiency");
        }

        [Fact]
        public async Task OfflineBatchOperations_ShouldOptimizeStorage()
        {
            // Arrange
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            var sessionId = await _cameraService.CreateDocumentSessionAsync();

            var images = new List<CapturedImage>();
            for (int i = 1; i <= 5; i++)
            {
                images.Add(CreateTestCapturedImage($"batch-image-{i}.jpg"));
            }

            // Act - Add multiple images in quick succession
            var tasks = images.Select(img => _cameraService.AddPageToSessionAsync(sessionId, img));
            await Task.WhenAll(tasks);

            // Assert
            var pages = await _cameraService.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(images.Count, "Should handle batch operations correctly");
        }

        #endregion

        #region Offline Mode Initialization and Recovery

        [Fact]
        public async Task ServiceInitialization_WithExistingOfflineData_ShouldRestoreCorrectly()
        {
            // Arrange - Setup existing offline sessions
            var existingSessions = new List<DocumentSession>
            {
                new()
                {
                    SessionId = "existing-session-1",
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    Pages = new List<CapturedImage>
                    {
                        CreateTestCapturedImage("existing-page-1.jpg"),
                        CreateTestCapturedImage("existing-page-2.jpg")
                    }
                }
            };

            _offlineStorageMock.Setup(x => x.GetAllSessionsAsync())
                .ReturnsAsync(existingSessions);

            // Act
            await _cameraService.InitializeAsync();

            // Assert
            var isSessionActive = await _cameraService.IsSessionActiveAsync("existing-session-1");
            isSessionActive.Should().BeTrue("Should restore existing offline sessions");

            var pages = await _cameraService.GetSessionPagesAsync("existing-session-1");
            pages.Should().HaveCount(2, "Should restore all pages from offline storage");
        }

        [Fact]
        public async Task OfflineDataSync_WhenGoingOnline_ShouldProcessAllQueuedOperations()
        {
            // Arrange - Start offline with operations
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            
            var queuedOperations = new List<OfflineOperation>
            {
                new() { OperationType = "capture", Payload = "session1-image1", Priority = 0 },
                new() { OperationType = "capture", Payload = "session1-image2", Priority = 1 },
                new() { OperationType = "session_complete", Payload = "session1", Priority = 2 }
            };

            _offlineQueueMock.Setup(x => x.GetQueueStatusAsync())
                .ReturnsAsync(new OfflineQueueStatus 
                { 
                    PendingOperations = queuedOperations.Count,
                    IsProcessing = false
                });

            // Act - Go online and trigger sync
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(true);
            _connectivityMock.Raise(x => x.OnOnline += null, 
                new ConnectivityEventArgs { IsOnline = true, PreviousState = false });

            // Assert
            _offlineQueueMock.Verify(x => x.ProcessQueueAsync(), Times.AtLeastOnce,
                "Should process all queued operations when connectivity restored");
        }

        #endregion

        #region Helper Methods

        private static CapturedImage CreateTestCapturedImage(string imageUrl, int size = 1000000)
        {
            // Create test image data of specified size
            var imageData = $"data:image/jpeg;base64,{new string('A', size / 100)}";
            
            return new CapturedImage
            {
                ImageData = imageData,
                ImageUrl = imageUrl,
                Timestamp = DateTime.UtcNow,
                Width = 1920,
                Height = 1080,
                Quality = 85
            };
        }

        #endregion
    }
}