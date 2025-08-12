using FluentAssertions;
using Moq;
using NoLock.Social.Core.Storage;
using NoLock.Social.Core.Storage.Metadata;
using System;
using System.Threading.Tasks;
using TG.Blazor.IndexedDB;

namespace NoLock.Social.Core.Tests.Storage.Metadata;

public class IndexedDBMetadataStoreTests
{
    private readonly Mock<IIndexedDBManagerWrapper> _dbManagerMock;
    private readonly IndexedDBMetadataStore<ContentMetadata> _sut;
    private const string StoreName = "test_metadata_store";

    public IndexedDBMetadataStoreTests()
    {
        _dbManagerMock = new Mock<IIndexedDBManagerWrapper>();
        _sut = new IndexedDBMetadataStore<ContentMetadata>(_dbManagerMock.Object, StoreName);
    }

    [Fact]
    public void Constructor_WithNullDbManager_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new IndexedDBMetadataStore<ContentMetadata>(null!, StoreName);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("dbManager");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidStoreName_ThrowsArgumentException(string? invalidStoreName)
    {
        // Act
        var act = () => new IndexedDBMetadataStore<ContentMetadata>(_dbManagerMock.Object, invalidStoreName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Store name cannot be null or empty*")
            .WithParameterName("storeName");
    }

    [Fact]
    public async Task GetMetadataAsync_WithExistingHash_ReturnsMetadata()
    {
        // Arrange
        var hash = "test-hash";
        var metadata = new ContentMetadata(new ContentReference("ref-hash", "image/png"));
        var entry = new MetadataEntry<ContentMetadata>
        {
            ContentHash = hash,
            Metadata = metadata,
            StoredAt = DateTime.UtcNow
        };

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, MetadataEntry<ContentMetadata>>(StoreName, hash))
            .ReturnsAsync(entry);

        // Act
        var result = await _sut.GetMetadataAsync(hash);

        // Assert
        result.Should().Be(metadata);
    }

    [Fact]
    public async Task GetMetadataAsync_WithNonExistingHash_ReturnsNull()
    {
        // Arrange
        var hash = "non-existing-hash";

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, MetadataEntry<ContentMetadata>>(StoreName, hash))
            .ReturnsAsync((MetadataEntry<ContentMetadata>?)null);

        // Act
        var result = await _sut.GetMetadataAsync(hash);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMetadataAsync_WhenExceptionThrown_ReturnsNull()
    {
        // Arrange
        var hash = "error-hash";

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, MetadataEntry<ContentMetadata>>(StoreName, hash))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _sut.GetMetadataAsync(hash);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetMetadataAsync_WithInvalidHash_ThrowsArgumentException(string? invalidHash)
    {
        // Act
        Func<Task> act = async () => await _sut.GetMetadataAsync(invalidHash!);

        // Assert
        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Content hash cannot be null or empty*")
            .WithParameterName("contentHash");
    }

    [Fact]
    public async Task StoreMetadataAsync_WithValidData_StoresSuccessfully()
    {
        // Arrange
        var hash = "test-hash";
        var metadata = new ContentMetadata(new ContentReference("ref-hash", "text/plain"));

        _dbManagerMock
            .Setup(x => x.AddRecord(It.IsAny<StoreRecord<MetadataEntry<ContentMetadata>>>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.StoreMetadataAsync(hash, metadata);

        // Assert
        _dbManagerMock.Verify(x => x.AddRecord(It.Is<StoreRecord<MetadataEntry<ContentMetadata>>>(
            r => r.Storename == StoreName &&
                 r.Data.ContentHash == hash &&
                 r.Data.Metadata == metadata
        )), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void StoreMetadataAsync_WithInvalidHash_ThrowsArgumentException(string? invalidHash)
    {
        // Arrange
        var metadata = new ContentMetadata(new ContentReference("ref-hash"));

        // Act
        Func<Task> act = async () => await _sut.StoreMetadataAsync(invalidHash!, metadata);

        // Assert
        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Content hash cannot be null or empty*")
            .WithParameterName("contentHash");
    }

    [Fact]
    public void StoreMetadataAsync_WithNullMetadata_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _sut.StoreMetadataAsync("hash", null!);

        // Assert
        act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("metadata");
    }

    [Fact]
    public async Task DeleteMetadataAsync_WhenSuccessful_ReturnsTrue()
    {
        // Arrange
        var hash = "hash-to-delete";

        _dbManagerMock
            .Setup(x => x.DeleteRecord(StoreName, hash))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.DeleteMetadataAsync(hash);

        // Assert
        result.Should().BeTrue();
        _dbManagerMock.Verify(x => x.DeleteRecord(StoreName, hash), Times.Once);
    }

    [Fact]
    public async Task DeleteMetadataAsync_WhenExceptionThrown_ReturnsFalse()
    {
        // Arrange
        var hash = "error-hash";

        _dbManagerMock
            .Setup(x => x.DeleteRecord(StoreName, hash))
            .ThrowsAsync(new Exception("Delete failed"));

        // Act
        var result = await _sut.DeleteMetadataAsync(hash);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void DeleteMetadataAsync_WithInvalidHash_ThrowsArgumentException(string? invalidHash)
    {
        // Act
        Func<Task> act = async () => await _sut.DeleteMetadataAsync(invalidHash!);

        // Assert
        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Content hash cannot be null or empty*")
            .WithParameterName("contentHash");
    }

    [Fact]
    public async Task ExistsAsync_WithExistingHash_ReturnsTrue()
    {
        // Arrange
        var hash = "existing-hash";
        var entry = new MetadataEntry<ContentMetadata>
        {
            ContentHash = hash,
            Metadata = new ContentMetadata(new ContentReference("ref"))
        };

        _dbManagerMock
            .Setup(x => x.GetRecordById<string, MetadataEntry<ContentMetadata>>(StoreName, hash))
            .ReturnsAsync(entry);

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
            .Setup(x => x.GetRecordById<string, MetadataEntry<ContentMetadata>>(StoreName, hash))
            .ReturnsAsync((MetadataEntry<ContentMetadata>?)null);

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
            .Setup(x => x.GetRecordById<string, MetadataEntry<ContentMetadata>>(StoreName, hash))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _sut.ExistsAsync(hash);

        // Assert
        result.Should().BeFalse();
    }
}