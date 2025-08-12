using FluentAssertions;
using NoLock.Social.Core.Storage;
using System;

namespace NoLock.Social.Core.Tests.Storage;

public class ContentReferenceTests
{
    [Fact]
    public void Constructor_WithValidHash_CreatesInstance()
    {
        // Arrange
        var hash = "abc123hash";
        var mimeType = "image/jpeg";

        // Act
        var reference = new ContentReference(hash, mimeType);

        // Assert
        reference.Hash.Should().Be(hash);
        reference.MimeType.Should().Be(mimeType);
    }

    [Fact]
    public void Constructor_WithoutMimeType_CreatesInstanceWithNullMimeType()
    {
        // Arrange
        var hash = "xyz789hash";

        // Act
        var reference = new ContentReference(hash);

        // Assert
        reference.Hash.Should().Be(hash);
        reference.MimeType.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Constructor_WithInvalidHash_ThrowsArgumentException(string? invalidHash)
    {
        // Act
        var act = () => new ContentReference(invalidHash!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Hash cannot be null or empty*")
            .WithParameterName("hash");
    }

    [Fact]
    public void ContentReference_IsImmutable()
    {
        // Arrange
        var reference = new ContentReference("hash", "text/plain");

        // Act & Assert
        reference.GetType().GetProperty("Hash")!.CanWrite.Should().BeFalse();
        reference.GetType().GetProperty("MimeType")!.CanWrite.Should().BeFalse();
    }

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var reference1 = new ContentReference("hash123", "image/png");
        var reference2 = new ContentReference("hash123", "image/png");

        // Act & Assert
        reference1.Equals(reference2).Should().BeTrue();
        (reference1 == reference2).Should().BeFalse(); // Reference equality
    }

    [Fact]
    public void Equals_WithDifferentHash_ReturnsFalse()
    {
        // Arrange
        var reference1 = new ContentReference("hash1", "image/png");
        var reference2 = new ContentReference("hash2", "image/png");

        // Act & Assert
        reference1.Equals(reference2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentMimeType_ReturnsFalse()
    {
        // Arrange
        var reference1 = new ContentReference("hash", "image/png");
        var reference2 = new ContentReference("hash", "image/jpeg");

        // Act & Assert
        reference1.Equals(reference2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithBothNullMimeType_ReturnsTrue()
    {
        // Arrange
        var reference1 = new ContentReference("hash");
        var reference2 = new ContentReference("hash");

        // Act & Assert
        reference1.Equals(reference2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        // Arrange
        var reference = new ContentReference("hash");

        // Act & Assert
        reference.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_WithSameValues_ReturnsSameHashCode()
    {
        // Arrange
        var reference1 = new ContentReference("hash", "text/plain");
        var reference2 = new ContentReference("hash", "text/plain");

        // Act & Assert
        reference1.GetHashCode().Should().Be(reference2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithDifferentValues_ReturnsDifferentHashCode()
    {
        // Arrange
        var reference1 = new ContentReference("hash1", "text/plain");
        var reference2 = new ContentReference("hash2", "text/plain");

        // Act & Assert
        reference1.GetHashCode().Should().NotBe(reference2.GetHashCode());
    }
}