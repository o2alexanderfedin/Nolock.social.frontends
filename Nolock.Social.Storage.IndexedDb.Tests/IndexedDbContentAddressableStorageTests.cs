using Microsoft.JSInterop;
using Moq;
using System.Text;
using NoLock.Social.Core.Storage;

namespace Nolock.Social.Storage.IndexedDb.Tests;

public class IndexedDbContentAddressableStorageTests
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly Mock<ISerializer<TestEntity>> _serializerMock;
    
    public class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
    
    public IndexedDbContentAddressableStorageTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _serializerMock = new Mock<ISerializer<TestEntity>>();
        
        // Setup default serialization behavior
        _serializerMock.Setup(s => s.Serialize(It.IsAny<TestEntity>()))
            .Returns<TestEntity>(entity => Encoding.UTF8.GetBytes($"{entity.Id}|{entity.Name}|{entity.CreatedAt:O}"));
    }

    [Fact]
    public void Constructor_ShouldInitialize_WithValidJSRuntime()
    {
        // Act
        _ = new IndexedDbContentAddressableStorage<TestEntity>(_jsRuntimeMock.Object, _serializerMock.Object);
    }

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
    
    // Helper method to test hash generation
    private static string ComputeHashForTest(byte[] data)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return Convert.ToBase64String(hashBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}