using FluentAssertions;
using NoLock.Social.Core.Hashing;
using System.Text;

namespace NoLock.Social.Core.Tests.Hashing;

public class SHA256HashAlgorithmTests
{
    private readonly SHA256HashAlgorithm _sut = new();

    [Fact]
    public void ComputeHash_WithValidInput_ReturnsConsistentHash()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("Hello, World!");
        
        // Act
        var hash1 = _sut.ComputeHash(input);
        var hash2 = _sut.ComputeHash(input);
        
        // Assert
        hash1.Should().BeEquivalentTo(hash2);
        hash1.Should().HaveCount(32); // SHA256 produces 32 bytes
    }

    [Fact]
    public void ComputeHash_WithDifferentInputs_ReturnsDifferentHashes()
    {
        // Arrange
        var input1 = Encoding.UTF8.GetBytes("Hello, World!");
        var input2 = Encoding.UTF8.GetBytes("Hello, Universe!");
        
        // Act
        var hash1 = _sut.ComputeHash(input1);
        var hash2 = _sut.ComputeHash(input2);
        
        // Assert
        hash1.Should().NotBeEquivalentTo(hash2);
    }

    [Fact]
    public async Task ComputeHashAsync_WithValidInput_ReturnsConsistentHash()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("Test data");
        
        // Act
        var hash1 = await _sut.ComputeHashAsync(input);
        var hash2 = _sut.ComputeHash(input);
        
        // Assert
        hash1.Should().BeEquivalentTo(hash2);
    }

    [Fact]
    public void AlgorithmName_ReturnsCorrectName()
    {
        // Assert
        _sut.AlgorithmName.Should().Be("SHA256");
    }
}