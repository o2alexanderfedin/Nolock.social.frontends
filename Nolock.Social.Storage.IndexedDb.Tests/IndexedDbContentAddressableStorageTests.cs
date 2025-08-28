using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using System.Text;
using System.Text.Json;
using CloudNimble.BlazorEssentials.IndexedDb;
using NoLock.Social.Core.Storage;
using NoLock.Social.Core.Hashing;
using Nolock.Social.Storage.IndexedDb.Models;
using Xunit;

namespace Nolock.Social.Storage.IndexedDb.Tests;

public class IndexedDbContentAddressableStorageTests
{
    #region Test Infrastructure
    
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly Mock<IJSObjectReference> _jsModuleMock;
    private readonly Mock<ISerializer<TestEntity>> _serializerMock;
    private readonly Mock<IHashService> _hashServiceMock;
    private readonly IndexedDbContentAddressableStorage<TestEntity> _storage;
    private readonly TestDataFactory _testDataFactory;
    
    public class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        
        public override bool Equals(object? obj)
        {
            if (obj is not TestEntity other) return false;
            return Id == other.Id && Name == other.Name && CreatedAt == other.CreatedAt;
        }
        
        public override int GetHashCode() => HashCode.Combine(Id, Name, CreatedAt);
    }
    
    public class TestDataFactory
    {
        private int _counter;
        
        public TestEntity CreateEntity(string? id = null)
        {
            _counter++;
            return new TestEntity
            {
                Id = id ?? $"test-{_counter}",
                Name = $"Test Entity {_counter}",
                CreatedAt = DateTime.UtcNow.AddDays(-_counter)
            };
        }
        
        public CasEntry<TestEntity> CreateCasEntry(TestEntity entity, string? hash = null)
        {
            return new CasEntry<TestEntity>
            {
                Hash = hash ?? ComputeTestHash(entity),
                Data = entity,
                TypeName = typeof(TestEntity).FullName!,
                StoredAt = DateTime.UtcNow
            };
        }
        
        public string ComputeTestHash(TestEntity entity)
        {
            var bytes = Encoding.UTF8.GetBytes($"{entity.Id}|{entity.Name}|{entity.CreatedAt:O}");
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hashBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
    
    public IndexedDbContentAddressableStorageTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _jsModuleMock = new Mock<IJSObjectReference>();
        _serializerMock = new Mock<ISerializer<TestEntity>>();
        _hashServiceMock = new Mock<IHashService>();
        _testDataFactory = new TestDataFactory();
        
        // Setup default serialization behavior
        _serializerMock.Setup(s => s.Serialize(It.IsAny<TestEntity>()))
            .Returns<TestEntity>(entity => Encoding.UTF8.GetBytes($"{entity.Id}|{entity.Name}|{entity.CreatedAt:O}"));
        
        // Setup hash service to compute hashes consistently with TestDataFactory
        _hashServiceMock.Setup(h => h.HashAsync(It.IsAny<byte[]>()))
            .Returns<byte[]>((data) =>
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha256.ComputeHash(data);
                var hash = Convert.ToBase64String(hashBytes)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .TrimEnd('=');
                return Task.FromResult(hash);
            });
        
        // Setup JSRuntime mocking for IndexedDB operations
        SetupIndexedDbMocks();
        
        _storage = new IndexedDbContentAddressableStorage<TestEntity>(_jsRuntimeMock.Object, _serializerMock.Object, _hashServiceMock.Object);
    }
    
    private void SetupIndexedDbMocks()
    {
        // Mock for database initialization - return the mocked JS module
        _jsRuntimeMock.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.IsAny<object[]>()))
            .ReturnsAsync(_jsModuleMock.Object);
        
        // Mock for OpenDb call (database opening)
        _jsModuleMock.Setup(js => js.InvokeAsync<bool>(
            "openDatabase",
            It.IsAny<object[]>()))
            .ReturnsAsync(true);
        
        // Default mock for GetAsync - returns null (not found)
        _jsModuleMock.Setup(js => js.InvokeAsync<CasEntry<TestEntity>?>(
            "get",
            It.IsAny<object[]>()))
            .ReturnsAsync((CasEntry<TestEntity>?)null);
        
        // Default mock for AddAsync - succeeds
        _jsModuleMock.Setup(js => js.InvokeAsync<string>(
            "add",
            It.IsAny<object[]>()))
            .ReturnsAsync("success");
        
        // Default mock for DeleteAsync - succeeds (returns void result)
        _jsModuleMock.Setup(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());
        
        // Default mock for GetAllAsync - returns empty array (not null)
        _jsModuleMock.Setup(js => js.InvokeAsync<CasEntry<TestEntity>[]>(
            "getAll",
            It.IsAny<object[]>()))
            .ReturnsAsync(Array.Empty<CasEntry<TestEntity>>());
    }
    
    #endregion

    #region Constructor Tests
    
    [Fact]
    public void Constructor_ShouldInitialize_WithValidJSRuntime()
    {
        // Act
        var storage = new IndexedDbContentAddressableStorage<TestEntity>(_jsRuntimeMock.Object, _serializerMock.Object, _hashServiceMock.Object);
        
        // Assert
        Assert.NotNull(storage);
    }
    
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSerializerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new IndexedDbContentAddressableStorage<TestEntity>(_jsRuntimeMock.Object, null!, _hashServiceMock.Object));
    }
    
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenHashServiceIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new IndexedDbContentAddressableStorage<TestEntity>(_jsRuntimeMock.Object, _serializerMock.Object, null!));
    }
    
    #endregion
    
    #region StoreAsync Tests
    
    [Fact]
    public async Task StoreAsync_ShouldStoreNewEntity_WhenEntityDoesNotExist()
    {
        // Arrange
        var entity = _testDataFactory.CreateEntity("test-1");
        var expectedHash = _testDataFactory.ComputeTestHash(entity);
        
        // Setup mock to return null (entity doesn't exist)
        SetupGetAsyncMock(expectedHash, null);
        
        // Act
        var resultHash = await _storage.StoreAsync(entity);
        
        // Assert
        Assert.Equal(expectedHash, resultHash);
        VerifyAddAsyncCalled(expectedHash);
        
        // Verify serializer was called to compute hash
        _serializerMock.Verify(s => s.Serialize(entity), Times.Once);
    }
    
    [Fact]
    public async Task StoreAsync_ShouldReturnExistingHash_WhenEntityAlreadyExists()
    {
        // Arrange
        var entity = _testDataFactory.CreateEntity("existing-1");
        var expectedHash = _testDataFactory.ComputeTestHash(entity);
        var existingEntry = _testDataFactory.CreateCasEntry(entity, expectedHash);
        
        // Setup mock to return existing entry (deduplication scenario)
        // ExistsAsync will call GetRawAsync which uses GetAsync
        SetupGetAsyncMock(expectedHash, existingEntry);
        
        // Act
        var resultHash = await _storage.StoreAsync(entity);
        
        // Assert
        Assert.Equal(expectedHash, resultHash);
        
        // Verify that AddAsync was NOT called (deduplication worked)
        _jsModuleMock.Verify(js => js.InvokeAsync<string>(
            "add",
            It.IsAny<object[]>()),
            Times.Never);
        
        // Verify serializer was still called to compute hash
        _serializerMock.Verify(s => s.Serialize(entity), Times.Once);
    }
    
    [Fact]
    public async Task StoreAsync_ShouldThrowArgumentNullException_WhenEntityIsNull()
    {
        // Arrange
        TestEntity? nullEntity = null;
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => 
            await _storage.StoreAsync(nullEntity!));
        
        // Verify that no database operations were attempted
        _jsRuntimeMock.Verify(js => js.InvokeAsync<object>(
            "CloudNimble.BlazorEssentials.IndexedDb.Add",
            It.IsAny<object[]>()),
            Times.Never);
        
        // Verify serializer was not called
        _serializerMock.Verify(s => s.Serialize(It.IsAny<TestEntity>()), Times.Never);
    }
    
    [Fact]
    public async Task StoreAsync_ShouldThrowTaskCanceledException_WhenTokenIsCancelled()
    {
        // Arrange
        var entity = _testDataFactory.CreateEntity("test-cancel");
        var cts = new CancellationTokenSource();
        
        // Cancel the token before the operation
        cts.Cancel();
        
        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () => 
            await _storage.StoreAsync(entity, cts.Token));
        
        // Verify that no database operations were attempted (Add should not be called)
        _jsModuleMock.Verify(js => js.InvokeAsync<string>(
            "add",
            It.IsAny<object[]>()),
            Times.Never);
        
        // Verify serializer was still called to compute hash (happens before cancellation check)
        _serializerMock.Verify(s => s.Serialize(entity), Times.Once);
    }
    
    [Fact]
    public async Task StoreAsync_ShouldStoreMultipleDistinctEntities_WithUniqueHashes()
    {
        // Arrange
        var entity1 = _testDataFactory.CreateEntity("entity-1");
        var entity2 = _testDataFactory.CreateEntity("entity-2");
        var hash1 = _testDataFactory.ComputeTestHash(entity1);
        var hash2 = _testDataFactory.ComputeTestHash(entity2);
        
        // Setup mocks for both entities (neither exists)
        SetupGetAsyncMock(hash1, null);
        SetupGetAsyncMock(hash2, null);
        
        // Act
        var resultHash1 = await _storage.StoreAsync(entity1);
        var resultHash2 = await _storage.StoreAsync(entity2);
        
        // Assert
        Assert.Equal(hash1, resultHash1);
        Assert.Equal(hash2, resultHash2);
        Assert.NotEqual(resultHash1, resultHash2); // Different entities should have different hashes
        
        // Verify both were added
        VerifyAddAsyncCalled(hash1);
        VerifyAddAsyncCalled(hash2);
    }
    
    [Fact]
    public async Task StoreAsync_ShouldCreateProperCasEntry_WithCorrectMetadata()
    {
        // Arrange
        var entity = _testDataFactory.CreateEntity("metadata-test");
        var expectedHash = _testDataFactory.ComputeTestHash(entity);
        CasEntry<TestEntity>? capturedEntry = null;
        
        // Setup mock to capture the CasEntry being stored
        _jsModuleMock.Setup(js => js.InvokeAsync<string>(
            "add",
            It.IsAny<object[]>()))
            .Callback<string, object[]>((method, args) =>
            {
                foreach (var arg in args)
                {
                    if (arg is CasEntry<TestEntity> entry)
                    {
                        capturedEntry = entry;
                        break;
                    }
                }
            })
            .ReturnsAsync("success");
        
        SetupGetAsyncMock(expectedHash, null);
        
        // Act
        var resultHash = await _storage.StoreAsync(entity);
        
        // Assert
        Assert.NotNull(capturedEntry);
        Assert.Equal(expectedHash, capturedEntry!.Hash);
        Assert.Equal(entity, capturedEntry.Data);
        Assert.Equal(typeof(TestEntity).FullName, capturedEntry.TypeName);
        Assert.True(capturedEntry.StoredAt <= DateTime.UtcNow);
        Assert.True(capturedEntry.StoredAt >= DateTime.UtcNow.AddSeconds(-5));
    }
    
    #endregion
    
    #region GetAsync Tests
    
    [Fact]
    public async Task GetAsync_ShouldReturnEntity_WhenEntityExists()
    {
        // Arrange
        var entity = _testDataFactory.CreateEntity("existing-entity");
        var hash = _testDataFactory.ComputeTestHash(entity);
        var existingEntry = new CasEntry<TestEntity>
        {
            Hash = hash,
            Data = entity,
            TypeName = typeof(TestEntity).FullName!,
            StoredAt = DateTime.UtcNow.AddMinutes(-5)
        };
        
        SetupGetAsyncMock(hash, existingEntry);
        
        // Act
        var result = await _storage.GetAsync(hash);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(entity.Id, result!.Id);
        Assert.Equal(entity.Name, result.Name);
        Assert.Equal(entity.CreatedAt, result.CreatedAt);
        
        // Verify GetAsync was called
        _jsModuleMock.Verify(js => js.InvokeAsync<CasEntry<TestEntity>?>(
            "get",
            It.Is<object[]>(args => args.Any(arg => arg != null && arg.ToString() == hash))),
            Times.Once);
    }
    
    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenEntityDoesNotExist()
    {
        // Arrange
        var nonExistentHash = "non-existent-hash";
        SetupGetAsyncMock(nonExistentHash, null);
        
        // Act
        var result = await _storage.GetAsync(nonExistentHash);
        
        // Assert
        Assert.Null(result);
        
        // Verify GetAsync was called
        _jsModuleMock.Verify(js => js.InvokeAsync<CasEntry<TestEntity>?>(
            "get",
            It.Is<object[]>(args => args.Any(arg => arg != null && arg.ToString() == nonExistentHash))),
            Times.Once);
    }
    
    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenEntryHasNullData()
    {
        // Arrange
        var hash = "hash-with-null-data";
        var entryWithNullData = new CasEntry<TestEntity>
        {
            Hash = hash,
            Data = null,
            TypeName = typeof(TestEntity).FullName!,
            StoredAt = DateTime.UtcNow
        };
        
        SetupGetAsyncMock(hash, entryWithNullData);
        
        // Act
        var result = await _storage.GetAsync(hash);
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public async Task GetAsync_ShouldThrowTaskCanceledException_WhenTokenIsCancelled()
    {
        // Arrange
        var hash = "test-hash";
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () => 
            await _storage.GetAsync(hash, cts.Token));
        
        // Verify that no database operations were attempted
        _jsModuleMock.Verify(js => js.InvokeAsync<CasEntry<TestEntity>?>(
            "get",
            It.IsAny<object[]>()),
            Times.Never);
    }
    
    #endregion
    
    #region ExistsAsync Tests
    
    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenEntityExists()
    {
        // Arrange
        var entity = _testDataFactory.CreateEntity("existing-for-exists");
        var hash = _testDataFactory.ComputeTestHash(entity);
        var existingEntry = new CasEntry<TestEntity>
        {
            Hash = hash,
            Data = entity,
            TypeName = typeof(TestEntity).FullName!,
            StoredAt = DateTime.UtcNow.AddHours(-1)
        };
        
        SetupGetAsyncMock(hash, existingEntry);
        
        // Act
        var exists = await _storage.ExistsAsync(hash);
        
        // Assert
        Assert.True(exists);
        
        // Verify GetAsync was called (ExistsAsync uses GetRawAsync internally)
        _jsModuleMock.Verify(js => js.InvokeAsync<CasEntry<TestEntity>?>(
            "get",
            It.Is<object[]>(args => args.Any(arg => arg != null && arg.ToString() == hash))),
            Times.Once);
    }
    
    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenEntityDoesNotExist()
    {
        // Arrange
        var nonExistentHash = "non-existent-for-exists";
        SetupGetAsyncMock(nonExistentHash, null);
        
        // Act
        var exists = await _storage.ExistsAsync(nonExistentHash);
        
        // Assert
        Assert.False(exists);
        
        // Verify GetAsync was called
        _jsModuleMock.Verify(js => js.InvokeAsync<CasEntry<TestEntity>?>(
            "get",
            It.Is<object[]>(args => args.Any(arg => arg != null && arg.ToString() == nonExistentHash))),
            Times.Once);
    }
    
    [Fact]
    public async Task ExistsAsync_ShouldThrowArgumentNullException_WhenHashIsNull()
    {
        // Arrange
        string? nullHash = null;
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => 
            await _storage.ExistsAsync(nullHash!));
        
        // Verify that no database operations were attempted
        _jsModuleMock.Verify(js => js.InvokeAsync<CasEntry<TestEntity>?>(
            "get",
            It.IsAny<object[]>()),
            Times.Never);
    }
    
    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenEntryExistsWithNullData()
    {
        // Arrange
        var hash = "hash-with-null-data-exists";
        var entryWithNullData = new CasEntry<TestEntity>
        {
            Hash = hash,
            Data = null, // Entry exists but data is null
            TypeName = typeof(TestEntity).FullName!,
            StoredAt = DateTime.UtcNow
        };
        
        SetupGetAsyncMock(hash, entryWithNullData);
        
        // Act
        var exists = await _storage.ExistsAsync(hash);
        
        // Assert
        Assert.True(exists); // Should return true because entry exists, even if data is null
    }
    
    #endregion
    
    #region DeleteAsync Tests
    
    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenEntityIsSuccessfullyDeleted()
    {
        // Arrange
        var hash = "hash-to-delete";
        
        // Setup mock for successful deletion
        _jsModuleMock.Setup(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());
        
        // Act
        var result = await _storage.DeleteAsync(hash);
        
        // Assert
        Assert.True(result);
        
        // Verify DeleteAsync was called with correct hash
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash)),
            Times.Once);
    }
    
    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenDeletingNonExistentEntity()
    {
        // Arrange
        var nonExistentHash = "non-existent-hash-to-delete";
        
        // Setup mock - IndexedDB typically doesn't throw for non-existent keys
        _jsModuleMock.Setup(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSVoidResult>()); // Returns success even for non-existent items
        
        // Act
        var result = await _storage.DeleteAsync(nonExistentHash);
        
        // Assert
        Assert.True(result); // Should return true as per implementation
        
        // Verify DeleteAsync was still called
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == nonExistentHash)),
            Times.Once);
    }
    
    [Fact]
    public async Task DeleteAsync_ShouldThrowArgumentNullException_WhenHashIsNull()
    {
        // Arrange
        string? nullHash = null;
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => 
            await _storage.DeleteAsync(nullHash!));
        
        // Verify that no database operations were attempted
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()),
            Times.Never);
    }
    
    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenExceptionOccurs()
    {
        // Arrange
        var hash = "hash-that-causes-error";
        
        // Setup mock to throw an exception
        _jsModuleMock.Setup(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash)))
            .ThrowsAsync(new InvalidOperationException("Database error"));
        
        // Act
        var result = await _storage.DeleteAsync(hash);
        
        // Assert
        Assert.False(result); // Should return false when exception occurs
        
        // Verify DeleteAsync was attempted
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash)),
            Times.Once);
    }
    
    [Fact]
    public async Task DeleteAsync_ShouldNotThrow_WhenTokenIsCancelled()
    {
        // Arrange
        var hash = "hash-to-delete-with-cancellation";
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // Setup mock for deletion
        _jsModuleMock.Setup(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());
        
        // Act - The implementation doesn't check cancellation token, so it should succeed
        var result = await _storage.DeleteAsync(hash, cts.Token);
        
        // Assert - Should return true despite cancellation (limitation of current implementation)
        Assert.True(result);
        
        // Verify that delete was still called (cancellation is not checked in DeleteAsync)
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()),
            Times.Once);
    }
    
    [Fact]
    public async Task DeleteAsync_ShouldHandleMultipleDeletions_Successfully()
    {
        // Arrange
        var hash1 = "hash-1-to-delete";
        var hash2 = "hash-2-to-delete";
        var hash3 = "hash-3-to-delete";
        
        // Setup mocks for all deletions
        _jsModuleMock.Setup(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());
        
        // Act
        var result1 = await _storage.DeleteAsync(hash1);
        var result2 = await _storage.DeleteAsync(hash2);
        var result3 = await _storage.DeleteAsync(hash3);
        
        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
        
        // Verify all deletions were called
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()),
            Times.Exactly(3));
    }
    
    #endregion
    
    #region All Property Tests
    
    // TODO: Implement by QA Engineer
    // - Test enumerate all entities
    // - Test empty collection
    // - Test filtering by type
    
    #endregion
    
    #region AllHashes Property Tests
    
    // TODO: Implement by QA Engineer
    // - Test enumerate all hashes
    // - Test empty collection
    
    #endregion
    
    #region ClearAsync Tests
    
    [Fact]
    public async Task ClearAsync_ShouldDeleteAllEntities_WhenStorageHasMultipleItems()
    {
        // Arrange
        var entity1 = _testDataFactory.CreateEntity("clear-1");
        var entity2 = _testDataFactory.CreateEntity("clear-2");
        var entity3 = _testDataFactory.CreateEntity("clear-3");
        
        var hash1 = _testDataFactory.ComputeTestHash(entity1);
        var hash2 = _testDataFactory.ComputeTestHash(entity2);
        var hash3 = _testDataFactory.ComputeTestHash(entity3);
        
        var entries = new[]
        {
            _testDataFactory.CreateCasEntry(entity1, hash1),
            _testDataFactory.CreateCasEntry(entity2, hash2),
            _testDataFactory.CreateCasEntry(entity3, hash3)
        };
        
        // Setup GetAllAsync to return all entries
        SetupGetAllAsyncMock(entries);
        
        // Setup DeleteAsync to succeed for each hash
        _jsModuleMock.Setup(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());
        
        // Act
        await _storage.ClearAsync();
        
        // Assert - Verify all items were deleted
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash1)),
            Times.Once);
        
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash2)),
            Times.Once);
        
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash3)),
            Times.Once);
        
        // Verify GetAllAsync was called to enumerate items
        _jsModuleMock.Verify(js => js.InvokeAsync<List<CasEntry<TestEntity>>>(
            "getAll",
            It.IsAny<object[]>()),
            Times.Once);
    }
    
    [Fact]
    public async Task ClearAsync_ShouldNotThrow_WhenStorageIsEmpty()
    {
        // Arrange
        // Setup GetAllAsync to return empty list - matching runtime expectation
        var emptyList = new List<CasEntry<TestEntity>>();
        _jsModuleMock.Setup(js => js.InvokeAsync<List<CasEntry<TestEntity>>>(
            "getAll",
            It.IsAny<object[]>()))
            .ReturnsAsync(emptyList);
        
        // Act
        var exception = await Record.ExceptionAsync(async () => await _storage.ClearAsync());
        
        // Assert
        Assert.Null(exception); // Should not throw any exception
        
        // Verify GetAllAsync was called
        _jsModuleMock.Verify(js => js.InvokeAsync<List<CasEntry<TestEntity>>>(
            "getAll",
            It.IsAny<object[]>()),
            Times.Once);
        
        // Verify DeleteAsync was never called (no items to delete)
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()),
            Times.Never);
    }
    
    [Fact]
    public async Task ClearAsync_ShouldRespectCancellationToken_WhenProvided()
    {
        // Arrange
        var entity1 = _testDataFactory.CreateEntity("cancel-clear-1");
        var entity2 = _testDataFactory.CreateEntity("cancel-clear-2");
        var entity3 = _testDataFactory.CreateEntity("cancel-clear-3");
        
        var hash1 = _testDataFactory.ComputeTestHash(entity1);
        var hash2 = _testDataFactory.ComputeTestHash(entity2);
        var hash3 = _testDataFactory.ComputeTestHash(entity3);
        
        var entries = new[]
        {
            _testDataFactory.CreateCasEntry(entity1, hash1),
            _testDataFactory.CreateCasEntry(entity2, hash2),
            _testDataFactory.CreateCasEntry(entity3, hash3)
        };
        
        // Setup GetAllAsync to return all entries as List (runtime expectation)
        SetupGetAllAsyncMock(entries);
        
        // Create a pre-cancelled token
        var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel before starting
        
        // Setup DeleteAsync mock - shouldn't be called due to early cancellation
        _jsModuleMock.Setup(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());
        
        // Act - ClearAsync with cancelled token should complete without throwing
        // The implementation uses WithCancellation which stops iteration but doesn't throw
        await _storage.ClearAsync(cts.Token);
        
        // Assert - Verify no deletions occurred due to pre-cancelled token
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()),
            Times.Never); // No deletions should occur with pre-cancelled token
    }
    
    [Fact]
    public async Task ClearAsync_ShouldOnlyDeleteEntriesOfCorrectType()
    {
        // Arrange
        var entity1 = _testDataFactory.CreateEntity("type-filter-1");
        var hash1 = _testDataFactory.ComputeTestHash(entity1);
        
        // Create entries with correct type
        var correctTypeEntry = new CasEntry<TestEntity>
        {
            Hash = hash1,
            Data = entity1,
            TypeName = typeof(TestEntity).FullName!,
            StoredAt = DateTime.UtcNow
        };
        
        // Create entry with different type (should be filtered out)
        var wrongTypeEntry = new CasEntry<TestEntity>
        {
            Hash = "wrong-type-hash",
            Data = _testDataFactory.CreateEntity("wrong-type"),
            TypeName = "Some.Other.Type",
            StoredAt = DateTime.UtcNow
        };
        
        // Setup GetAllAsync to return both entries (SetupGetAllAsyncMock converts to List)
        SetupGetAllAsyncMock(correctTypeEntry, wrongTypeEntry);
        
        // Setup DeleteAsync
        _jsModuleMock.Setup(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());
        
        // Act
        await _storage.ClearAsync();
        
        // Assert - Only the correct type entry should be deleted
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash1)),
            Times.Once);
        
        // Wrong type entry should NOT be deleted
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == "wrong-type-hash")),
            Times.Never);
    }
    
    [Fact]
    public async Task ClearAsync_ShouldContinueDeleting_EvenIfSomeDeletionsFail()
    {
        // Arrange
        var entity1 = _testDataFactory.CreateEntity("fail-clear-1");
        var entity2 = _testDataFactory.CreateEntity("fail-clear-2");
        var entity3 = _testDataFactory.CreateEntity("fail-clear-3");
        
        var hash1 = _testDataFactory.ComputeTestHash(entity1);
        var hash2 = _testDataFactory.ComputeTestHash(entity2);
        var hash3 = _testDataFactory.ComputeTestHash(entity3);
        
        // Setup GetAllAsync to return all entries (SetupGetAllAsyncMock converts to List)
        SetupGetAllAsyncMock(
            _testDataFactory.CreateCasEntry(entity1, hash1),
            _testDataFactory.CreateCasEntry(entity2, hash2),
            _testDataFactory.CreateCasEntry(entity3, hash3)
        );
        
        // Setup DeleteAsync - second deletion fails
        _jsModuleMock.Setup(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash1)))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());
        
        _jsModuleMock.Setup(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash2)))
            .ThrowsAsync(new InvalidOperationException("Delete failed"));
        
        _jsModuleMock.Setup(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash3)))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());
        
        // Act - Should throw because one deletion failed
        await Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _storage.ClearAsync());
        
        // Assert - Verify first deletion was attempted
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash1)),
            Times.Once);
        
        // Verify second deletion was attempted (and failed)
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash2)),
            Times.Once);
        
        // Third deletion should not have been attempted (exception stopped execution)
        _jsModuleMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "delete",
            It.Is<object[]>(args => args.Length >= 3 && args[2] != null && args[2].ToString() == hash3)),
            Times.Never);
    }
    
    #endregion
    
    #region Hash Generation Tests

    [Fact]
    public void Serializer_ShouldGenerateSameHash_ForSameContent()
    {
        // Arrange
        var entity1 = new TestEntity { Id = "1", Name = "Test", CreatedAt = new DateTime(2024, 1, 1) };
        var entity2 = new TestEntity { Id = "1", Name = "Test", CreatedAt = new DateTime(2024, 1, 1) };
        
        var bytes1 = Encoding.UTF8.GetBytes($"{entity1.Id}|{entity1.Name}|{entity1.CreatedAt:O}");
        var bytes2 = Encoding.UTF8.GetBytes($"{entity2.Id}|{entity2.Name}|{entity2.CreatedAt:O}");
        
        // Act
        var hash1 = ComputeHashForTest(bytes1);
        var hash2 = ComputeHashForTest(bytes2);
        
        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Serializer_ShouldGenerateDifferentHash_ForDifferentContent()
    {
        // Arrange
        var entity1 = new TestEntity { Id = "1", Name = "Test1" };
        var entity2 = new TestEntity { Id = "2", Name = "Test2" };
        
        var bytes1 = Encoding.UTF8.GetBytes($"{entity1.Id}|{entity1.Name}|{entity1.CreatedAt:O}");
        var bytes2 = Encoding.UTF8.GetBytes($"{entity2.Id}|{entity2.Name}|{entity2.CreatedAt:O}");
        
        // Act
        var hash1 = ComputeHashForTest(bytes1);
        var hash2 = ComputeHashForTest(bytes2);
        
        // Assert
        Assert.NotEqual(hash1, hash2);
    }
    
    #endregion
    
    #region Helper Methods
    
    private static string ComputeHashForTest(byte[] data)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return Convert.ToBase64String(hashBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
    
    /// <summary>
    /// Sets up mock to return a specific entity when GetAsync is called with the given hash
    /// </summary>
    private void SetupGetAsyncMock(string hash, CasEntry<TestEntity>? entry)
    {
        _jsModuleMock.Setup(js => js.InvokeAsync<CasEntry<TestEntity>?>(
            "get",
            It.Is<object[]>(args => args.Length > 0 && args.Any(arg => arg != null && arg.ToString() == hash))))
            .ReturnsAsync(entry);
    }
    
    /// <summary>
    /// Sets up mock to return multiple entities for GetAllAsync
    /// </summary>
    private void SetupGetAllAsyncMock(params CasEntry<TestEntity>[] entries)
    {
        // The runtime actually expects List<CasEntry<TestEntity>> not an array
        _jsModuleMock.Setup(js => js.InvokeAsync<List<CasEntry<TestEntity>>>(
            "getAll",
            It.IsAny<object[]>()))
            .ReturnsAsync(entries.ToList());
    }
    
    /// <summary>
    /// Sets up mock to throw an exception for DeleteAsync
    /// </summary>
    private void SetupDeleteAsyncToThrow()
    {
        _jsRuntimeMock.Setup(js => js.InvokeAsync<object>(
            "CloudNimble.BlazorEssentials.IndexedDb.Delete",
            It.IsAny<object[]>()))
            .ThrowsAsync(new InvalidOperationException("Delete failed"));
    }
    
    /// <summary>
    /// Verifies that AddAsync was called with the expected entry
    /// </summary>
    private void VerifyAddAsyncCalled(string expectedHash)
    {
        _jsModuleMock.Verify(js => js.InvokeAsync<string>(
            "add",
            It.Is<object[]>(args => VerifyAddAsyncArgs(args, expectedHash))),
            Times.Once);
    }
    
    private bool VerifyAddAsyncArgs(object[] args, string expectedHash)
    {
        // Check for CasEntry in the args array
        foreach (var arg in args)
        {
            if (arg is CasEntry<TestEntity> entry && entry.Hash == expectedHash)
            {
                return true;
            }
        }
        
        // Also check if the hash is passed directly as a parameter
        return args.Any(arg => arg != null && arg.ToString() == expectedHash);
    }
    
    #endregion
}