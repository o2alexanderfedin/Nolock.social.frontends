using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NoLock.Social.Core.Storage;
using Xunit;

namespace NoLock.Social.Core.Tests.Storage
{
    public class TypedContentAddressableStorageTests
    {
        private readonly Mock<IContentAddressableStorage<byte[]>> _mockByteStorage;
        private readonly Mock<ISerializer<TestData>> _mockSerializer;
        private readonly TypedContentAddressableStorage<TestData> _typedStorage;

        public TypedContentAddressableStorageTests()
        {
            _mockByteStorage = new Mock<IContentAddressableStorage<byte[]>>();
            _mockSerializer = new Mock<ISerializer<TestData>>();
            _typedStorage = new TypedContentAddressableStorage<TestData>(_mockByteStorage.Object, _mockSerializer.Object);
        }

        public class TestData
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
        }

        [Fact]
        public async Task StoreAsync_ShouldSerializeAndStore()
        {
            // Arrange
            var testData = new TestData { Id = "1", Name = "Test", Value = 42 };
            var serializedBytes = Encoding.UTF8.GetBytes("serialized");
            var expectedHash = "hash123";

            _mockSerializer.Setup(s => s.Serialize(testData)).Returns(serializedBytes);
            _mockByteStorage.Setup(s => s.StoreAsync(serializedBytes)).ReturnsAsync(expectedHash);

            // Act
            var result = await _typedStorage.StoreAsync(testData);

            // Assert
            Assert.Equal(expectedHash, result);
            _mockSerializer.Verify(s => s.Serialize(testData), Times.Once);
            _mockByteStorage.Verify(s => s.StoreAsync(serializedBytes), Times.Once);
        }

