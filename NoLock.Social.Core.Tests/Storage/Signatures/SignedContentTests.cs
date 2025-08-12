using FluentAssertions;
using NoLock.Social.Core.Storage.Signatures;
using System;

namespace NoLock.Social.Core.Tests.Storage.Signatures;

public class SignedContentTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var hash = "abc123hash";
        var signature = new byte[] { 1, 2, 3, 4 };
        var algorithm = "RSA-SHA256";
        var publicKeyId = "key123";
        var signedAt = DateTime.UtcNow;

        // Act
        var signedContent = new SignedContent(hash, signature, algorithm, publicKeyId, signedAt);

        // Assert
        signedContent.ContentHash.Should().Be(hash);
        signedContent.Signature.Should().BeEquivalentTo(signature);
        signedContent.Algorithm.Should().Be(algorithm);
        signedContent.SignerPublicKeyId.Should().Be(publicKeyId);
        signedContent.SignedAt.Should().Be(signedAt);
    }

    [Fact]
    public void Constructor_WithoutOptionalParameters_UsesDefaults()
    {
        // Arrange
        var hash = "xyz789hash";
        var signature = new byte[] { 5, 6, 7, 8 };
        var algorithm = "ECDSA";
        var beforeCreation = DateTime.UtcNow;

        // Act
        var signedContent = new SignedContent(hash, signature, algorithm);
        var afterCreation = DateTime.UtcNow;

        // Assert
        signedContent.ContentHash.Should().Be(hash);
        signedContent.Signature.Should().BeEquivalentTo(signature);
        signedContent.Algorithm.Should().Be(algorithm);
        signedContent.SignerPublicKeyId.Should().BeNull();
        signedContent.SignedAt.Should().BeOnOrAfter(beforeCreation).And.BeOnOrBefore(afterCreation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_WithInvalidHash_ThrowsArgumentException(string? invalidHash)
    {
        // Arrange
        var signature = new byte[] { 1, 2, 3 };
        var algorithm = "RSA";

        // Act
        var act = () => new SignedContent(invalidHash!, signature, algorithm);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Content hash cannot be null or empty*")
            .WithParameterName("contentHash");
    }

    [Fact]
    public void Constructor_WithNullSignature_ThrowsArgumentException()
    {
        // Arrange
        var hash = "validhash";
        var algorithm = "RSA";

        // Act
        var act = () => new SignedContent(hash, null!, algorithm);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Signature cannot be null or empty*")
            .WithParameterName("signature");
    }

    [Fact]
    public void Constructor_WithEmptySignature_ThrowsArgumentException()
    {
        // Arrange
        var hash = "validhash";
        var signature = Array.Empty<byte>();
        var algorithm = "RSA";

        // Act
        var act = () => new SignedContent(hash, signature, algorithm);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Signature cannot be null or empty*")
            .WithParameterName("signature");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidAlgorithm_ThrowsArgumentException(string? invalidAlgorithm)
    {
        // Arrange
        var hash = "validhash";
        var signature = new byte[] { 1, 2, 3 };

        // Act
        var act = () => new SignedContent(hash, signature, invalidAlgorithm!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Algorithm cannot be null or empty*")
            .WithParameterName("algorithm");
    }

    [Fact]
    public void Constructor_CreatesDefensiveCopyOfSignature()
    {
        // Arrange
        var hash = "hash";
        var originalSignature = new byte[] { 1, 2, 3, 4 };
        var algorithm = "RSA";

        // Act
        var signedContent = new SignedContent(hash, originalSignature, algorithm);
        originalSignature[0] = 99; // Modify original array

        // Assert
        signedContent.Signature[0].Should().Be(1); // Should not be affected
    }

    [Fact]
    public void SignedContent_IsImmutable()
    {
        // Arrange
        var signedContent = new SignedContent("hash", new byte[] { 1 }, "RSA");

        // Act & Assert
        signedContent.GetType().GetProperty("ContentHash")!.CanWrite.Should().BeFalse();
        signedContent.GetType().GetProperty("Signature")!.CanWrite.Should().BeFalse();
        signedContent.GetType().GetProperty("Algorithm")!.CanWrite.Should().BeFalse();
        signedContent.GetType().GetProperty("SignerPublicKeyId")!.CanWrite.Should().BeFalse();
        signedContent.GetType().GetProperty("SignedAt")!.CanWrite.Should().BeFalse();
    }
}