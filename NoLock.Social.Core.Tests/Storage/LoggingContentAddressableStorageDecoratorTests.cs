using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Storage;

namespace NoLock.Social.Core.Tests.Storage
{
    public class LoggingContentAddressableStorageDecoratorTests
    {
        private readonly Mock<IContentAddressableStorage<string>> _mockStorage;
        private readonly Mock<ILogger<LoggingContentAddressableStorageDecorator<string>>> _mockLogger;
        private readonly LoggingContentAddressableStorageDecorator<string> _sut;

        public LoggingContentAddressableStorageDecoratorTests()
        {
            _mockStorage = new Mock<IContentAddressableStorage<string>>();
            _mockLogger = new Mock<ILogger<LoggingContentAddressableStorageDecorator<string>>>();
            _sut = new LoggingContentAddressableStorageDecorator<string>(_mockStorage.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullStorage_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new LoggingContentAddressableStorageDecorator<string>(null!, _mockLogger.Object));
            exception.ParamName.Should().Be("underlying");
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert  
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new LoggingContentAddressableStorageDecorator<string>(_mockStorage.Object, null!));
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Act
            var decorator = new LoggingContentAddressableStorageDecorator<string>(_mockStorage.Object, _mockLogger.Object);

            // Assert
            decorator.Should().NotBeNull();
        }

        [Fact]
        public async Task StoreAsync_ForwardsToUnderlyingStorage()
        {
            // Arrange
            var data = "test data";
            var expectedHash = "expected-hash";
            _mockStorage.Setup(x => x.StoreAsync(data, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedHash);

            // Act
            var result = await _sut.StoreAsync(data, CancellationToken.None);

            // Assert
            result.Should().Be(expectedHash);
            _mockStorage.Verify(x => x.StoreAsync(data, CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task GetAsync_ForwardsToUnderlyingStorage()
        {
            // Arrange
            var hash = "test-hash";
            var expectedData = "expected data";
            _mockStorage.Setup(x => x.GetAsync(hash, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedData);

            // Act
            var result = await _sut.GetAsync(hash, CancellationToken.None);

            // Assert
            result.Should().Be(expectedData);
            _mockStorage.Verify(x => x.GetAsync(hash, CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task ExistsAsync_ForwardsToUnderlyingStorage()
        {
            // Arrange
            var hash = "test-hash";
            _mockStorage.Setup(x => x.ExistsAsync(hash, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(true);

            // Act
            var result = await _sut.ExistsAsync(hash, CancellationToken.None);

            // Assert
            result.Should().BeTrue();
            _mockStorage.Verify(x => x.ExistsAsync(hash, CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ForwardsToUnderlyingStorage()
        {
            // Arrange
            var hash = "test-hash";
            _mockStorage.Setup(x => x.DeleteAsync(hash, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(true);

            // Act
            var result = await _sut.DeleteAsync(hash, CancellationToken.None);

            // Assert
            result.Should().BeTrue();
            _mockStorage.Verify(x => x.DeleteAsync(hash, CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task StoreAsync_LogsInformationMessages()
        {
            // Arrange
            var data = "test data";
            var expectedHash = "expected-hash";
            _mockStorage.Setup(x => x.StoreAsync(data, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedHash);

            // Act
            await _sut.StoreAsync(data, CancellationToken.None);

            // Assert
            _mockLogger.VerifyLog(LogLevel.Information, $"Storing content of type {typeof(string).Name}", Times.Once());
            _mockLogger.VerifyLog(LogLevel.Information, $"Content stored successfully with hash: {expectedHash}", Times.Once());
        }

        [Fact]
        public async Task GetAsync_WhenContentFound_LogsInformationMessage()
        {
            // Arrange
            var hash = "test-hash";
            var expectedData = "expected data";
            _mockStorage.Setup(x => x.GetAsync(hash, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedData);

            // Act
            await _sut.GetAsync(hash, CancellationToken.None);

            // Assert
            _mockLogger.VerifyLog(LogLevel.Information, $"Retrieving content with hash: {hash}", Times.Once());
            _mockLogger.VerifyLog(LogLevel.Information, $"Content retrieved successfully for hash: {hash}", Times.Once());
        }

        [Fact]
        public async Task GetAsync_WhenContentNotFound_LogsWarningMessage()
        {
            // Arrange
            var hash = "test-hash";
            _mockStorage.Setup(x => x.GetAsync(hash, It.IsAny<CancellationToken>()))
                       .ReturnsAsync((string?)null);

            // Act
            await _sut.GetAsync(hash, CancellationToken.None);

            // Assert
            _mockLogger.VerifyLog(LogLevel.Information, $"Retrieving content with hash: {hash}", Times.Once());
            _mockLogger.VerifyLog(LogLevel.Warning, $"Content not found for hash: {hash}", Times.Once());
        }

        [Fact]
        public async Task DeleteAsync_WhenContentDeleted_LogsInformationMessages()
        {
            // Arrange
            var hash = "test-hash";
            _mockStorage.Setup(x => x.DeleteAsync(hash, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(true);

            // Act
            await _sut.DeleteAsync(hash, CancellationToken.None);

            // Assert
            _mockLogger.VerifyLog(LogLevel.Information, $"Attempting to delete content with hash: {hash}", Times.Once());
            _mockLogger.VerifyLog(LogLevel.Information, $"Content deleted successfully for hash: {hash}", Times.Once());
        }

        [Fact]
        public async Task DeleteAsync_WhenContentNotFound_LogsWarningMessage()
        {
            // Arrange
            var hash = "test-hash";
            _mockStorage.Setup(x => x.DeleteAsync(hash, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(false);

            // Act
            await _sut.DeleteAsync(hash, CancellationToken.None);

            // Assert
            _mockLogger.VerifyLog(LogLevel.Information, $"Attempting to delete content with hash: {hash}", Times.Once());
            _mockLogger.VerifyLog(LogLevel.Warning, $"Content not found for deletion, hash: {hash}", Times.Once());
        }

        // TODO: Add exception handling tests  
        // TODO: Add performance/timing tests
        // TODO: Add concurrent access tests
    }

    public static class MockLoggerExtensions
    {
        public static void VerifyLog<T>(this Mock<ILogger<T>> mockLogger, LogLevel logLevel, string message, Times times)
        {
            mockLogger.Verify(
                x => x.Log(
                    logLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                times);
        }
    }
}