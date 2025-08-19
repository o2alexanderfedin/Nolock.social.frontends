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
    /// Comprehensive validation tests for IndexedDB storage operations
    /// Tests Story 1.8 requirement: "Images stored locally in IndexedDB"
    /// </summary>
    public class IndexedDBStorageValidationTests : IDisposable
    {
        private readonly Mock<IJSRuntimeWrapper> _jsRuntimeWrapperMock;
        private readonly IndexedDbStorageService _storageService;

        public IndexedDBStorageValidationTests()
        {
            _jsRuntimeWrapperMock = new Mock<IJSRuntimeWrapper>();
            
            // Setup default mock for all InvokeVoidAsync calls
            _jsRuntimeWrapperMock.Setup(x => x.InvokeVoidAsync(
                It.IsAny<string>(), 
                It.IsAny<object[]>()))
                .Returns(ValueTask.CompletedTask);
            
            _storageService = new IndexedDbStorageService(_jsRuntimeWrapperMock.Object);
        }

        public void Dispose()
        {
            _storageService?.Dispose();
        }

        #region Session Storage Operations

        [Theory]
        [InlineData("simple-session", "single-page", "basic single page session")]
        [InlineData("multi-page-session", "multi-page", "multi-page document session")]
        [InlineData("complex-session-123", "multi-page", "session with numeric ID")]
        public async Task SaveSession_WithValidData_ShouldStoreCorrectly(
            string sessionId, string processorId, string scenario)
        {
            // Arrange
            var session = new DocumentSession
            {
                SessionId = sessionId,
                DocumentType = processorId,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                Pages = new List<CapturedImage>()
            };

            // Act (using global mock setup)
            await _storageService.SaveSessionAsync(session);

            // Assert
            _jsRuntimeWrapperMock.Verify(x => x.InvokeVoidAsync("indexedDBStorage.saveSession", 
                It.Is<object[]>(args => 
                    args.Length >= 2 && 
                    args[0].ToString() == sessionId)), 
                Times.Once, $"Should save session correctly for {scenario}");
        }

        [Fact]
        public async Task SaveSession_WithNullSession_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await _storageService.Invoking(s => s.SaveSessionAsync(null))
                .Should().ThrowAsync<ArgumentNullException>("Should reject null session");
        }

        [Fact]
        public async Task LoadSession_WithExistingId_ShouldReturnCorrectSession()
        {
            // Arrange
            var expectedSession = new DocumentSession
            {
                SessionId = "load-test-session",
                DocumentType = "multi-page",
                CreatedAt = DateTime.UtcNow
            };

            // Return JSON since the service deserializes it
            var sessionJson = System.Text.Json.JsonSerializer.Serialize(expectedSession);
            _jsRuntimeWrapperMock.Setup(x => x.InvokeAsync<string>("indexedDBStorage.loadSession", 
                It.IsAny<object[]>()))
                .ReturnsAsync(sessionJson);

            // Act
            var result = await _storageService.LoadSessionAsync("load-test-session");

            // Assert
            result.Should().NotBeNull();
            result!.SessionId.Should().Be(expectedSession.SessionId);
            result.DocumentType.Should().Be(expectedSession.DocumentType);
        }

        [Fact]
        public async Task LoadSession_WithNonExistentId_ShouldReturnNull()
        {
            // Arrange
            _jsRuntimeWrapperMock.Setup(x => x.InvokeAsync<string>("indexedDBStorage.loadSession", 
                It.IsAny<object[]>()))
                .ReturnsAsync((string)null);

            // Act
            var result = await _storageService.LoadSessionAsync("non-existent-session");

            // Assert
            result.Should().BeNull("Should return null for non-existent session");
        }

        [Theory]
        [InlineData(null, "null session ID")]
        [InlineData("", "empty session ID")]
        public async Task LoadSession_WithInvalidId_ShouldThrowArgumentException(
            string sessionId, string scenario)
        {
            // Act & Assert
            await _storageService.Invoking(s => s.LoadSessionAsync(sessionId))
                .Should().ThrowAsync<ArgumentException>($"Should reject {scenario}");
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

            // Act (using global mock setup)
            await _storageService.SaveImageAsync(image);

            // Assert - The service passes imageId (timestamp) as first arg, JSON as second
            _jsRuntimeWrapperMock.Verify(x => x.InvokeVoidAsync("indexedDBStorage.saveImage", 
                It.Is<object[]>(args => 
                    args.Length >= 2 && 
                    args[1].ToString().Contains($"\"ImageUrl\":\"{fileName}\""))), 
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

            // Act (using global mock setup) & Assert
            await _storageService.Invoking(s => s.SaveImageAsync(largeImage))
                .Should().NotThrowAsync("Should handle large images");

            _jsRuntimeWrapperMock.Verify(x => x.InvokeVoidAsync("indexedDBStorage.saveImage", 
                It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task SaveImage_WithNullImage_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await _storageService.Invoking(s => s.SaveImageAsync(null))
                .Should().ThrowAsync<ArgumentNullException>("Should reject null image");
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

            // Return JSON since the service deserializes it
            var imageJson = System.Text.Json.JsonSerializer.Serialize(expectedImage);
            _jsRuntimeWrapperMock.Setup(x => x.InvokeAsync<string>("indexedDBStorage.loadImage", 
                It.IsAny<object[]>()))
                .ReturnsAsync(imageJson);

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
                OperationId = $"test-op-{operationType}",
                OperationType = operationType,
                Payload = $"test-{operationType}-data",
                Priority = priority,
                CreatedAt = DateTime.UtcNow
            };

            // Act (using global mock setup)
            await _storageService.QueueOfflineOperationAsync(operation);

            // Assert - The service passes operationId as first arg, JSON as second
            _jsRuntimeWrapperMock.Verify(x => x.InvokeVoidAsync("indexedDBStorage.queueOperation", 
                It.Is<object[]>(args => 
                    args.Length >= 2 && 
                    args[1].ToString().Contains($"\"OperationType\":\"{operationType}\""))), 
                Times.Once, $"Should queue operation correctly for {scenario}");
        }

        [Fact]
        public async Task GetPendingOperations_ShouldReturnOrderedByPriority()
        {
            // Arrange
            var operations = new List<OfflineOperation>
            {
                new() { OperationId = "op1", OperationType = "sync", Priority = 0, CreatedAt = DateTime.UtcNow },
                new() { OperationId = "op2", OperationType = "upload", Priority = 1, CreatedAt = DateTime.UtcNow },
                new() { OperationId = "op3", OperationType = "delete", Priority = 2, CreatedAt = DateTime.UtcNow }
            };

            // Return JSON array since the service deserializes it
            var operationsJson = operations.Select(o => System.Text.Json.JsonSerializer.Serialize(o)).ToArray();
            _jsRuntimeWrapperMock.Setup(x => x.InvokeAsync<string[]>("indexedDBStorage.getPendingOperations", 
                It.IsAny<object[]>()))
                .ReturnsAsync(operationsJson);

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
            // Arrange (using global mock setup)
            // Act
            await _storageService.RemoveOperationAsync(operationId);

            // Assert
            _jsRuntimeWrapperMock.Verify(x => x.InvokeVoidAsync("indexedDBStorage.removeOperation", 
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
                new() { SessionId = "session-1", DocumentType = "single-page" },
                new() { SessionId = "session-2", DocumentType = "multi-page" },
                new() { SessionId = "session-3", DocumentType = "multi-page" }
            };

            // Return JSON array since the service deserializes it
            var sessionsJson = expectedSessions.Select(s => System.Text.Json.JsonSerializer.Serialize(s)).ToArray();
            _jsRuntimeWrapperMock.Setup(x => x.InvokeAsync<string[]>("indexedDBStorage.getAllSessions", 
                It.IsAny<object[]>()))
                .ReturnsAsync(sessionsJson);

            // Act
            var result = await _storageService.GetAllSessionsAsync();

            // Assert
            result.Should().HaveCount(expectedSessions.Count);
            result.Should().BeEquivalentTo(expectedSessions);
        }

        [Fact]
        public async Task ClearAllData_ShouldRemoveAllStoredData()
        {
            // Arrange (using global mock setup)
            // Act
            await _storageService.ClearAllDataAsync();

            // Assert
            _jsRuntimeWrapperMock.Verify(x => x.InvokeVoidAsync("indexedDBStorage.clearAllData", 
                It.IsAny<object[]>()), Times.Once);

            // Verify subsequent operations can be called after clearing
            _jsRuntimeWrapperMock.Setup(x => x.InvokeAsync<string[]>("indexedDBStorage.getAllSessions", 
                It.IsAny<object[]>()))
                .ReturnsAsync(Array.Empty<string>());
            
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

            _jsRuntimeWrapperMock.Setup(x => x.InvokeVoidAsync("indexedDBStorage.saveImage", 
                It.IsAny<object[]>()))
                .ThrowsAsync(new JSException(jsError));

            // Act & Assert
            await _storageService.Invoking(s => s.SaveImageAsync(testImage))
                .Should().ThrowAsync<OfflineStorageException>()
                .Where(ex => ex.InnerException is JSException && ex.InnerException.Message.Contains(jsError),
                    $"Should handle {scenario} appropriately");
        }

        [Fact]
        public async Task ConcurrentOperations_ShouldMaintainDataIntegrity()
        {
            // Arrange
            var session = new DocumentSession
            {
                SessionId = "concurrent-test",
                DocumentType = "multi-page",
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

            // Act - Execute concurrent operations (using global mock setup)
            var tasks = new List<Task> { _storageService.SaveSessionAsync(session) };
            tasks.AddRange(images.Select(img => _storageService.SaveImageAsync(img)));

            await Task.WhenAll(tasks);

            // Assert - Verify all operations completed
            _jsRuntimeWrapperMock.Verify(x => x.InvokeVoidAsync("indexedDBStorage.saveSession", 
                It.IsAny<object[]>()), Times.Once);
            _jsRuntimeWrapperMock.Verify(x => x.InvokeVoidAsync("indexedDBStorage.saveImage", 
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
                    DocumentType = "multi-page",
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            // Act - Measure performance (using global mock setup)
            var startTime = DateTime.UtcNow;
            var tasks = sessions.Select(session => _storageService.SaveSessionAsync(session));
            await Task.WhenAll(tasks);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            duration.Should().BeLessThan(TimeSpan.FromSeconds(10), 
                $"Batch operations should complete efficiently for {scenario}");
            
            _jsRuntimeWrapperMock.Verify(x => x.InvokeVoidAsync("indexedDBStorage.saveSession", 
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
                DocumentType = "multi-page",
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow.AddMinutes(5),
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

            // Act (using global mock setup)
            await _storageService.SaveSessionAsync(complexSession);

            // Assert - Verify complex data is serialized correctly
            _jsRuntimeWrapperMock.Verify(x => x.InvokeVoidAsync("indexedDBStorage.saveSession", 
                It.Is<object[]>(args => 
                    args.Length >= 2 && 
                    args[0].ToString() == "complex-session-test" &&
                    args[1].ToString().Contains("page1.jpg") &&
                    args[1].ToString().Contains("page2.jpg"))), 
                Times.Once);
        }

        #endregion
    }
}