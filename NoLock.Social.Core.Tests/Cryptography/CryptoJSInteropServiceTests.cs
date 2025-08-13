using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Core.Tests.Cryptography
{
    public class CryptoJSInteropServiceTests
    {
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly CryptoJSInteropService _sut;

        public CryptoJSInteropServiceTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _sut = new CryptoJSInteropService(_jsRuntimeMock.Object);
        }

        [Fact]
        public async Task InitializeLibsodiumAsync_WhenSuccessful_ReturnsTrue()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("crypto.initializeLibsodium", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.InitializeLibsodiumAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsLibsodiumReadyAsync_WhenReady_ReturnsTrue()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("crypto.isLibsodiumReady", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.IsLibsodiumReadyAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ComputeSha256Async_WithByteArray_ReturnsHash()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("test data");
            var expectedHash = new byte[32]; // SHA-256 produces 32 bytes
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<byte[]>("crypto.sha256", It.IsAny<object[]>()))
                .ReturnsAsync(expectedHash);

            // Act
            var result = await _sut.ComputeSha256Async(data);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(32);
        }

        [Fact]
        public async Task ComputeSha256Async_WithString_ReturnsHash()
        {
            // Arrange
            var data = "test data";
            var expectedHash = new byte[32];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<byte[]>("crypto.sha256", It.IsAny<object[]>()))
                .ReturnsAsync(expectedHash);

            // Act
            var result = await _sut.ComputeSha256Async(data);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(32);
        }

        [Fact]
        public async Task GetRandomBytesAsync_ReturnsRequestedLength()
        {
            // Arrange
            var length = 16;
            var expectedBytes = new byte[length];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<byte[]>("crypto.getRandomBytes", It.IsAny<object[]>()))
                .ReturnsAsync(expectedBytes);

            // Act
            var result = await _sut.GetRandomBytesAsync(length);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(length);
        }

        [Fact]
        public async Task DeriveKeyArgon2idAsync_ReturnsDeterministicKey()
        {
            // Arrange
            var passphrase = "test passphrase";
            var username = "testuser";
            var expectedKey = new byte[32];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<byte[]>("crypto.deriveKeyArgon2id", It.IsAny<object[]>()))
                .ReturnsAsync(expectedKey);

            // Act
            var result = await _sut.DeriveKeyArgon2idAsync(passphrase, username);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(32);
        }

        [Fact]
        public async Task GenerateEd25519KeyPairFromSeedAsync_ReturnsKeyPair()
        {
            // Arrange
            var seed = new byte[32];
            var expectedKeyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<Ed25519KeyPair>("crypto.generateEd25519KeyPairFromSeed", It.IsAny<object[]>()))
                .ReturnsAsync(expectedKeyPair);

            // Act
            var result = await _sut.GenerateEd25519KeyPairFromSeedAsync(seed);

            // Assert
            result.Should().NotBeNull();
            result.PublicKey.Should().NotBeNull();
            result.PrivateKey.Should().NotBeNull();
        }

        [Fact]
        public async Task SignEd25519Async_ReturnsSignature()
        {
            // Arrange
            var data = new byte[100];
            var privateKey = new byte[64];
            var expectedSignature = new byte[64];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<byte[]>("crypto.signEd25519", It.IsAny<object[]>()))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _sut.SignEd25519Async(data, privateKey);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(64);
        }

        [Fact]
        public async Task VerifyEd25519Async_WithValidSignature_ReturnsTrue()
        {
            // Arrange
            var data = new byte[100];
            var signature = new byte[64];
            var publicKey = new byte[32];
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("crypto.verifyEd25519", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.VerifyEd25519Async(data, signature, publicKey);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task BytesToBase64Async_ConvertsCorrectly()
        {
            // Arrange
            var bytes = new byte[] { 1, 2, 3, 4, 5 };
            var expectedBase64 = "AQIDBAU=";
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<string>("crypto.base64Encode", It.IsAny<object[]>()))
                .ReturnsAsync(expectedBase64);

            // Act
            var result = await _sut.BytesToBase64Async(bytes);

            // Assert
            result.Should().Be(expectedBase64);
        }

        [Fact]
        public async Task Base64ToBytesAsync_ConvertsCorrectly()
        {
            // Arrange
            var base64 = "AQIDBAU=";
            var expectedBytes = new byte[] { 1, 2, 3, 4, 5 };
            
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<byte[]>("crypto.base64Decode", It.IsAny<object[]>()))
                .ReturnsAsync(expectedBytes);

            // Act
            var result = await _sut.Base64ToBytesAsync(base64);

            // Assert
            result.Should().BeEquivalentTo(expectedBytes);
        }

        [Fact]
        public async Task ClearMemoryAsync_CallsJSFunction()
        {
            // Arrange
            var data = new byte[10];

            // Act
            await _sut.ClearMemoryAsync(data);

            // Assert
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("crypto.clearMemory", It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public void Ed25519KeyPair_Clear_ZeroesPrivateKey()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[] { 1, 2, 3 },
                PrivateKey = new byte[] { 4, 5, 6 }
            };

            // Act
            keyPair.Clear();

            // Assert
            keyPair.PrivateKey.Should().AllBeEquivalentTo((byte)0);
            keyPair.PublicKey.Should().NotBeNull(); // Public key should not be cleared
            keyPair.PublicKey.Should().ContainInOrder(new byte[] { 1, 2, 3 }); // Public key should retain its values
        }
    }
}