        [Fact]
        public async Task StoreAsync_WithNullContent_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await _typedStorage.StoreAsync(null!));
        }

        [Fact]
        public async Task GetAsync_WhenDataExists_ShouldRetrieveAndDeserialize()
        {
            // Arrange
            var hash = "hash123";
            var storedBytes = Encoding.UTF8.GetBytes("stored");
            var expectedData = new TestData { Id = "1", Name = "Test", Value = 42 };

            _mockByteStorage.Setup(s => s.GetAsync(hash)).ReturnsAsync(storedBytes);
            _mockSerializer.Setup(s => s.Deserialize(storedBytes)).Returns(expectedData);

            // Act
            var result = await _typedStorage.GetAsync(hash);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedData.Id, result.Id);
            Assert.Equal(expectedData.Name, result.Name);
            Assert.Equal(expectedData.Value, result.Value);
            _mockByteStorage.Verify(s => s.GetAsync(hash), Times.Once);
            _mockSerializer.Verify(s => s.Deserialize(storedBytes), Times.Once);
        }

        [Fact]
        public async Task GetAsync_WhenDataDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            var hash = "nonexistent";
            _mockByteStorage.Setup(s => s.GetAsync(hash)).ReturnsAsync((byte[]?)null);

            // Act
            var result = await _typedStorage.GetAsync(hash);

            // Assert
            Assert.Null(result);
            _mockByteStorage.Verify(s => s.GetAsync(hash), Times.Once);
            _mockSerializer.Verify(s => s.Deserialize(It.IsAny<byte[]>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetAsync_WithInvalidHash_ShouldThrowArgumentException(string invalidHash)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _typedStorage.GetAsync(invalidHash));
        }

        [Fact]
        public async Task ExistsAsync_ShouldDelegateToByteStorage()
        {
            // Arrange
            var hash = "hash123";
            _mockByteStorage.Setup(s => s.ExistsAsync(hash)).ReturnsAsync(true);

            // Act
            var result = await _typedStorage.ExistsAsync(hash);

            // Assert
            Assert.True(result);
            _mockByteStorage.Verify(s => s.ExistsAsync(hash), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExistsAsync_WithInvalidHash_ShouldThrowArgumentException(string invalidHash)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _typedStorage.ExistsAsync(invalidHash));
        }

        [Fact]
        public async Task DeleteAsync_ShouldDelegateToByteStorage()
        {
            // Arrange
            var hash = "hash123";
            _mockByteStorage.Setup(s => s.DeleteAsync(hash)).ReturnsAsync(true);

            // Act
            var result = await _typedStorage.DeleteAsync(hash);

            // Assert
            Assert.True(result);
            _mockByteStorage.Verify(s => s.DeleteAsync(hash), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task DeleteAsync_WithInvalidHash_ShouldThrowArgumentException(string invalidHash)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _typedStorage.DeleteAsync(invalidHash));
        }

        [Fact]
        public async Task GetAllHashesAsync_ShouldDelegateToByteStorage()
        {
            // Arrange
            var expectedHashes = new List<string> { "hash1", "hash2", "hash3" };
            _mockByteStorage.Setup(s => s.GetAllHashesAsync())
                .Returns(expectedHashes.ToAsyncEnumerable());

            // Act
            var result = new List<string>();
            await foreach (var hash in _typedStorage.GetAllHashesAsync())
            {
                result.Add(hash);
            }

            // Assert
            Assert.Equal(expectedHashes, result);
            _mockByteStorage.Verify(s => s.GetAllHashesAsync(), Times.Once);
        }

        [Fact]
        public async Task GetSizeAsync_ShouldDelegateToByteStorage()
        {
            // Arrange
            var hash = "hash123";
            var expectedSize = 1024L;
            _mockByteStorage.Setup(s => s.GetSizeAsync(hash)).ReturnsAsync(expectedSize);

            // Act
            var result = await _typedStorage.GetSizeAsync(hash);

            // Assert
            Assert.Equal(expectedSize, result);
            _mockByteStorage.Verify(s => s.GetSizeAsync(hash), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetSizeAsync_WithInvalidHash_ShouldThrowArgumentException(string invalidHash)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _typedStorage.GetSizeAsync(invalidHash));
        }

        [Fact]
        public async Task GetTotalSizeAsync_ShouldDelegateToByteStorage()
        {
            // Arrange
            var expectedSize = 10240L;
            _mockByteStorage.Setup(s => s.GetTotalSizeAsync()).ReturnsAsync(expectedSize);

            // Act
            var result = await _typedStorage.GetTotalSizeAsync();

            // Assert
            Assert.Equal(expectedSize, result);
            _mockByteStorage.Verify(s => s.GetTotalSizeAsync(), Times.Once);
        }

        [Fact]
        public async Task ClearAsync_ShouldDelegateToByteStorage()
        {
            // Arrange
            _mockByteStorage.Setup(s => s.ClearAsync()).Returns(ValueTask.CompletedTask);

            // Act
            await _typedStorage.ClearAsync();

            // Assert
            _mockByteStorage.Verify(s => s.ClearAsync(), Times.Once);
        }

        [Fact]
        public async Task StoreAsync_WhenSerializationFails_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var testData = new TestData { Id = "1", Name = "Test", Value = 42 };
            _mockSerializer.Setup(s => s.Serialize(testData)).Throws(new Exception("Serialization failed"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await _typedStorage.StoreAsync(testData));
            Assert.Contains("Failed to store content of type TestData", exception.Message);
            Assert.NotNull(exception.InnerException);
        }

        [Fact]
        public async Task GetAsync_WhenDeserializationFails_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var hash = "hash123";
            var storedBytes = Encoding.UTF8.GetBytes("stored");
            _mockByteStorage.Setup(s => s.GetAsync(hash)).ReturnsAsync(storedBytes);
            _mockSerializer.Setup(s => s.Deserialize(storedBytes)).Throws(new Exception("Deserialization failed"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await _typedStorage.GetAsync(hash));
            Assert.Contains("Failed to retrieve content of type TestData", exception.Message);
            Assert.NotNull(exception.InnerException);
        }

        [Fact]
        public void Constructor_WithNullByteStorage_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new TypedContentAddressableStorage<TestData>(null!, _mockSerializer.Object));
        }

        [Fact]
        public void Constructor_WithNullSerializer_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new TypedContentAddressableStorage<TestData>(_mockByteStorage.Object, null!));
        }
    }

    // Helper extension for converting IEnumerable to IAsyncEnumerable
    public static class AsyncEnumerableExtensions
    {
        public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return item;
                await Task.CompletedTask;
            }
        }
    }
}