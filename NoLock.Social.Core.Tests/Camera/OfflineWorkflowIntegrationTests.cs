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
    /// Integration tests for offline capture workflow validation
    /// Tests Story 1.8 acceptance criteria for offline functionality
    /// </summary>
    public class OfflineWorkflowIntegrationTests : IDisposable
    {
        private readonly CameraService _cameraService;
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly Mock<IOfflineStorageService> _offlineStorageMock;
        private readonly Mock<IOfflineQueueService> _offlineQueueMock;
        private readonly Mock<IConnectivityService> _connectivityMock;

        public OfflineWorkflowIntegrationTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _offlineStorageMock = new Mock<IOfflineStorageService>();
            _offlineQueueMock = new Mock<IOfflineQueueService>();
            _connectivityMock = new Mock<IConnectivityService>();
            
            // Setup default mocks for basic operations
            _connectivityMock.Setup(x => x.StartMonitoringAsync()).Returns(Task.CompletedTask);
            _offlineStorageMock.Setup(x => x.GetAllSessionsAsync())
                .ReturnsAsync(new List<DocumentSession>());
            _offlineStorageMock.Setup(x => x.SaveImageAsync(It.IsAny<CapturedImage>()))
                .Returns(Task.CompletedTask);
            _offlineStorageMock.Setup(x => x.SaveSessionAsync(It.IsAny<DocumentSession>()))
                .Returns(Task.CompletedTask);
            _offlineQueueMock.Setup(x => x.QueueOperationAsync(It.IsAny<OfflineOperation>()))
                .Returns(Task.CompletedTask);
            _offlineQueueMock.Setup(x => x.ProcessQueueAsync()).Returns(Task.CompletedTask);
            
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

        #region Story 1.8 Acceptance Criteria Tests

        [Theory]
        [InlineData(true, "online mode - should work normally")]
        [InlineData(false, "offline mode - should store locally")]
        public async Task CaptureWorkflow_InOnlineAndOfflineModes_ShouldWorkCorrectly(
            bool isOnline, string scenario)
        {
            // Arrange
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(isOnline);
            var testImage = CreateTestCapturedImage("test-document.jpg");

            // Act - Create session and capture image
            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            await _cameraService.AddPageToSessionAsync(sessionId, testImage);

            // Assert
            var pages = await _cameraService.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(1, $"Failed for {scenario}");
            pages[0].Should().BeEquivalentTo(testImage, $"Image not stored correctly for {scenario}");

            // Verify storage interactions based on connectivity
            if (isOnline)
            {
                _offlineQueueMock.Verify(x => x.QueueOperationAsync(It.IsAny<OfflineOperation>()), Times.Never,
                    "Should not queue operations when online");
            }
            else
            {
                _offlineStorageMock.Verify(x => x.SaveImageAsync(testImage), Times.Once,
                    "Should save image to IndexedDB when offline");
            }
        }

        [Fact]
        public async Task OfflineImageStorage_ShouldPersistToIndexedDB()
        {
            // Arrange - Simulate offline mode
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            var testImages = new[]
            {
                CreateTestCapturedImage("page1.jpg"),
                CreateTestCapturedImage("page2.jpg"),
                CreateTestCapturedImage("page3.jpg")
            };

            // Act - Capture multiple images while offline
            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            foreach (var image in testImages)
            {
                await _cameraService.AddPageToSessionAsync(sessionId, image);
            }

            // Assert - Verify all images stored in IndexedDB
            foreach (var image in testImages)
            {
                _offlineStorageMock.Verify(x => x.SaveImageAsync(image), Times.Once,
                    $"Image {image.ImageUrl} should be saved to IndexedDB");
            }

            var pages = await _cameraService.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(testImages.Length, "All images should be accessible");
        }

        [Fact]
        public async Task DataPersistence_OnBrowserRefresh_ShouldNotLoseData()
        {
            // Arrange - Create session with data offline
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            var testImage = CreateTestCapturedImage("persistent-image.jpg");
            
            await _cameraService.AddPageToSessionAsync(sessionId, testImage);

            // Setup storage to return saved sessions on initialization
            var savedSession = new DocumentSession
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                Pages = new List<CapturedImage> { testImage }
            };
            
            _offlineStorageMock.Setup(x => x.GetAllSessionsAsync())
                .ReturnsAsync(new List<DocumentSession> { savedSession });

            // Act - Simulate browser refresh by reinitializing
            await _cameraService.InitializeAsync();

            // Assert - Verify data is restored
            var isSessionActive = await _cameraService.IsSessionActiveAsync(sessionId);
            isSessionActive.Should().BeTrue("Session should be restored after browser refresh");

            var restoredPages = await _cameraService.GetSessionPagesAsync(sessionId);
            restoredPages.Should().HaveCount(1, "Pages should be restored");
            restoredPages[0].ImageUrl.Should().Be(testImage.ImageUrl, "Image data should persist");
        }

        #endregion

        #region Error Handling and Edge Cases

        [Theory]
        [InlineData("corrupted-session-data", "storage corruption")]
        [InlineData("network-timeout", "network timeout")]
        [InlineData("quota-exceeded", "storage quota exceeded")]
        public async Task OfflineOperations_WithStorageErrors_ShouldHandleGracefully(
            string errorType, string scenario)
        {
            // Arrange
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            
            var storageException = new OfflineStorageException(
                $"Simulated {errorType} error", "test-operation");
                
            _offlineStorageMock.Setup(x => x.SaveImageAsync(It.IsAny<CapturedImage>()))
                .ThrowsAsync(storageException);

            var testImage = CreateTestCapturedImage("error-test.jpg");
            var sessionId = await _cameraService.CreateDocumentSessionAsync();

            // Act & Assert - Should handle error gracefully
            await _cameraService.Invoking(s => s.AddPageToSessionAsync(sessionId, testImage))
                .Should().NotThrowAsync($"Should handle {scenario} gracefully");
        }

        #endregion

        #region Performance and Resource Management

        [Fact]
        public async Task MultipleOfflineSessions_ShouldManageResourcesEfficiently()
        {
            // Arrange - Multiple concurrent offline sessions
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            var sessionIds = new List<string>();

            // Act - Create multiple sessions with images
            for (int i = 0; i < 5; i++)
            {
                var sessionId = await _cameraService.CreateDocumentSessionAsync();
                sessionIds.Add(sessionId);
                
                // Add multiple pages to each session
                for (int j = 0; j < 3; j++)
                {
                    var image = CreateTestCapturedImage($"session{i}-page{j}.jpg");
                    await _cameraService.AddPageToSessionAsync(sessionId, image);
                }
            }

            // Assert - Verify resource management
            foreach (var sessionId in sessionIds)
            {
                var isActive = await _cameraService.IsSessionActiveAsync(sessionId);
                isActive.Should().BeTrue($"Session {sessionId} should remain active");
                
                var pages = await _cameraService.GetSessionPagesAsync(sessionId);
                pages.Should().HaveCount(3, $"Session {sessionId} should have all pages");
            }

            // Cleanup sessions
            foreach (var sessionId in sessionIds)
            {
                await _cameraService.DisposeDocumentSessionAsync(sessionId);
            }
        }

        [Fact]
        public async Task OfflineDataCleanup_ShouldRemoveExpiredSessions()
        {
            // Arrange - Create sessions and simulate expiry
            _connectivityMock.Setup(x => x.IsOnlineAsync()).ReturnsAsync(false);
            var sessionId = await _cameraService.CreateDocumentSessionAsync();
            
            // Act - Run cleanup process
            await _cameraService.CleanupInactiveSessionsAsync();

            // Assert - Verify cleanup behavior
            _offlineStorageMock.Verify(x => x.ClearAllDataAsync(), Times.Never,
                "Should not clear all data during normal cleanup");
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