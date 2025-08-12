using FluentAssertions;
using Moq;
using NoLock.Social.Core.Hashing;
using NoLock.Social.Core.Storage;
using System.Text;
using TG.Blazor.IndexedDB;

namespace NoLock.Social.Core.Tests.Storage;

public class IndexedDBContentAddressableStorageTests
{
    private readonly Mock<IIndexedDBManagerWrapper> _dbManagerMock;
    private readonly Mock<IHashAlgorithm> _hashAlgorithmMock;
    private readonly IndexedDBContentAddressableStorage _sut;
    private const string StoreName = "content_addressable_storage";

    public IndexedDBContentAddressableStorageTests()
    {
        _dbManagerMock = new Mock<IIndexedDBManagerWrapper>();
        _hashAlgorithmMock = new Mock<IHashAlgorithm>();
        _sut = new IndexedDBContentAddressableStorage(_dbManagerMock.Object, _hashAlgorithmMock.Object);
    }

    [Fact]
    public async Task StoreAsync_WithNewContent_StoresAndReturnsHash()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("Test content");
        var hashBytes = new byte[] { 1, 2, 3, 4 };
        var expectedHash = "AQIDBA"; // Base64 of [1,2,3,4] without padding

        _hashAlgorithmMock
            .Setup(x => x.ComputeHashAsync(content))
            .ReturnsAsync(hashBytes);

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, It.IsAny<string>()))
            .ReturnsAsync((ContentEntry?)null);

        _dbManagerMock
            .Setup(x => x.AddRecord(It.IsAny<StoreRecord<ContentEntry>>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.StoreAsync(content);

        // Assert
        result.Should().Be(expectedHash);
        _dbManagerMock.Verify(x => x.AddRecord(It.Is<StoreRecord<ContentEntry>>(
            r => r.Storename == StoreName &&
                 r.Data.Hash == expectedHash &&
                 r.Data.Content.SequenceEqual(content)
        )), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_WithExistingContent_ReturnsHashWithoutStoring()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("Existing content");
        var hashBytes = new byte[] { 5, 6, 7, 8 };
        var expectedHash = "BQYHCA"; // Base64 of [5,6,7,8] without padding

        var existingEntry = new ContentEntry
        {
            Hash = expectedHash,
            Content = content
        };

        _hashAlgorithmMock
            .Setup(x => x.ComputeHashAsync(content))
            .ReturnsAsync(hashBytes);

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, expectedHash))
            .ReturnsAsync(existingEntry);

        // Act
        var result = await _sut.StoreAsync(content);

        // Assert
        result.Should().Be(expectedHash);
        _dbManagerMock.Verify(x => x.AddRecord(It.IsAny<StoreRecord<ContentEntry>>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_WithExistingHash_ReturnsContent()
    {
        // Arrange
        var hash = "test-hash";
        var expectedContent = Encoding.UTF8.GetBytes("Retrieved content");
        var contentEntry = new ContentEntry
        {
            Hash = hash,
            Content = expectedContent
        };

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, hash))
            .ReturnsAsync(contentEntry);

        // Act
        var result = await _sut.GetAsync(hash);

        // Assert
        result.Should().BeEquivalentTo(expectedContent);
    }

    [Fact]
    public async Task GetAsync_WithNonExistingHash_ReturnsNull()
    {
        // Arrange
        var hash = "non-existing-hash";

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, hash))
            .ReturnsAsync((ContentEntry?)null);

        // Act
        var result = await _sut.GetAsync(hash);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenExceptionThrown_ReturnsNull()
    {
        // Arrange
        var hash = "error-hash";

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, hash))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _sut.GetAsync(hash);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingHash_ReturnsTrue()
    {
        // Arrange
        var hash = "existing-hash";
        var contentEntry = new ContentEntry
        {
            Hash = hash,
            Content = new byte[] { 1, 2, 3 }
        };

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, hash))
            .ReturnsAsync(contentEntry);

        // Act
        var result = await _sut.ExistsAsync(hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingHash_ReturnsFalse()
    {
        // Arrange
        var hash = "non-existing-hash";

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, hash))
            .ReturnsAsync((ContentEntry?)null);

        // Act
        var result = await _sut.ExistsAsync(hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenExceptionThrown_ReturnsFalse()
    {
        // Arrange
        var hash = "error-hash";

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, hash))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _sut.ExistsAsync(hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenSuccessful_ReturnsTrue()
    {
        // Arrange
        var hash = "hash-to-delete";

        _dbManagerMock
            .Setup(x => x.DeleteRecord(StoreName, hash))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.DeleteAsync(hash);

        // Assert
        result.Should().BeTrue();
        _dbManagerMock.Verify(x => x.DeleteRecord(StoreName, hash), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenExceptionThrown_ReturnsFalse()
    {
        // Arrange
        var hash = "error-hash";

        _dbManagerMock
            .Setup(x => x.DeleteRecord(StoreName, hash))
            .ThrowsAsync(new Exception("Delete failed"));

        // Act
        var result = await _sut.DeleteAsync(hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllHashesAsync_WithMultipleEntries_ReturnsAllHashes()
    {
        // Arrange
        var entries = new List<ContentEntry>
        {
            new() { Hash = "hash1", Content = new byte[] { 1 } },
            new() { Hash = "hash2", Content = new byte[] { 2 } },
            new() { Hash = "hash3", Content = new byte[] { 3 } }
        };

        _dbManagerMock
            .Setup(x => x.GetRecords<ContentEntry>(StoreName))
            .ReturnsAsync(entries);

        // Act
        var result = new List<string>();
        await foreach (var hash in _sut.GetAllHashesAsync())
        {
            result.Add(hash);
        }

        // Assert
        result.Should().BeEquivalentTo(new[] { "hash1", "hash2", "hash3" });
    }

    [Fact]
    public async Task GetAllHashesAsync_WithNoEntries_ReturnsEmpty()
    {
        // Arrange
        _dbManagerMock
            .Setup(x => x.GetRecords<ContentEntry>(StoreName))
            .ReturnsAsync(new List<ContentEntry>());

        // Act
        var result = new List<string>();
        await foreach (var hash in _sut.GetAllHashesAsync())
        {
            result.Add(hash);
        }

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllHashesAsync_WhenNullReturned_ReturnsEmpty()
    {
        // Arrange
        _dbManagerMock
            .Setup(x => x.GetRecords<ContentEntry>(StoreName))
            .ReturnsAsync((List<ContentEntry>?)null);

        // Act
        var result = new List<string>();
        await foreach (var hash in _sut.GetAllHashesAsync())
        {
            result.Add(hash);
        }

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSizeAsync_WithExistingHash_ReturnsContentSize()
    {
        // Arrange
        var hash = "test-hash";
        var content = new byte[] { 1, 2, 3, 4, 5 };
        var contentEntry = new ContentEntry
        {
            Hash = hash,
            Content = content
        };

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, hash))
            .ReturnsAsync(contentEntry);

        // Act
        var result = await _sut.GetSizeAsync(hash);

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public async Task GetSizeAsync_WithNonExistingHash_ReturnsZero()
    {
        // Arrange
        var hash = "non-existing-hash";

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, hash))
            .ReturnsAsync((ContentEntry?)null);

        // Act
        var result = await _sut.GetSizeAsync(hash);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetSizeAsync_WhenExceptionThrown_ReturnsZero()
    {
        // Arrange
        var hash = "error-hash";

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, hash))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _sut.GetSizeAsync(hash);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetTotalSizeAsync_WithMultipleEntries_ReturnsTotalSize()
    {
        // Arrange
        var entries = new List<ContentEntry>
        {
            new() { Hash = "hash1", Content = new byte[10] },
            new() { Hash = "hash2", Content = new byte[20] },
            new() { Hash = "hash3", Content = new byte[30] }
        };

        _dbManagerMock
            .Setup(x => x.GetRecords<ContentEntry>(StoreName))
            .ReturnsAsync(entries);

        // Act
        var result = await _sut.GetTotalSizeAsync();

        // Assert
        result.Should().Be(60);
    }

    [Fact]
    public async Task GetTotalSizeAsync_WithNoEntries_ReturnsZero()
    {
        // Arrange
        _dbManagerMock
            .Setup(x => x.GetRecords<ContentEntry>(StoreName))
            .ReturnsAsync(new List<ContentEntry>());

        // Act
        var result = await _sut.GetTotalSizeAsync();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetTotalSizeAsync_WhenNullReturned_ReturnsZero()
    {
        // Arrange
        _dbManagerMock
            .Setup(x => x.GetRecords<ContentEntry>(StoreName))
            .ReturnsAsync((List<ContentEntry>?)null);

        // Act
        var result = await _sut.GetTotalSizeAsync();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ClearAsync_CallsClearStore()
    {
        // Arrange
        _dbManagerMock
            .Setup(x => x.ClearStore(StoreName))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ClearAsync();

        // Assert
        _dbManagerMock.Verify(x => x.ClearStore(StoreName), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_ConvertsHashToUrlSafeBase64()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("Test");
        // Hash bytes that would produce special characters in base64
        var hashBytes = new byte[] { 255, 62, 63, 254 }; // Will have + and / in base64
        var expectedHash = "_z4__g"; // URL-safe version without padding (/ becomes _)

        _hashAlgorithmMock
            .Setup(x => x.ComputeHashAsync(content))
            .ReturnsAsync(hashBytes);

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, It.IsAny<string>()))
            .ReturnsAsync((ContentEntry?)null);

        // Act
        var result = await _sut.StoreAsync(content);

        // Assert
        result.Should().Be(expectedHash);
        result.Should().NotContain("+");
        result.Should().NotContain("/");
        result.Should().NotContain("=");
    }

    [Fact]
    public async Task StoreAsync_WithEmptyContent_StoresAndReturnsHash()
    {
        // Arrange
        var content = Array.Empty<byte>();
        var hashBytes = new byte[] { 0, 0, 0, 0 };
        var expectedHash = "AAAAAA";

        _hashAlgorithmMock
            .Setup(x => x.ComputeHashAsync(content))
            .ReturnsAsync(hashBytes);

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, It.IsAny<string>()))
            .ReturnsAsync((ContentEntry?)null);

        // Act
        var result = await _sut.StoreAsync(content);

        // Assert
        result.Should().Be(expectedHash);
        _dbManagerMock.Verify(x => x.AddRecord(It.Is<StoreRecord<ContentEntry>>(
            r => r.Data.Content.Length == 0
        )), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_WithLargeContent_HandlesCorrectly()
    {
        // Arrange
        var content = new byte[1024 * 1024]; // 1MB
        Random.Shared.NextBytes(content);
        var hashBytes = new byte[] { 10, 20, 30, 40 };
        var expectedHash = "ChQeKA";

        _hashAlgorithmMock
            .Setup(x => x.ComputeHashAsync(content))
            .ReturnsAsync(hashBytes);

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, ContentEntry>(StoreName, It.IsAny<string>()))
            .ReturnsAsync((ContentEntry?)null);

        // Act
        var result = await _sut.StoreAsync(content);

        // Assert
        result.Should().Be(expectedHash);
        _dbManagerMock.Verify(x => x.AddRecord(It.Is<StoreRecord<ContentEntry>>(
            r => r.Data.Content.Length == 1024 * 1024
        )), Times.Once);
    }
}