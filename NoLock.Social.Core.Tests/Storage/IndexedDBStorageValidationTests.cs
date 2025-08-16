using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.Storage.Interfaces;
using NoLock.Social.Core.Storage.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Core.Tests.Storage
{
    /// <summary>
    /// Comprehensive validation tests for IndexedDB storage operations
    /// Tests Story 1.8 requirement: "Images stored locally in IndexedDB"
    /// </summary>
    public class IndexedDBStorageValidationTests : IDisposable
    {
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly IndexedDbStorageService _storageService;

        public IndexedDBStorageValidationTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _storageService = new IndexedDbStorageService(_jsRuntimeMock.Object);
        }

        public void Dispose()
        {
            _storageService?.Dispose();
        }

        #region Session Storage Operations

        [Theory]
        [InlineData("simple-session", DocumentType.SinglePage, "basic single page session")]
        [InlineData("multi-page-session", DocumentType.MultiPage, "multi-page document session")]
        [InlineData("complex-session-123", DocumentType.MultiPage, "session with numeric ID")]
        public async Task SaveSession_WithValidData_ShouldStoreCorrectly(
            string sessionId, DocumentType docType, string scenario)
        {
            // Arrange
            var session = new DocumentSession
            {
                SessionId = sessionId,
                DocumentType = docType,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                IsCompleted = false,
                Pages = new List<CapturedImage>()
            };

            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("indexedDBStorage.saveSession", 
                It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

            // Act
            await _storageService.SaveSessionAsync(session);

            // Assert
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("indexedDBStorage.saveSession", 
                It.Is<object[]>(args => 
                    args.Length > 0 && 
                    args[0].ToString().Contains(sessionId))), 
                Times.Once, $"Should save session correctly for {scenario}");
        }

        [Theory]
        [InlineData(null, "null session")]
        [InlineData("", "empty session ID")]
        [InlineData("   ", "whitespace session ID")]
        public async Task SaveSession_WithInvalidData_ShouldThrowException(
            string sessionId, string scenario)
        {
            // Arrange
            var session = new DocumentSession { SessionId = sessionId };

            // Act & Assert
            await _storageService.Invoking(s => s.SaveSessionAsync(session))
                .Should().ThrowAsync<ArgumentException>($"Should reject {scenario}");
        }

        [Fact]
        public async Task LoadSession_WithExistingId_ShouldReturnCorrectSession()
        {
            // Arrange
            var expectedSession = new DocumentSession
            {
                SessionId = "load-test-session",
                DocumentType = DocumentType.MultiPage,
                CreatedAt = DateTime.UtcNow,
                IsCompleted = false
            };

            _jsRuntimeMock.Setup(x => x.InvokeAsync<DocumentSession>("indexedDBStorage.loadSession", 
                It.IsAny<object[]>()))
                .ReturnsAsync(expectedSession);

            // Act
            var result = await _storageService.LoadSessionAsync("load-test-session");

            // Assert
            result.Should().NotBeNull();
            result!.SessionId.Should().Be(expectedSession.SessionId);
            result.DocumentType.Should().Be(expectedSession.DocumentType);
        }

        [Theory]
        [InlineData("non-existent-session", "non-existent session")]
        [InlineData(null, "null session ID")]
        [InlineData("", "empty session ID")]
        public async Task LoadSession_WithInvalidId_ShouldReturnNull(
            string sessionId, string scenario)
        {
            // Arrange
            _jsRuntimeMock.Setup(x => x.InvokeAsync<DocumentSession>("indexedDBStorage.loadSession", 
                It.IsAny<object[]>()))
                .ReturnsAsync((DocumentSession)null);

            // Act
            var result = await _storageService.LoadSessionAsync(sessionId);

            // Assert
            result.Should().BeNull($"Should return null for {scenario}");
        }

        #endregion

        #region Image Storage Operations

        [Theory]
        [InlineData("image1.jpg", 1920, 1080, 85, "standard quality image")]
        [InlineData("high-res.png", 4096, 2160, 95, "high resolution image")]
        [InlineData("thumbnail.jpg", 150, 150, 60, "thumbnail image")]
        public async Task SaveImage_WithValidData_ShouldStoreCorrectly(
            string fileName, int width, int height, int quality, string scenario)
        {
            // Arrange
            var image = new CapturedImage
            {
                ImageData = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAAAQABAAD/",
                ImageUrl = fileName,
                Timestamp = DateTime.UtcNow,
                Width = width,
                Height = height,
                Quality = quality
            };

            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("indexedDBStorage.saveImage", 
                It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

            // Act
            await _storageService.SaveImageAsync(image);

            // Assert
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("indexedDBStorage.saveImage", 
                It.Is<object[]>(args => 
                    args.Length > 0 && 
                    args[0].ToString().Contains(fileName))), 
                Times.Once, $"Should save image correctly for {scenario}");
        }

        [Fact]
        public async Task SaveImage_WithLargeImageData_ShouldHandleCorrectly()
        {
            // Arrange - Large image data
            var largeImageData = "data:image/jpeg;base64," + new string('A', 5000000); // ~5MB
            var largeImage = new CapturedImage
            {
                ImageData = largeImageData,
                ImageUrl = "large-image.jpg",
                Timestamp = DateTime.UtcNow,
                Width = 4096,
                Height = 3072,
                Quality = 90
            };

            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("indexedDBStorage.saveImage", 
                It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

            // Act & Assert
            await _storageService.Invoking(s => s.SaveImageAsync(largeImage))
                .Should().NotThrowAsync("Should handle large images");

            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("indexedDBStorage.saveImage", 
                It.IsAny<object[]>()), Times.Once);
        }

        [Theory]
        [InlineData(null, "null image")]
        [InlineData("", "empty image data")]
        public async Task SaveImage_WithInvalidData_ShouldThrowException(
            string imageData, string scenario)
        {
            // Arrange
            var image = new CapturedImage { ImageData = imageData };

            // Act & Assert
            await _storageService.Invoking(s => s.SaveImageAsync(image))
                .Should().ThrowAsync<ArgumentException>($"Should reject {scenario}");
        }

        [Fact]
        public async Task LoadImage_WithValidId_ShouldReturnCorrectImage()
        {
            // Arrange
            var expectedImage = new CapturedImage
            {
                ImageData = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQAAAQABAAD/",
                ImageUrl = "test-image.jpg",
                Timestamp = DateTime.UtcNow,
                Width = 1920,
                Height = 1080,
                Quality = 85
            };

            _jsRuntimeMock.Setup(x => x.InvokeAsync<CapturedImage>("indexedDBStorage.loadImage", 
                It.IsAny<object[]>()))
                .ReturnsAsync(expectedImage);

            // Act
            var result = await _storageService.LoadImageAsync("test-image-id");

            // Assert
            result.Should().NotBeNull();
            result!.ImageUrl.Should().Be(expectedImage.ImageUrl);
            result.ImageData.Should().Be(expectedImage.ImageData);
        }

        #endregion

        #region Offline Queue Operations

        [Theory]
        [InlineData("upload", 0, "high priority upload")]
        [InlineData("sync", 1, "medium priority sync")]
        [InlineData("delete", 2, "low priority delete")]
        public async Task QueueOfflineOperation_WithValidData_ShouldStoreCorrectly(
            string operationType, int priority, string scenario)
        {
            // Arrange
            var operation = new OfflineOperation
            {
                OperationType = operationType,
                Payload = $"test-{operationType}-data",
                Priority = priority,
                CreatedAt = DateTime.UtcNow
            };

            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("indexedDBStorage.queueOperation", 
                It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

            // Act
            await _storageService.QueueOfflineOperationAsync(operation);

            // Assert
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("indexedDBStorage.queueOperation", 
                It.Is<object[]>(args => 
                    args.Length > 0 && 
                    args[0].ToString().Contains(operationType))), 
                Times.Once, $"Should queue operation correctly for {scenario}");
        }

        [Fact]
        public async Task GetPendingOperations_ShouldReturnOrderedByPriority()
        {
            // Arrange
            var operations = new List<OfflineOperation>
            {
                new() { OperationType = "sync", Priority = 0, CreatedAt = DateTime.UtcNow },
                new() { OperationType = "upload", Priority = 1, CreatedAt = DateTime.UtcNow },
                new() { OperationType = "delete", Priority = 2, CreatedAt = DateTime.UtcNow }
            };

            _jsRuntimeMock.Setup(x => x.InvokeAsync<List<OfflineOperation>>("indexedDBStorage.getPendingOperations", 
                It.IsAny<object[]>()))
                .ReturnsAsync(operations);

            // Act
            var result = await _storageService.GetPendingOperationsAsync();

            // Assert
            result.Should().HaveCount(3);
            result[0].OperationType.Should().Be("sync", "Highest priority should be first");
            result[1].OperationType.Should().Be("upload", "Medium priority should be second");
            result[2].OperationType.Should().Be("delete", "Lowest priority should be last");
        }

        [Theory]
        [InlineData("operation-1", "first operation")]
        [InlineData("operation-2", "second operation")]
        [InlineData("non-existent", "non-existent operation")]
        public async Task RemoveOperation_WithValidId_ShouldRemoveCorrectly(
            string operationId, string scenario)
        {
            // Arrange
            _jsRuntimeMock.Setup(x => x.InvokeAsync<bool>("indexedDBStorage.removeOperation", 
                It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            await _storageService.RemoveOperationAsync(operationId);

            // Assert
            _jsRuntimeMock.Verify(x => x.InvokeAsync<bool>("indexedDBStorage.removeOperation", 
                It.Is<object[]>(args => args[0].ToString() == operationId)), 
                Times.Once, $"Should remove operation correctly for {scenario}");
        }

        #endregion

        #region Bulk Operations

        [Fact]
        public async Task GetAllSessions_WithMultipleSessions_ShouldReturnAllCorrectly()
        {
            // Arrange
            var expectedSessions = new List<DocumentSession>
            {
                new() { SessionId = "session-1", DocumentType = DocumentType.SinglePage },
                new() { SessionId = "session-2", DocumentType = DocumentType.MultiPage },
                new() { SessionId = "session-3", DocumentType = DocumentType.MultiPage }
            };

            _jsRuntimeMock.Setup(x => x.InvokeAsync<List<DocumentSession>>("indexedDBStorage.getAllSessions", 
                It.IsAny<object[]>()))
                .ReturnsAsync(expectedSessions);

            // Act
            var result = await _storageService.GetAllSessionsAsync();

            // Assert
            result.Should().HaveCount(expectedSessions.Count);
            result.Should().BeEquivalentTo(expectedSessions);
        }

        [Fact]
        public async Task ClearAllData_ShouldRemoveAllStoredData()
        {
            // Arrange
            _jsRuntimeMock.Setup(x => x.InvokeAsync<bool>("indexedDBStorage.clearAllData", 
                It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            await _storageService.ClearAllDataAsync();

            // Assert
            _jsRuntimeMock.Verify(x => x.InvokeAsync<bool>("indexedDBStorage.clearAllData", 
                It.IsAny<object[]>()), Times.Once);

            // Verify subsequent operations work correctly after clearing
            var sessions = await _storageService.GetAllSessionsAsync();
            sessions.Should().BeEmpty("No sessions should remain after clearing all data");
        }

        #endregion

        #region Error Handling and Recovery

        [Theory]
        [InlineData("QuotaExceededError", "storage quota exceeded")]
        [InlineData("InvalidStateError", "invalid IndexedDB state")]
        [InlineData("DataError", "data corruption error")]
        public async Task StorageOperations_WithJSErrors_ShouldThrowAppropriateExceptions(
            string jsError, string scenario)
        {
            // Arrange
            var testImage = new CapturedImage
            {
                ImageData = "data:image/jpeg;base64,test",
                ImageUrl = "error-test.jpg",
                Timestamp = DateTime.UtcNow
            };

            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("indexedDBStorage.saveImage", 
                It.IsAny<object[]>()))
                .ThrowsAsync(new JSException(jsError));

            // Act & Assert
            await _storageService.Invoking(s => s.SaveImageAsync(testImage))
                .Should().ThrowAsync<JSException>()
                .WithMessage($"*{jsError}*", $"Should handle {scenario} appropriately");
        }

        [Fact]
        public async Task ConcurrentOperations_ShouldMaintainDataIntegrity()
        {
            // Arrange
            var session = new DocumentSession
            {
                SessionId = "concurrent-test",
                DocumentType = DocumentType.MultiPage,
                CreatedAt = DateTime.UtcNow
            };

            var images = Enumerable.Range(1, 5)
                .Select(i => new CapturedImage
                {
                    ImageData = $"data:image/jpeg;base64,image{i}",
                    ImageUrl = $"concurrent-image-{i}.jpg",
                    Timestamp = DateTime.UtcNow.AddMilliseconds(i)
                })
                .ToList();

            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>(It.IsAny<string>(), It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

            // Act - Execute concurrent operations
            var tasks = new List<Task> { _storageService.SaveSessionAsync(session) };
            tasks.AddRange(images.Select(img => _storageService.SaveImageAsync(img)));

            await Task.WhenAll(tasks);

            // Assert - Verify all operations completed
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("indexedDBStorage.saveSession", 
                It.IsAny<object[]>()), Times.Once);
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("indexedDBStorage.saveImage", 
                It.IsAny<object[]>()), Times.Exactly(images.Count));
        }

        [Fact]
        public async Task StorageOperations_AfterServiceDisposal_ShouldThrowObjectDisposedException()
        {
            // Arrange
            var testSession = new DocumentSession { SessionId = "disposal-test" };
            _storageService.Dispose();

            // Act & Assert
            await _storageService.Invoking(s => s.SaveSessionAsync(testSession))
                .Should().ThrowAsync<ObjectDisposedException>();
        }

        #endregion

        #region Performance and Capacity Tests

        [Theory]
        [InlineData(10, "small batch")]
        [InlineData(50, "medium batch")]
        [InlineData(100, "large batch")]
        public async Task BatchOperations_WithManyItems_ShouldPerformEfficiently(
            int itemCount, string scenario)
        {
            // Arrange
            var sessions = Enumerable.Range(1, itemCount)
                .Select(i => new DocumentSession
                {
                    SessionId = $"batch-session-{i}",
                    DocumentType = DocumentType.MultiPage,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("indexedDBStorage.saveSession", 
                It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

            // Act - Measure performance
            var startTime = DateTime.UtcNow;
            var tasks = sessions.Select(session => _storageService.SaveSessionAsync(session));
            await Task.WhenAll(tasks);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            duration.Should().BeLessThan(TimeSpan.FromSeconds(10), 
                $"Batch operations should complete efficiently for {scenario}");
            
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("indexedDBStorage.saveSession", 
                It.IsAny<object[]>()), Times.Exactly(itemCount));
        }

        #endregion

        #region Data Validation Tests

        [Fact]
        public async Task SaveSession_WithComplexSessionData_ShouldPreserveAllProperties()
        {
            // Arrange
            var complexSession = new DocumentSession
            {
                SessionId = "complex-session-test",
                DocumentType = DocumentType.MultiPage,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow.AddMinutes(5),
                IsCompleted = true,
                Pages = new List<CapturedImage>
                {
                    new()
                    {
                        ImageData = "data:image/jpeg;base64,page1data",
                        ImageUrl = "page1.jpg",
                        Timestamp = DateTime.UtcNow,
                        Width = 1920,
                        Height = 1080,
                        Quality = 85
                    },
                    new()
                    {
                        ImageData = "data:image/jpeg;base64,page2data",
                        ImageUrl = "page2.jpg", 
                        Timestamp = DateTime.UtcNow.AddSeconds(1),
                        Width = 1920,
                        Height = 1080,
                        Quality = 90
                    }
                }
            };

            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("indexedDBStorage.saveSession", 
                It.IsAny<object[]>()))
                .Returns(ValueTask.FromResult<object>(true));

            // Act
            await _storageService.SaveSessionAsync(complexSession);

            // Assert - Verify complex data is serialized correctly
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("indexedDBStorage.saveSession", 
                It.Is<object[]>(args => 
                    args.Length > 0 && 
                    args[0].ToString().Contains("complex-session-test") &&
                    args[0].ToString().Contains("page1.jpg") &&
                    args[0].ToString().Contains("page2.jpg"))), 
                Times.Once);
        }

        #endregion
    }
}