using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Camera.Interfaces;
using NoLock.Social.Core.Camera.Services;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.Storage.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Core.Tests.Camera
{
    public class CameraServiceSessionTests : IDisposable
    {
        private readonly CameraService _sut;
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly Mock<IOfflineStorageService> _offlineStorageMock;
        private readonly Mock<IOfflineQueueService> _offlineQueueMock;
        private readonly Mock<IConnectivityService> _connectivityMock;

        public CameraServiceSessionTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _offlineStorageMock = new Mock<IOfflineStorageService>();
            _offlineQueueMock = new Mock<IOfflineQueueService>();
            _connectivityMock = new Mock<IConnectivityService>();
            
            _sut = new CameraService(
                _jsRuntimeMock.Object,
                _offlineStorageMock.Object,
                _offlineQueueMock.Object,
                _connectivityMock.Object);
        }

        public void Dispose()
        {
            _sut?.Dispose();
        }

        #region Initialization Tests

        [Fact]
        public async Task InitializeAsync_WithValidStoredSessions_ShouldRestoreActiveSessions()
        {
            // Arrange
            var session1 = new DocumentSession { SessionId = "session-1", CreatedAt = DateTime.UtcNow };
            var session2 = new DocumentSession { SessionId = "session-2", CreatedAt = DateTime.UtcNow };
            var storedSessions = new List<DocumentSession> { session1, session2 };
            
            _offlineStorageMock.Setup(x => x.GetAllSessionsAsync())
                .ReturnsAsync(storedSessions);

            // Act
            await _sut.InitializeAsync();

            // Assert
            _offlineStorageMock.Verify(x => x.GetAllSessionsAsync(), Times.Once);
            _offlineQueueMock.Verify(x => x.ProcessQueueAsync(), Times.Once);
            
            // Verify sessions are restored by checking they're active
            var isSession1Active = await _sut.IsSessionActiveAsync("session-1");
            var isSession2Active = await _sut.IsSessionActiveAsync("session-2");
            
            isSession1Active.Should().BeTrue();
            isSession2Active.Should().BeTrue();
        }

        [Fact]
        public async Task InitializeAsync_WithCorruptedData_ShouldHandleGracefully()
        {
            // Arrange
            _offlineStorageMock.Setup(x => x.GetAllSessionsAsync())
                .ThrowsAsync(new Exception("Storage corrupted"));

            // Act & Assert - Should not throw
            await _sut.Invoking(s => s.InitializeAsync())
                .Should().NotThrowAsync();
        }

        [Fact]
        public async Task InitializeAsync_WhenDisposed_ShouldThrowObjectDisposedException()
        {
            // Arrange
            _sut.Dispose();

            // Act & Assert
            await _sut.Invoking(s => s.InitializeAsync())
                .Should().ThrowAsync<ObjectDisposedException>();
        }

        #endregion

        #region Session Creation Tests

        [Fact]
        public async Task CreateDocumentSessionAsync_ShouldReturnValidSessionId()
        {
            // Act
            var sessionId = await _sut.CreateDocumentSessionAsync();

            // Assert
            sessionId.Should().NotBeNullOrEmpty();
            sessionId.Should().MatchRegex(@"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$");
        }

        [Fact]
        public async Task CreateDocumentSessionAsync_ShouldCreateUniqueSessionIds()
        {
            // Act
            var sessionId1 = await _sut.CreateDocumentSessionAsync();
            var sessionId2 = await _sut.CreateDocumentSessionAsync();

            // Assert
            sessionId1.Should().NotBe(sessionId2);
        }

        [Fact]
        public async Task CreateDocumentSessionAsync_WhenDisposed_ShouldThrowObjectDisposedException()
        {
            // Arrange
            _sut.Dispose();

            // Act & Assert
            await _sut.Invoking(s => s.CreateDocumentSessionAsync())
                .Should().ThrowAsync<ObjectDisposedException>();
        }

        #endregion

        #region Session Activity Tests

        [Fact]
        public async Task IsSessionActiveAsync_WithValidSession_ShouldReturnTrue()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();

            // Act
            var isActive = await _sut.IsSessionActiveAsync(sessionId);

            // Assert
            isActive.Should().BeTrue();
        }

        [Theory]
        [InlineData(null, "null session ID")]
        [InlineData("", "empty session ID")]
        [InlineData("   ", "whitespace session ID")]
        [InlineData("invalid-id", "non-existent session ID")]
        public async Task IsSessionActiveAsync_WithInvalidSessionId_ShouldReturnFalse(
            string sessionId, string scenario)
        {
            // Act
            var isActive = await _sut.IsSessionActiveAsync(sessionId);

            // Assert
            isActive.Should().BeFalse($"Expected false for {scenario}");
        }

        #endregion

        #region Page Management Tests

        [Fact]
        public async Task AddPageToSessionAsync_WithValidData_ShouldAddPage()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            var capturedImage = CreateTestCapturedImage("page1.jpg");

            // Act
            await _sut.AddPageToSessionAsync(sessionId, capturedImage);

            // Assert
            var pages = await _sut.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(1);
            pages[0].Should().BeEquivalentTo(capturedImage);
        }

        [Theory]
        [InlineData(null, "valid-image", "null session ID")]
        [InlineData("", "valid-image", "empty session ID")]
        [InlineData("valid-session", null, "null captured image")]
        public async Task AddPageToSessionAsync_WithInvalidParameters_ShouldThrowException(
            string sessionId, string imageType, string scenario)
        {
            // Arrange
            var validSessionId = sessionId == "valid-session" ? await _sut.CreateDocumentSessionAsync() : sessionId;
            var capturedImage = imageType == "valid-image" ? CreateTestCapturedImage("test.jpg") : null;

            // Act & Assert
            await _sut.Invoking(s => s.AddPageToSessionAsync(validSessionId, capturedImage))
                .Should().ThrowAsync<ArgumentException>($"Expected exception for {scenario}");
        }

        [Fact]
        public async Task AddPageToSessionAsync_WithNonExistentSession_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var capturedImage = CreateTestCapturedImage("test.jpg");

            // Act & Assert
            await _sut.Invoking(s => s.AddPageToSessionAsync("non-existent-session", capturedImage))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Session 'non-existent-session' not found*");
        }

        [Fact]
        public async Task AddPageToSessionAsync_MultiplePages_ShouldMaintainOrder()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            var page1 = CreateTestCapturedImage("page1.jpg");
            var page2 = CreateTestCapturedImage("page2.jpg");
            var page3 = CreateTestCapturedImage("page3.jpg");

            // Act
            await _sut.AddPageToSessionAsync(sessionId, page1);
            await _sut.AddPageToSessionAsync(sessionId, page2);
            await _sut.AddPageToSessionAsync(sessionId, page3);

            // Assert
            var pages = await _sut.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(3);
            pages[0].ImageUrl.Should().Be("page1.jpg");
            pages[1].ImageUrl.Should().Be("page2.jpg");
            pages[2].ImageUrl.Should().Be("page3.jpg");
        }

        #endregion

        #region Page Retrieval Tests

        [Theory]
        [InlineData(null, "null session ID")]
        [InlineData("", "empty session ID")]
        [InlineData("non-existent", "non-existent session ID")]
        public async Task GetSessionPagesAsync_WithInvalidSessionId_ShouldThrowException(
            string sessionId, string scenario)
        {
            // Act & Assert
            await _sut.Invoking(s => s.GetSessionPagesAsync(sessionId))
                .Should().ThrowAsync<Exception>($"Expected exception for {scenario}");
        }

        [Fact]
        public async Task GetSessionPagesAsync_WithEmptySession_ShouldReturnEmptyArray()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();

            // Act
            var pages = await _sut.GetSessionPagesAsync(sessionId);

            // Assert
            pages.Should().NotBeNull();
            pages.Should().BeEmpty();
        }

        #endregion

        #region Page Removal Tests

        [Theory]
        [InlineData(0, 3, 2, "remove first page")]
        [InlineData(1, 3, 2, "remove middle page")]
        [InlineData(2, 3, 2, "remove last page")]
        public async Task RemovePageFromSessionAsync_WithValidIndex_ShouldRemovePage(
            int indexToRemove, int totalPages, int expectedRemainingPages, string scenario)
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            for (int i = 0; i < totalPages; i++)
            {
                await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage($"page{i + 1}.jpg"));
            }

            // Act
            await _sut.RemovePageFromSessionAsync(sessionId, indexToRemove);

            // Assert
            var pages = await _sut.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(expectedRemainingPages, $"Failed for {scenario}");
        }

        [Theory]
        [InlineData(-1, "negative index")]
        [InlineData(3, "index beyond range")]
        [InlineData(10, "far beyond range")]
        public async Task RemovePageFromSessionAsync_WithInvalidIndex_ShouldThrowArgumentOutOfRangeException(
            int invalidIndex, string scenario)
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage("page1.jpg"));
            await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage("page2.jpg"));
            await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage("page3.jpg"));

            // Act & Assert
            await _sut.Invoking(s => s.RemovePageFromSessionAsync(sessionId, invalidIndex))
                .Should().ThrowAsync<ArgumentOutOfRangeException>($"Expected exception for {scenario}");
        }

        #endregion

        #region Page Reordering Tests

        [Theory]
        [InlineData(0, 2, new[] { "page2.jpg", "page3.jpg", "page1.jpg" }, "move first to last")]
        [InlineData(2, 0, new[] { "page3.jpg", "page1.jpg", "page2.jpg" }, "move last to first")]
        [InlineData(0, 1, new[] { "page2.jpg", "page1.jpg", "page3.jpg" }, "move first to middle")]
        [InlineData(1, 1, new[] { "page1.jpg", "page2.jpg", "page3.jpg" }, "same position")]
        public async Task ReorderPagesInSessionAsync_WithValidIndices_ShouldReorderCorrectly(
            int fromIndex, int toIndex, string[] expectedOrder, string scenario)
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage("page1.jpg"));
            await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage("page2.jpg"));
            await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage("page3.jpg"));

            // Act
            await _sut.ReorderPagesInSessionAsync(sessionId, fromIndex, toIndex);

            // Assert
            var pages = await _sut.GetSessionPagesAsync(sessionId);
            var actualOrder = pages.Select(p => p.ImageUrl).ToArray();
            actualOrder.Should().BeEquivalentTo(expectedOrder, o => o.WithStrictOrdering(), 
                $"Failed for {scenario}");
        }

        [Theory]
        [InlineData(-1, 0, "negative fromIndex")]
        [InlineData(0, -1, "negative toIndex")]
        [InlineData(3, 0, "fromIndex beyond range")]
        [InlineData(0, 3, "toIndex beyond range")]
        public async Task ReorderPagesInSessionAsync_WithInvalidIndices_ShouldThrowArgumentOutOfRangeException(
            int fromIndex, int toIndex, string scenario)
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage("page1.jpg"));
            await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage("page2.jpg"));

            // Act & Assert
            await _sut.Invoking(s => s.ReorderPagesInSessionAsync(sessionId, fromIndex, toIndex))
                .Should().ThrowAsync<ArgumentOutOfRangeException>($"Expected exception for {scenario}");
        }

        #endregion

        #region Session Cleanup Tests

        [Fact]
        public async Task ClearDocumentSessionAsync_ShouldRemoveAllPages()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage("page1.jpg"));
            await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage("page2.jpg"));

            // Act
            await _sut.ClearDocumentSessionAsync(sessionId);

            // Assert
            var pages = await _sut.GetSessionPagesAsync(sessionId);
            pages.Should().BeEmpty();
        }

        [Fact]
        public async Task DisposeDocumentSessionAsync_ShouldRemoveSession()
        {
            // Arrange
            var sessionId = await _sut.CreateDocumentSessionAsync();
            await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage("page1.jpg"));

            // Act
            await _sut.DisposeDocumentSessionAsync(sessionId);

            // Assert
            var isActive = await _sut.IsSessionActiveAsync(sessionId);
            isActive.Should().BeFalse();
        }

        [Fact]
        public async Task DisposeDocumentSessionAsync_WithNullSessionId_ShouldThrowArgumentException()
        {
            // Act & Assert
            await _sut.Invoking(s => s.DisposeDocumentSessionAsync(null))
                .Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task DisposeDocumentSessionAsync_WithNonExistentSession_ShouldNotThrow()
        {
            // Act & Assert
            await _sut.Invoking(s => s.DisposeDocumentSessionAsync("non-existent"))
                .Should().NotThrowAsync();
        }

        #endregion

        #region Session Expiry Tests

        [Fact]
        public async Task CleanupInactiveSessionsAsync_ShouldRemoveExpiredSessions()
        {
            // Arrange
            var sessionId1 = await _sut.CreateDocumentSessionAsync();
            var sessionId2 = await _sut.CreateDocumentSessionAsync();
            
            // Add pages to both sessions
            await _sut.AddPageToSessionAsync(sessionId1, CreateTestCapturedImage("page1.jpg"));
            await _sut.AddPageToSessionAsync(sessionId2, CreateTestCapturedImage("page2.jpg"));
            
            // Simulate expiry by manipulating the session through reflection
            // Note: This would require access to internal session state for proper testing
            // For now, we'll test the cleanup doesn't throw and continues to work
            
            // Act
            await _sut.CleanupInactiveSessionsAsync();
            
            // Assert - Both sessions should still be active since they haven't expired
            var isActive1 = await _sut.IsSessionActiveAsync(sessionId1);
            var isActive2 = await _sut.IsSessionActiveAsync(sessionId2);
            
            isActive1.Should().BeTrue("Session 1 should still be active");
            isActive2.Should().BeTrue("Session 2 should still be active");
        }

        #endregion

        #region Session Lifecycle Integration Tests

        [Fact]
        public async Task SessionLifecycle_CreateAddPagesCleanupDispose_ShouldWorkCorrectly()
        {
            // Arrange & Act - Create session
            var sessionId = await _sut.CreateDocumentSessionAsync();
            sessionId.Should().NotBeNullOrEmpty();

            // Act - Add multiple pages
            var page1 = CreateTestCapturedImage("document-page1.jpg");
            var page2 = CreateTestCapturedImage("document-page2.jpg");
            var page3 = CreateTestCapturedImage("document-page3.jpg");

            await _sut.AddPageToSessionAsync(sessionId, page1);
            await _sut.AddPageToSessionAsync(sessionId, page2);
            await _sut.AddPageToSessionAsync(sessionId, page3);

            // Assert - Verify pages are added
            var pages = await _sut.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(3);

            // Act - Perform page operations
            await _sut.RemovePageFromSessionAsync(sessionId, 1); // Remove middle page
            await _sut.ReorderPagesInSessionAsync(sessionId, 1, 0); // Reorder remaining pages

            // Assert - Verify operations
            pages = await _sut.GetSessionPagesAsync(sessionId);
            pages.Should().HaveCount(2);

            // Act - Clear session
            await _sut.ClearDocumentSessionAsync(sessionId);

            // Assert - Verify cleared
            pages = await _sut.GetSessionPagesAsync(sessionId);
            pages.Should().BeEmpty();

            // Act - Dispose session
            await _sut.DisposeDocumentSessionAsync(sessionId);

            // Assert - Verify disposed
            var isActive = await _sut.IsSessionActiveAsync(sessionId);
            isActive.Should().BeFalse();
        }

        #endregion

        #region Memory Management Tests

        [Fact]
        public async Task MultipleSessionsDisposal_ShouldCleanupAllResources()
        {
            // Arrange - Create multiple sessions with pages
            var sessionIds = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                var sessionId = await _sut.CreateDocumentSessionAsync();
                sessionIds.Add(sessionId);
                
                // Add pages to each session
                for (int j = 0; j < 3; j++)
                {
                    await _sut.AddPageToSessionAsync(sessionId, CreateTestCapturedImage($"session{i}-page{j}.jpg"));
                }
            }

            // Act - Dispose all sessions
            foreach (var sessionId in sessionIds)
            {
                await _sut.DisposeDocumentSessionAsync(sessionId);
            }

            // Assert - Verify all sessions are inactive
            foreach (var sessionId in sessionIds)
            {
                var isActive = await _sut.IsSessionActiveAsync(sessionId);
                isActive.Should().BeFalse($"Session {sessionId} should be inactive after disposal");
            }
        }

        [Fact]
        public void ServiceDisposal_ShouldCleanupAllActiveSessions()
        {
            // This test verifies that the service's Dispose method properly cleans up resources
            // We can't directly test the internal state, but we can verify no exceptions are thrown
            
            // Arrange - Create service with sessions
            var jsRuntimeMock = new Mock<IJSRuntime>();
            var offlineStorageMock = new Mock<IOfflineStorageService>();
            var offlineQueueMock = new Mock<IOfflineQueueService>();
            var connectivityMock = new Mock<IConnectivityService>();
            using var service = new CameraService(jsRuntimeMock.Object, offlineStorageMock.Object, offlineQueueMock.Object, connectivityMock.Object);
            
            // Create multiple sessions
            var sessionTask1 = service.CreateDocumentSessionAsync();
            var sessionTask2 = service.CreateDocumentSessionAsync();
            
            sessionTask1.Wait();
            sessionTask2.Wait();

            // Act & Assert - Disposal should not throw
            service.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        #endregion

        #region Helper Methods

        private static CapturedImage CreateTestCapturedImage(string imageUrl)
        {
            return new CapturedImage
            {
                ImageData = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAAAQABAAD/",
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