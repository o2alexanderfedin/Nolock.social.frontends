using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NoLock.Social.Core.Hashing;
using NoLock.Social.Core.Storage;

namespace NoLock.Social.Core.Tests.Hashing;

public class SHA256HashServiceTests
{
    private readonly Mock<IHashAlgorithm> _mockHashAlgorithm;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ISerializer<string>> _mockStringSerializer;
    private readonly SHA256HashService _sut;

    public SHA256HashServiceTests()
    {
        _mockHashAlgorithm = new Mock<IHashAlgorithm>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockStringSerializer = new Mock<ISerializer<string>>();
        
        // Setup service provider to return serializer for string types
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ISerializer<string>)))
            .Returns(_mockStringSerializer.Object);
        
        // Setup default serializer behavior to convert string to UTF8 bytes
        _mockStringSerializer
            .Setup(s => s.Serialize(It.IsAny<string>()))
            .Returns<string>(input => Encoding.UTF8.GetBytes(input));
        
        _sut = new SHA256HashService(_mockHashAlgorithm.Object, _mockServiceProvider.Object);
    }

    [Fact]
    public async Task HashAsync_WithStringInput_ReturnsBase64UrlString()
    {
        // Arrange
        var input = "test data";
        var mockHashBytes = new byte[] { 0x12, 0x34, 0x56, 0x78, 0xAB, 0xCD, 0xEF };
        _mockHashAlgorithm.Setup(x => x.ComputeHashAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(mockHashBytes);

        // Act
        var result = await _sut.HashAsync(input);

        // Assert
        result.Should().Be("EjRWeKvN7w"); // Base64Url format (no padding)
        _mockHashAlgorithm.Verify(x => x.ComputeHashAsync(
            It.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes) == input)), 
            Times.Once);
    }

    [Fact]
    public async Task HashAsync_WithByteArrayInput_ReturnsBase64UrlString()
    {
        // Arrange
        var inputBytes = Encoding.UTF8.GetBytes("test data");
        var mockHashBytes = new byte[] { 0x12, 0x34, 0x56, 0x78, 0xAB, 0xCD, 0xEF };
        _mockHashAlgorithm.Setup(x => x.ComputeHashAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(mockHashBytes);

        // Act
        var result = await _sut.HashAsync(inputBytes);

        // Assert
        result.Should().Be("EjRWeKvN7w"); // Base64Url format (no padding)
        _mockHashAlgorithm.Verify(x => x.ComputeHashAsync(
            It.Is<byte[]>(bytes => bytes.SequenceEqual(inputBytes))), 
            Times.Once);
    }

    [Fact]
    public async Task HashAsync_EnsuresUrlSafeEncoding_ReplacesSpecialCharacters()
    {
        // Arrange
        var input = "test data";
        // Use bytes that will produce base64 with +, /, and = padding
        // FB EF FE produces "++/+" in base64, let's use that pattern
        var mockHashBytes = new byte[] { 0xFB, 0xEF, 0xFE, 0x3F, 0xFF, 0xF5 };
        _mockHashAlgorithm.Setup(x => x.ComputeHashAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(mockHashBytes);

        // Act
        var result = await _sut.HashAsync(input);

        // Assert
        // Base64: "++/+P//1" with padding "=" would be "++/+P//1=="
        // Base64Url: "--_-P__1" with no padding
        result.Should().Be("--_-P__1");
        result.Should().NotContain("+");
        result.Should().NotContain("/");
        result.Should().NotContain("=");
        _mockHashAlgorithm.Verify(x => x.ComputeHashAsync(
            It.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes) == input)), 
            Times.Once);
    }

    [Fact]
    public async Task HashAsync_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await _sut.HashAsync<string>(null!));
        _mockHashAlgorithm.Verify(x => x.ComputeHashAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task HashAsync_WithCustomObject_UsesSerializer()
    {
        // Arrange
        var customObject = new TestObject { Value = "test" };
        var mockSerializer = new Mock<ISerializer<TestObject>>();
        var serializedBytes = Encoding.UTF8.GetBytes("{\"Value\":\"test\"}");
        var mockHashBytes = new byte[] { 0x12, 0x34, 0x56 };
        
        mockSerializer.Setup(s => s.Serialize(customObject)).Returns(serializedBytes);
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ISerializer<TestObject>)))
            .Returns(mockSerializer.Object);
        _mockHashAlgorithm.Setup(x => x.ComputeHashAsync(serializedBytes))
            .ReturnsAsync(mockHashBytes);

        // Act
        var result = await _sut.HashAsync(customObject);

        // Assert
        result.Should().Be("EjRW"); // Base64Url format
        mockSerializer.Verify(s => s.Serialize(customObject), Times.Once);
        _mockHashAlgorithm.Verify(x => x.ComputeHashAsync(serializedBytes), Times.Once);
    }

    [Theory]
    [InlineData(new byte[] { }, "")]  // Empty input
    [InlineData(new byte[] { 0x00 }, "AA")]  // Single null byte
    [InlineData(new byte[] { 0xFF }, "_w")]  // Single max byte
    [InlineData(new byte[] { 0x00, 0x00, 0x00 }, "AAAA")]  // Multiple null bytes
    public async Task HashAsync_WithVariousByteInputs_ReturnsExpectedBase64Url(byte[] input, string expectedBase64Url)
    {
        // Arrange
        _mockHashAlgorithm.Setup(x => x.ComputeHashAsync(input))
            .ReturnsAsync(input); // Use input as hash for predictable test

        // Act
        var result = await _sut.HashAsync(input);

        // Assert
        result.Should().Be(expectedBase64Url);
    }

    [Fact]
    public async Task HashAsync_WhenSerializerNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var customObject = new TestObject { Value = "test" };
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ISerializer<TestObject>)))
            .Returns((object)null!);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _sut.HashAsync(customObject));
    }

    public class TestObject
    {
        public string Value { get; set; } = string.Empty;
    }
}