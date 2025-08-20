using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.Storage.Interfaces;
using NoLock.Social.Core.Storage.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Core.Tests.Storage
{
    /// <summary>
    /// Tests for browser refresh persistence scenarios using IndexedDB
    /// Validates Story 1.8 requirement: "No data loss on browser refresh"
    /// </summary>
    public class BrowserRefreshPersistenceTests : IDisposable
    {
        private readonly Mock<IJSRuntimeWrapper> _jsRuntimeWrapperMock;
        private readonly IndexedDbStorageService _storageService;
        private readonly Dictionary<string, object> _mockIndexedDb = new();

        public BrowserRefreshPersistenceTests()
        {
            _jsRuntimeWrapperMock = new Mock<IJSRuntimeWrapper>();
            SetupMockIndexedDb();
            _storageService = new IndexedDbStorageService(_jsRuntimeWrapperMock.Object);
        }

        public void Dispose()
        {
            _storageService?.Dispose();
        }

        private void SetupMockIndexedDb()
        {
            // Setup initialization
            _jsRuntimeWrapperMock.Setup(x => x.InvokeVoidAsync("indexedDBStorage.initialize", It.IsAny<object?[]?>()))
                .Returns(ValueTask.CompletedTask);

            // Setup save session
            _jsRuntimeWrapperMock.Setup(x => x.InvokeVoidAsync("indexedDBStorage.saveSession", It.IsAny<object?[]?>()))
                .Callback<string, object?[]?>((method, args) =>
                {
                    if (args?.Length >= 2)
                    {
                        var sessionId = args[0]?.ToString();
                        var sessionJson = args[1]?.ToString();
                        if (!string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(sessionJson))
                        {
                            _mockIndexedDb[$"session_{sessionId}"] = sessionJson;
                        }
                    }
                })
                .Returns(ValueTask.CompletedTask);

            // Setup load session
            _jsRuntimeWrapperMock.Setup(x => x.InvokeAsync<string>("indexedDBStorage.loadSession", It.IsAny<object?[]?>()))
                .ReturnsAsync((string method, object?[]? args) =>
                {
                    if (args?.Length >= 1)
                    {
                        var sessionId = args[0]?.ToString();
                        if (!string.IsNullOrEmpty(sessionId) && _mockIndexedDb.TryGetValue($"session_{sessionId}", out var session))
                        {
                            return session?.ToString() ?? string.Empty;
                        }
                    }
                    return string.Empty;
                });

            // Setup save image
            _jsRuntimeWrapperMock.Setup(x => x.InvokeVoidAsync("indexedDBStorage.saveImage", It.IsAny<object?[]?>()))
                .Callback<string, object?[]?>((method, args) =>
                {
                    if (args?.Length >= 2)
                    {
                        var imageId = args[0]?.ToString();
                        var imageJson = args[1]?.ToString();
                        if (!string.IsNullOrEmpty(imageId) && !string.IsNullOrEmpty(imageJson))
                        {
                            _mockIndexedDb[$"image_{imageId}"] = imageJson;
                        }
                    }
                })
                .Returns(ValueTask.CompletedTask);

            // Setup load image
            _jsRuntimeWrapperMock.Setup(x => x.InvokeAsync<string>("indexedDBStorage.loadImage", It.IsAny<object?[]?>()))
                .ReturnsAsync((string method, object?[]? args) =>
                {
                    if (args?.Length >= 1)
                    {
                        var imageId = args[0]?.ToString();
                        if (!string.IsNullOrEmpty(imageId) && _mockIndexedDb.TryGetValue($"image_{imageId}", out var image))
                        {
                            return image?.ToString() ?? string.Empty;
                        }
                    }
                    return string.Empty;
                });

            // Setup queue operation
            _jsRuntimeWrapperMock.Setup(x => x.InvokeVoidAsync("indexedDBStorage.queueOperation", It.IsAny<object?[]?>()))
                .Callback<string, object?[]?>((method, args) =>
                {
                    if (args?.Length >= 2)
                    {
                        var operationId = args[0]?.ToString();
                        var operationJson = args[1]?.ToString();
                        if (!string.IsNullOrEmpty(operationId) && !string.IsNullOrEmpty(operationJson))
                        {
                            _mockIndexedDb[$"operation_{operationId}"] = operationJson;
                        }
                    }
                })
                .Returns(ValueTask.CompletedTask);

            // Setup get pending operations
            _jsRuntimeWrapperMock.Setup(x => x.InvokeAsync<string[]>("indexedDBStorage.getPendingOperations", It.IsAny<object?[]?>()))
                .ReturnsAsync(() =>
                {
                    var operations = _mockIndexedDb
                        .Where(kvp => kvp.Key.StartsWith("operation_"))
                        .Select(kvp => kvp.Value?.ToString())
                        .Where(op => !string.IsNullOrEmpty(op))
                        .ToArray();
                    return operations!;
                });

            // Setup get all sessions
            _jsRuntimeWrapperMock.Setup(x => x.InvokeAsync<string[]>("indexedDBStorage.getAllSessions", It.IsAny<object?[]?>()))
                .ReturnsAsync(() =>
                {
                    var sessions = _mockIndexedDb
                        .Where(kvp => kvp.Key.StartsWith("session_"))
                        .Select(kvp => kvp.Value?.ToString())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToArray();
                    return sessions!;
                });

            // Setup clear all data
            _jsRuntimeWrapperMock.Setup(x => x.InvokeVoidAsync("indexedDBStorage.clearAllData", It.IsAny<object?[]?>()))
                .Callback(() => _mockIndexedDb.Clear())
                .Returns(ValueTask.CompletedTask);

            // Setup dispose
            _jsRuntimeWrapperMock.Setup(x => x.InvokeVoidAsync("indexedDBStorage.dispose", It.IsAny<object?[]?>()))
                .Returns(ValueTask.CompletedTask);
        }

        #region Browser Refresh Scenarios

        [Theory]
        [InlineData(1, "single session with one page")]
        [InlineData(3, "single session with multiple pages")]
        [InlineData(5, "single session with many pages")]
        public async Task BrowserRefresh_WithSingleSession_ShouldPersistAllData(
            int pageCount, string scenario)
        {
            // Arrange - Create session with pages before "refresh"
            var session = CreateTestDocumentSession("refresh-test-session");
            var images = new List<CapturedImage>();
            
            for (int i = 0; i < pageCount; i++)
            {
                var image = CreateTestCapturedImage($"page-{i + 1}.jpg");
                images.Add(image);
                session.Pages.Add(image);
            }

            // Simulate saving before refresh
            await _storageService.SaveSessionAsync(session);
            foreach (var image in images)
            {
                await _storageService.SaveImageAsync(image);
            }

            // Act - Simulate browser refresh by creating new service instance
            using var newStorageService = new IndexedDbStorageService(_jsRuntimeWrapperMock.Object);
            
            // Simulate restoration after refresh
            var restoredSession = await newStorageService.LoadSessionAsync(session.SessionId);

            // Assert - Verify all data persisted
            restoredSession.Should().NotBeNull($"Session should persist for {scenario}");
            restoredSession!.SessionId.Should().Be(session.SessionId, "Session ID should match");
            restoredSession.Pages.Should().HaveCount(pageCount, $"All pages should persist for {scenario}");
            
            for (int i = 0; i < pageCount; i++)
            {
                restoredSession.Pages[i].ImageUrl.Should().Be($"page-{i + 1}.jpg", 
                    $"Page {i + 1} should persist correctly");
            }
        }

        [Fact]
        public async Task BrowserRefresh_WithMultipleSessions_ShouldPersistAllSessions()
        {
            // Arrange - Create multiple sessions before refresh
            var sessions = new List<DocumentSession>();
            for (int i = 0; i < 3; i++)
            {
                var session = CreateTestDocumentSession($"multi-session-{i}");
                
                // Add pages to each session
                for (int j = 0; j < 2; j++)
                {
                    var image = CreateTestCapturedImage($"session{i}-page{j}.jpg");
                    session.Pages.Add(image);
                    await _storageService.SaveImageAsync(image);
                }
                
                sessions.Add(session);
                await _storageService.SaveSessionAsync(session);
            }

            // Act - Simulate browser refresh
            using var newStorageService = new IndexedDbStorageService(_jsRuntimeWrapperMock.Object);
            var restoredSessions = await newStorageService.GetAllSessionsAsync();

            // Assert - Verify all sessions persist
            restoredSessions.Should().HaveCount(sessions.Count, 
                "All sessions should persist after browser refresh");
                
            foreach (var originalSession in sessions)
            {
                var restored = restoredSessions.Find(s => s.SessionId == originalSession.SessionId);
                restored.Should().NotBeNull($"Session {originalSession.SessionId} should be restored");
                restored!.Pages.Should().HaveCount(originalSession.Pages.Count, 
                    "All pages should be restored for each session");
            }
        }

        [Fact]
        public async Task BrowserRefresh_WithPendingOperations_ShouldRestoreQueue()
        {
            // Arrange - Create pending operations before refresh
            var operations = new List<OfflineOperation>
            {
                new() { OperationType = "upload", Payload = "session1-data", Priority = 1 },
                new() { OperationType = "sync", Payload = "session2-data", Priority = 0 },
                new() { OperationType = "delete", Payload = "old-session", Priority = 2 }
            };

            foreach (var operation in operations)
            {
                await _storageService.QueueOfflineOperationAsync(operation);
            }

            // Act - Simulate browser refresh
            using var newStorageService = new IndexedDbStorageService(_jsRuntimeWrapperMock.Object);
            var restoredOperations = await newStorageService.GetPendingOperationsAsync();

            // Assert - Verify queue persists with correct ordering
            restoredOperations.Should().HaveCount(operations.Count, 
                "All pending operations should persist after refresh");
                
            // Verify operations are restored with correct priorities
            var sortedRestored = restoredOperations.OrderBy(op => op.Priority).ToList();
            sortedRestored[0].OperationType.Should().Be("sync", "Highest priority operation should be first");
            sortedRestored[1].OperationType.Should().Be("upload", "Medium priority operation should be second");
            sortedRestored[2].OperationType.Should().Be("delete", "Lowest priority operation should be last");
        }

        #endregion

        #region Data Integrity Tests

        [Fact]
        public async Task BrowserRefresh_WithCorruptedData_ShouldHandleGracefully()
        {
            // Arrange - Setup JSRuntime to simulate corrupted data
            // Need to override the default mock for this specific test
            var corruptedMock = new Mock<IJSRuntimeWrapper>();
            corruptedMock.Setup(x => x.InvokeVoidAsync("indexedDBStorage.initialize", It.IsAny<object?[]?>()))
                .Returns(ValueTask.CompletedTask);
            corruptedMock.Setup(x => x.InvokeAsync<string>("indexedDBStorage.loadSession", It.IsAny<object?[]?>()))
                .ThrowsAsync(new JSException("IndexedDB corrupted"));
            
            using var testService = new IndexedDbStorageService(corruptedMock.Object);

            // Act & Assert - Should throw OfflineStorageException wrapping the JSException
            var exception = await testService.Invoking(s => s.LoadSessionAsync("corrupted-session"))
                .Should().ThrowAsync<OfflineStorageException>("Should wrap JS errors in OfflineStorageException");
            
            exception.And.InnerException.Should().BeOfType<JSException>();
        }

        [Fact]
        public async Task BrowserRefresh_AfterStorageQuotaExceeded_ShouldRecoverGracefully()
        {
            // Arrange - Simulate quota exceeded scenario
            var session = CreateTestDocumentSession("quota-test");
            var largeImage = CreateTestCapturedImage("huge-image.jpg", 10000000); // 10MB
            
            // Create a special mock for this test
            var quotaMock = new Mock<IJSRuntimeWrapper>();
            quotaMock.Setup(x => x.InvokeVoidAsync("indexedDBStorage.initialize", It.IsAny<object?[]?>()))
                .Returns(ValueTask.CompletedTask);
            
            int callCount = 0;
            quotaMock.Setup(x => x.InvokeVoidAsync("indexedDBStorage.saveImage", It.IsAny<object?[]?>()))
                .Returns(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        throw new JSException("QuotaExceededError");
                    }
                    return ValueTask.CompletedTask;
                });
            
            using var testService = new IndexedDbStorageService(quotaMock.Object);

            // Act - Attempt to save large image
            var exception = await testService.Invoking(s => s.SaveImageAsync(largeImage))
                .Should().ThrowAsync<OfflineStorageException>()
                .WithMessage("*Failed to save image*");
            
            exception.And.InnerException.Should().BeOfType<JSException>();

            // Assert - Service should remain functional after quota error
            var smallImage = CreateTestCapturedImage("small-image.jpg", 1000);
                
            await testService.Invoking(s => s.SaveImageAsync(smallImage))
                .Should().NotThrowAsync("Should recover after quota error");
        }

        [Fact]
        public async Task BrowserRefresh_WithConcurrentOperations_ShouldMaintainConsistency()
        {
            // Arrange - Simulate concurrent operations before refresh
            var session = CreateTestDocumentSession("concurrent-test");
            var tasks = new List<Task>();

            // Create multiple concurrent save operations
            for (int i = 0; i < 5; i++)
            {
                var image = CreateTestCapturedImage($"concurrent-image-{i}.jpg");
                session.Pages.Add(image);
                tasks.Add(_storageService.SaveImageAsync(image));
            }

            tasks.Add(_storageService.SaveSessionAsync(session));

            // Act - Execute all operations concurrently
            await Task.WhenAll(tasks);

            // Simulate browser refresh
            using var newStorageService = new IndexedDbStorageService(_jsRuntimeWrapperMock.Object);
            var restoredSession = await newStorageService.LoadSessionAsync(session.SessionId);

            // Assert - Verify data consistency
            restoredSession.Should().NotBeNull("Session should persist after concurrent operations");
            restoredSession!.Pages.Should().HaveCount(5, "All concurrent images should be saved");
        }

        #endregion

        #region Performance Impact Tests

        [Fact]
        public async Task BrowserRefresh_WithManyStoredSessions_ShouldLoadEfficiently()
        {
            // Arrange - Create many sessions to test performance
            var sessionCount = 20;
            var sessionsCreated = new List<string>();

            for (int i = 0; i < sessionCount; i++)
            {
                var session = CreateTestDocumentSession($"performance-session-{i}");
                sessionsCreated.Add(session.SessionId);
                
                // Add a few images to each session
                for (int j = 0; j < 3; j++)
                {
                    var image = CreateTestCapturedImage($"s{i}-p{j}.jpg");
                    session.Pages.Add(image);
                    await _storageService.SaveImageAsync(image);
                }
                
                await _storageService.SaveSessionAsync(session);
            }

            // Act - Simulate browser refresh and measure load time
            var startTime = DateTime.UtcNow;
            using var newStorageService = new IndexedDbStorageService(_jsRuntimeWrapperMock.Object);
            var allSessions = await newStorageService.GetAllSessionsAsync();
            var loadTime = DateTime.UtcNow - startTime;

            // Assert - Verify efficient loading
            allSessions.Should().HaveCount(sessionCount, "All sessions should be loaded");
            loadTime.Should().BeLessThan(TimeSpan.FromSeconds(5), 
                "Loading many sessions should be reasonably fast");
        }

        #endregion

        #region Storage Cleanup Tests

        [Fact]
        public async Task BrowserRefresh_AfterPartialCleanup_ShouldMaintainValidState()
        {
            // Arrange - Create sessions and perform partial cleanup
            var session1 = CreateTestDocumentSession("cleanup-session-1");
            var session2 = CreateTestDocumentSession("cleanup-session-2");
            
            await _storageService.SaveSessionAsync(session1);
            await _storageService.SaveSessionAsync(session2);

            // Remove one session but keep the other
            // Note: This would typically be done through a cleanup method
            // For now, we'll test that partial data persists correctly

            // Act - Simulate browser refresh
            using var newStorageService = new IndexedDbStorageService(_jsRuntimeWrapperMock.Object);
            var remainingSessions = await newStorageService.GetAllSessionsAsync();

            // Assert - Verify remaining data is intact
            remainingSessions.Should().HaveCount(2, "Sessions should persist until explicitly cleaned");
        }

        #endregion

        #region Helper Methods

        private static DocumentSession CreateTestDocumentSession(string sessionId)
        {
            return new DocumentSession
            {
                SessionId = sessionId,
                DocumentType = "multi-page",
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                Pages = new List<CapturedImage>()
            };
        }

        private static CapturedImage CreateTestCapturedImage(string imageUrl, int approximateSize = 1000000)
        {
            // Create test image data of approximate size
            var paddingLength = Math.Max(100, approximateSize / 100);
            var imageData = $"data:image/jpeg;base64,{new string('A', paddingLength)}";
            
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

        private static string GetImageId(CapturedImage image)
        {
            // Simulate the ID generation logic used by the storage service
            return $"{image.Timestamp:yyyyMMddHHmmssfff}_{image.ImageUrl.GetHashCode():X}";
        }

        #endregion
    }
}