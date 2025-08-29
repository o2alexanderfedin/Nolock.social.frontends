using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.Cryptography.Services
{
    public class WebCryptoServiceTests
    {
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly WebCryptoService _service;

        public WebCryptoServiceTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _service = new WebCryptoService(_jsRuntimeMock.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidJSRuntime_ShouldInitialize()
        {
            // Arrange & Act
            var service = new WebCryptoService(_jsRuntimeMock.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeAssignableTo<IWebCryptoService>();
        }

        [Fact]
        public void Constructor_WithNullJSRuntime_ShouldThrowArgumentNullException()
        {
            // Arrange & Act
            var action = () => new WebCryptoService(null);

            // Assert
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("jsRuntime");
        }

        #endregion

        #region IsAvailableAsync Tests

        [Theory]
        [InlineData(true, true, "Web Crypto is available")]
        [InlineData(false, false, "Web Crypto is not available")]
        public async Task IsAvailableAsync_ShouldReturnExpectedValue(bool jsResult, bool expected, string scenario)
        {
            // Arrange
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<bool>("webCryptoInterop.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(jsResult);

            // Act
            var result = await _service.IsAvailableAsync();

            // Assert
            result.Should().Be(expected, scenario);
        }

        [Fact]
        public async Task IsAvailableAsync_WhenJSThrows_ShouldReturnFalse()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<bool>("webCryptoInterop.isAvailable", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("JavaScript error"));

            // Act
            var result = await _service.IsAvailableAsync();

            // Assert
            result.Should().BeFalse("Should return false when JS throws exception");
        }

        #endregion

        #region GetRandomBytesAsync Tests

        [Theory]
        [InlineData(16, "16 bytes for AES-128")]
        [InlineData(32, "32 bytes for AES-256")]
        [InlineData(64, "64 bytes for SHA-512")]
        [InlineData(128, "128 bytes for larger keys")]
        [InlineData(1, "Single byte")]
        [InlineData(0, "Zero bytes")]
        public async Task GetRandomBytesAsync_WithVariousLengths_ShouldReturnCorrectData(int length, string scenario)
        {
            // Arrange
            var expectedBytes = new byte[length];
            Random.Shared.NextBytes(expectedBytes);
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>(
                    "webCryptoInterop.getRandomValues", 
                    It.Is<object[]>(args => args.Length == 1 && (int)args[0] == length)))
                .ReturnsAsync(expectedBytes);

            // Act
            var result = await _service.GetRandomBytesAsync(length);

            // Assert
            result.Should().BeEquivalentTo(expectedBytes, scenario);
        }

        [Fact]
        public async Task GetRandomBytesAsync_WithNegativeLength_ShouldPassToJS()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>(
                    "webCryptoInterop.getRandomValues",
                    It.Is<object[]>(args => (int)args[0] == -1)))
                .ThrowsAsync(new JSException("Invalid length"));

            // Act
            var action = async () => await _service.GetRandomBytesAsync(-1);

            // Assert
            await action.Should().ThrowAsync<JSException>();
        }

        #endregion

        #region SHA-256 Tests

        [Theory]
        [InlineData(new byte[] { 1, 2, 3 }, "Simple byte array")]
        [InlineData(new byte[] { }, "Empty array")]
        [InlineData(new byte[] { 0xFF, 0x00, 0xFF }, "Mixed bytes")]
        public async Task Sha256Async_WithValidData_ShouldReturnHash(byte[] input, string scenario)
        {
            // Arrange
            var expectedHash = new byte[32]; // SHA-256 produces 32 bytes
            Random.Shared.NextBytes(expectedHash);
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>(
                    "webCryptoInterop.sha256",
                    It.Is<object[]>(args => args.Length == 1 && args[0].Equals(input))))
                .ReturnsAsync(expectedHash);

            // Act
            var result = await _service.Sha256Async(input);

            // Assert
            result.Should().BeEquivalentTo(expectedHash, scenario);
        }

        [Fact]
        public async Task Sha256Async_WithNullData_ShouldThrowArgumentNullException()
        {
            // Arrange & Act
            var action = async () => await _service.Sha256Async(null);

            // Assert
            await action.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("data");
        }

        [Fact]
        public async Task Sha256Async_WhenJSThrows_ShouldPropagateException()
        {
            // Arrange
            var data = new byte[] { 1, 2, 3 };
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>("webCryptoInterop.sha256", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Hashing failed"));

            // Act
            var action = async () => await _service.Sha256Async(data);

            // Assert
            await action.Should().ThrowAsync<JSException>()
                .WithMessage("Hashing failed");
        }

        #endregion

        #region SHA-512 Tests

        [Theory]
        [InlineData(new byte[] { 1, 2, 3 }, "Simple byte array")]
        [InlineData(new byte[] { }, "Empty array")]
        [InlineData(new byte[] { 0xFF, 0x00, 0xFF }, "Mixed bytes")]
        public async Task Sha512Async_WithValidData_ShouldReturnHash(byte[] input, string scenario)
        {
            // Arrange
            var expectedHash = new byte[64]; // SHA-512 produces 64 bytes
            Random.Shared.NextBytes(expectedHash);
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>(
                    "webCryptoInterop.sha512",
                    It.Is<object[]>(args => args.Length == 1 && args[0].Equals(input))))
                .ReturnsAsync(expectedHash);

            // Act
            var result = await _service.Sha512Async(input);

            // Assert
            result.Should().BeEquivalentTo(expectedHash, scenario);
        }

        [Fact]
        public async Task Sha512Async_WithNullData_ShouldThrowArgumentNullException()
        {
            // Arrange & Act
            var action = async () => await _service.Sha512Async(null);

            // Assert
            await action.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("data");
        }

        #endregion

        #region PBKDF2 Tests

        [Theory]
        [InlineData(10000, 32, "SHA-256", "Standard parameters")]
        [InlineData(1, 16, "SHA-256", "Minimum iterations")]
        [InlineData(100000, 64, "SHA-512", "High iterations with SHA-512")]
        [InlineData(5000, 128, "SHA-256", "Large key length")]
        public async Task Pbkdf2Async_WithValidParameters_ShouldReturnDerivedKey(
            int iterations, int keyLength, string hash, string scenario)
        {
            // Arrange
            var password = new byte[] { 1, 2, 3, 4 };
            var salt = new byte[] { 5, 6, 7, 8 };
            var expectedKey = new byte[keyLength];
            Random.Shared.NextBytes(expectedKey);
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>(
                    "webCryptoInterop.pbkdf2",
                    It.Is<object[]>(args => 
                        args.Length == 5 &&
                        args[0].Equals(password) &&
                        args[1].Equals(salt) &&
                        (int)args[2] == iterations &&
                        (int)args[3] == keyLength &&
                        (string)args[4] == hash)))
                .ReturnsAsync(expectedKey);

            // Act
            var result = await _service.Pbkdf2Async(password, salt, iterations, keyLength, hash);

            // Assert
            result.Should().BeEquivalentTo(expectedKey, scenario);
        }

        [Theory]
        [InlineData(null, new byte[] { 1 }, 1000, 32, "password", "Null password")]
        [InlineData(new byte[] { 1 }, null, 1000, 32, "salt", "Null salt")]
        public async Task Pbkdf2Async_WithNullParameters_ShouldThrowArgumentNullException(
            byte[] password, byte[] salt, int iterations, int keyLength, 
            string paramName, string scenario)
        {
            // Arrange & Act
            var action = async () => await _service.Pbkdf2Async(password, salt, iterations, keyLength);

            // Assert
            await action.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName(paramName);
        }

        [Theory]
        [InlineData(0, 32, "iterations", "Zero iterations")]
        [InlineData(-1, 32, "iterations", "Negative iterations")]
        [InlineData(1000, 0, "keyLength", "Zero key length")]
        [InlineData(1000, -1, "keyLength", "Negative key length")]
        public async Task Pbkdf2Async_WithInvalidNumericParameters_ShouldThrowArgumentException(
            int iterations, int keyLength, string paramName, string scenario)
        {
            // Arrange
            var password = new byte[] { 1, 2, 3 };
            var salt = new byte[] { 4, 5, 6 };

            // Act
            var action = async () => await _service.Pbkdf2Async(password, salt, iterations, keyLength);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>()
                .Where(ex => ex.ParamName == paramName);
        }

        [Fact]
        public async Task Pbkdf2Async_WithDefaultHashParameter_ShouldUseSHA256()
        {
            // Arrange
            var password = new byte[] { 1, 2, 3 };
            var salt = new byte[] { 4, 5, 6 };
            var expectedKey = new byte[32];
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>(
                    "webCryptoInterop.pbkdf2",
                    It.Is<object[]>(args => (string)args[4] == "SHA-256")))
                .ReturnsAsync(expectedKey);

            // Act
            var result = await _service.Pbkdf2Async(password, salt, 1000, 32);

            // Assert
            _jsRuntimeMock.Verify(js => js.InvokeAsync<byte[]>(
                "webCryptoInterop.pbkdf2",
                It.Is<object[]>(args => (string)args[4] == "SHA-256")), 
                Times.Once);
        }

        #endregion

        #region ECDSA Key Generation Tests

        [Theory]
        [InlineData("P-256", "Default curve")]
        [InlineData("P-384", "P-384 curve")]
        [InlineData("P-521", "P-521 curve")]
        public async Task GenerateECDSAKeyPairAsync_WithVariousCurves_ShouldReturnKeyPair(
            string curve, string scenario)
        {
            // Arrange
            var expectedKeyPair = new ECDSAKeyPair
            {
                PublicKey = new byte[] { 1, 2, 3, 4 },
                PrivateKey = new byte[] { 5, 6, 7, 8 }
            };
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<ECDSAKeyPair>(
                    "webCryptoInterop.generateECDSAKeyPair",
                    It.Is<object[]>(args => args.Length == 1 && (string)args[0] == curve)))
                .ReturnsAsync(expectedKeyPair);

            // Act
            var result = await _service.GenerateECDSAKeyPairAsync(curve);

            // Assert
            result.Should().BeEquivalentTo(expectedKeyPair, scenario);
        }

        [Fact]
        public async Task GenerateECDSAKeyPairAsync_WhenJSReturnsNull_ShouldReturnEmptyKeyPair()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<ECDSAKeyPair>(
                    "webCryptoInterop.generateECDSAKeyPair",
                    It.IsAny<object[]>()))
                .ReturnsAsync((ECDSAKeyPair)null);

            // Act
            var result = await _service.GenerateECDSAKeyPairAsync();

            // Assert
            result.Should().NotBeNull();
            result.PublicKey.Should().BeEmpty();
            result.PrivateKey.Should().BeEmpty();
        }

        [Fact]
        public async Task GenerateECDSAKeyPairAsync_WithDefaultParameters_ShouldUseP256()
        {
            // Arrange
            var expectedKeyPair = new ECDSAKeyPair();
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<ECDSAKeyPair>(
                    "webCryptoInterop.generateECDSAKeyPair",
                    It.Is<object[]>(args => (string)args[0] == "P-256")))
                .ReturnsAsync(expectedKeyPair);

            // Act
            await _service.GenerateECDSAKeyPairAsync();

            // Assert
            _jsRuntimeMock.Verify(js => js.InvokeAsync<ECDSAKeyPair>(
                "webCryptoInterop.generateECDSAKeyPair",
                It.Is<object[]>(args => (string)args[0] == "P-256")), 
                Times.Once);
        }

        #endregion

        #region ECDSA Sign Tests

        [Theory]
        [InlineData("P-256", "SHA-256", "Default parameters")]
        [InlineData("P-384", "SHA-384", "P-384 with SHA-384")]
        [InlineData("P-521", "SHA-512", "P-521 with SHA-512")]
        public async Task SignECDSAAsync_WithValidParameters_ShouldReturnSignature(
            string curve, string hash, string scenario)
        {
            // Arrange
            var privateKey = new byte[] { 1, 2, 3, 4 };
            var data = new byte[] { 5, 6, 7, 8 };
            var expectedSignature = new byte[] { 9, 10, 11, 12 };
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>(
                    "webCryptoInterop.signECDSA",
                    It.Is<object[]>(args => 
                        args.Length == 4 &&
                        args[0].Equals(privateKey) &&
                        args[1].Equals(data) &&
                        (string)args[2] == curve &&
                        (string)args[3] == hash)))
                .ReturnsAsync(expectedSignature);

            // Act
            var result = await _service.SignECDSAAsync(privateKey, data, curve, hash);

            // Assert
            result.Should().BeEquivalentTo(expectedSignature, scenario);
        }

        [Theory]
        [InlineData(null, new byte[] { 1 }, "privateKey", "Null private key")]
        [InlineData(new byte[] { 1 }, null, "data", "Null data")]
        public async Task SignECDSAAsync_WithNullParameters_ShouldThrowArgumentNullException(
            byte[] privateKey, byte[] data, string paramName, string scenario)
        {
            // Arrange & Act
            var action = async () => await _service.SignECDSAAsync(privateKey, data);

            // Assert
            await action.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName(paramName);
        }

        [Fact]
        public async Task SignECDSAAsync_WithDefaultParameters_ShouldUseP256AndSHA256()
        {
            // Arrange
            var privateKey = new byte[] { 1, 2, 3 };
            var data = new byte[] { 4, 5, 6 };
            var expectedSignature = new byte[] { 7, 8, 9 };
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>(
                    "webCryptoInterop.signECDSA",
                    It.Is<object[]>(args => 
                        (string)args[2] == "P-256" && 
                        (string)args[3] == "SHA-256")))
                .ReturnsAsync(expectedSignature);

            // Act
            await _service.SignECDSAAsync(privateKey, data);

            // Assert
            _jsRuntimeMock.Verify(js => js.InvokeAsync<byte[]>(
                "webCryptoInterop.signECDSA",
                It.Is<object[]>(args => 
                    (string)args[2] == "P-256" && 
                    (string)args[3] == "SHA-256")), 
                Times.Once);
        }

        #endregion

        #region ECDSA Verify Tests

        [Theory]
        [InlineData(true, "Valid signature")]
        [InlineData(false, "Invalid signature")]
        public async Task VerifyECDSAAsync_WithVariousResults_ShouldReturnExpectedValue(
            bool expectedResult, string scenario)
        {
            // Arrange
            var publicKey = new byte[] { 1, 2, 3 };
            var signature = new byte[] { 4, 5, 6 };
            var data = new byte[] { 7, 8, 9 };
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<bool>(
                    "webCryptoInterop.verifyECDSA",
                    It.IsAny<object[]>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.VerifyECDSAAsync(publicKey, signature, data);

            // Assert
            result.Should().Be(expectedResult, scenario);
        }

        [Theory]
        [InlineData(null, new byte[] { 1 }, new byte[] { 2 }, "publicKey", "Null public key")]
        [InlineData(new byte[] { 1 }, null, new byte[] { 2 }, "signature", "Null signature")]
        [InlineData(new byte[] { 1 }, new byte[] { 2 }, null, "data", "Null data")]
        public async Task VerifyECDSAAsync_WithNullParameters_ShouldThrowArgumentNullException(
            byte[] publicKey, byte[] signature, byte[] data, string paramName, string scenario)
        {
            // Arrange & Act
            var action = async () => await _service.VerifyECDSAAsync(publicKey, signature, data);

            // Assert
            await action.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName(paramName);
        }

        [Theory]
        [InlineData("P-256", "SHA-256", "Default parameters")]
        [InlineData("P-384", "SHA-384", "P-384 with SHA-384")]
        [InlineData("P-521", "SHA-512", "P-521 with SHA-512")]
        public async Task VerifyECDSAAsync_WithVariousCurvesAndHashes_ShouldPassToJS(
            string curve, string hash, string scenario)
        {
            // Arrange
            var publicKey = new byte[] { 1, 2, 3 };
            var signature = new byte[] { 4, 5, 6 };
            var data = new byte[] { 7, 8, 9 };
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<bool>(
                    "webCryptoInterop.verifyECDSA",
                    It.Is<object[]>(args => 
                        args.Length == 5 &&
                        args[0].Equals(publicKey) &&
                        args[1].Equals(signature) &&
                        args[2].Equals(data) &&
                        (string)args[3] == curve &&
                        (string)args[4] == hash)))
                .ReturnsAsync(true);

            // Act
            var result = await _service.VerifyECDSAAsync(publicKey, signature, data, curve, hash);

            // Assert
            result.Should().BeTrue(scenario);
            _jsRuntimeMock.Verify(js => js.InvokeAsync<bool>(
                "webCryptoInterop.verifyECDSA",
                It.Is<object[]>(args => 
                    (string)args[3] == curve && 
                    (string)args[4] == hash)), 
                Times.Once);
        }

        #endregion

        #region AES-GCM Encryption Tests

        [Theory]
        [InlineData(16, 12, "AES-128 with 96-bit IV")]
        [InlineData(32, 12, "AES-256 with 96-bit IV")]
        [InlineData(16, 16, "AES-128 with 128-bit IV")]
        [InlineData(32, 16, "AES-256 with 128-bit IV")]
        public async Task EncryptAESGCMAsync_WithValidParameters_ShouldReturnEncryptedData(
            int keySize, int ivSize, string scenario)
        {
            // Arrange
            var key = new byte[keySize];
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var iv = new byte[ivSize];
            Random.Shared.NextBytes(key);
            Random.Shared.NextBytes(iv);
            
            var expectedEncrypted = new byte[data.Length + 16]; // Data + tag
            Random.Shared.NextBytes(expectedEncrypted);
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>(
                    "webCryptoInterop.encryptAESGCM",
                    It.Is<object[]>(args => 
                        args.Length == 3 &&
                        args[0].Equals(key) &&
                        args[1].Equals(data) &&
                        args[2].Equals(iv))))
                .ReturnsAsync(expectedEncrypted);

            // Act
            var result = await _service.EncryptAESGCMAsync(key, data, iv);

            // Assert
            result.Should().BeEquivalentTo(expectedEncrypted, scenario);
        }

        [Theory]
        [InlineData(null, new byte[] { 1 }, new byte[] { 2 }, "key", "Null key")]
        [InlineData(new byte[] { 1 }, null, new byte[] { 2 }, "data", "Null data")]
        [InlineData(new byte[] { 1 }, new byte[] { 2 }, null, "iv", "Null IV")]
        public async Task EncryptAESGCMAsync_WithNullParameters_ShouldThrowArgumentNullException(
            byte[] key, byte[] data, byte[] iv, string paramName, string scenario)
        {
            // Arrange & Act
            var action = async () => await _service.EncryptAESGCMAsync(key, data, iv);

            // Assert
            await action.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName(paramName);
        }

        [Fact]
        public async Task EncryptAESGCMAsync_WithEmptyData_ShouldStillWork()
        {
            // Arrange
            var key = new byte[16];
            var data = new byte[0];
            var iv = new byte[12];
            var expectedEncrypted = new byte[16]; // Just the tag for empty data
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>(
                    "webCryptoInterop.encryptAESGCM",
                    It.IsAny<object[]>()))
                .ReturnsAsync(expectedEncrypted);

            // Act
            var result = await _service.EncryptAESGCMAsync(key, data, iv);

            // Assert
            result.Should().BeEquivalentTo(expectedEncrypted);
        }

        #endregion

        #region AES-GCM Decryption Tests

        [Theory]
        [InlineData(16, 12, "AES-128 with 96-bit IV")]
        [InlineData(32, 12, "AES-256 with 96-bit IV")]
        [InlineData(16, 16, "AES-128 with 128-bit IV")]
        [InlineData(32, 16, "AES-256 with 128-bit IV")]
        public async Task DecryptAESGCMAsync_WithValidParameters_ShouldReturnDecryptedData(
            int keySize, int ivSize, string scenario)
        {
            // Arrange
            var key = new byte[keySize];
            var encryptedData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
            var iv = new byte[ivSize];
            Random.Shared.NextBytes(key);
            Random.Shared.NextBytes(iv);
            
            var expectedDecrypted = new byte[] { 1, 2, 3, 4 };
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>(
                    "webCryptoInterop.decryptAESGCM",
                    It.Is<object[]>(args => 
                        args.Length == 3 &&
                        args[0].Equals(key) &&
                        args[1].Equals(encryptedData) &&
                        args[2].Equals(iv))))
                .ReturnsAsync(expectedDecrypted);

            // Act
            var result = await _service.DecryptAESGCMAsync(key, encryptedData, iv);

            // Assert
            result.Should().BeEquivalentTo(expectedDecrypted, scenario);
        }

        [Theory]
        [InlineData(null, new byte[] { 1 }, new byte[] { 2 }, "key", "Null key")]
        [InlineData(new byte[] { 1 }, null, new byte[] { 2 }, "encryptedData", "Null encrypted data")]
        [InlineData(new byte[] { 1 }, new byte[] { 2 }, null, "iv", "Null IV")]
        public async Task DecryptAESGCMAsync_WithNullParameters_ShouldThrowArgumentNullException(
            byte[] key, byte[] encryptedData, byte[] iv, string paramName, string scenario)
        {
            // Arrange & Act
            var action = async () => await _service.DecryptAESGCMAsync(key, encryptedData, iv);

            // Assert
            await action.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName(paramName);
        }

        [Fact]
        public async Task DecryptAESGCMAsync_WhenDecryptionFails_ShouldPropagateException()
        {
            // Arrange
            var key = new byte[16];
            var encryptedData = new byte[] { 1, 2, 3 };
            var iv = new byte[12];
            
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>(
                    "webCryptoInterop.decryptAESGCM",
                    It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Decryption failed - invalid tag"));

            // Act
            var action = async () => await _service.DecryptAESGCMAsync(key, encryptedData, iv);

            // Assert
            await action.Should().ThrowAsync<JSException>()
                .WithMessage("Decryption failed - invalid tag");
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task FullCryptoWorkflow_ShouldWorkCorrectly()
        {
            // This test simulates a complete cryptographic workflow
            // Arrange
            var testData = new byte[] { 1, 2, 3, 4, 5 };
            var password = new byte[] { 6, 7, 8, 9 };
            var salt = new byte[] { 10, 11, 12, 13 };
            
            // Setup IsAvailable
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<bool>("webCryptoInterop.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            
            // Setup GetRandomBytes for IV
            var iv = new byte[12];
            Random.Shared.NextBytes(iv);
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>("webCryptoInterop.getRandomValues", It.Is<object[]>(args => (int)args[0] == 12)))
                .ReturnsAsync(iv);
            
            // Setup PBKDF2 for key derivation
            var derivedKey = new byte[32];
            Random.Shared.NextBytes(derivedKey);
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>("webCryptoInterop.pbkdf2", It.IsAny<object[]>()))
                .ReturnsAsync(derivedKey);
            
            // Setup SHA-256 hash
            var hashedData = new byte[32];
            Random.Shared.NextBytes(hashedData);
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>("webCryptoInterop.sha256", It.IsAny<object[]>()))
                .ReturnsAsync(hashedData);
            
            // Setup AES-GCM encryption
            var encryptedData = new byte[testData.Length + 16];
            Random.Shared.NextBytes(encryptedData);
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>("webCryptoInterop.encryptAESGCM", It.IsAny<object[]>()))
                .ReturnsAsync(encryptedData);
            
            // Act - Perform a complete workflow
            var isAvailable = await _service.IsAvailableAsync();
            isAvailable.Should().BeTrue();
            
            var randomIv = await _service.GetRandomBytesAsync(12);
            randomIv.Should().HaveCount(12);
            
            var key = await _service.Pbkdf2Async(password, salt, 10000, 32);
            key.Should().HaveCount(32);
            
            var hash = await _service.Sha256Async(testData);
            hash.Should().HaveCount(32);
            
            var encrypted = await _service.EncryptAESGCMAsync(key, testData, randomIv);
            encrypted.Should().NotBeEmpty();
            
            // Verify all methods were called
            _jsRuntimeMock.Verify(js => js.InvokeAsync<bool>("webCryptoInterop.isAvailable", It.IsAny<object[]>()), Times.Once);
            _jsRuntimeMock.Verify(js => js.InvokeAsync<byte[]>("webCryptoInterop.getRandomValues", It.IsAny<object[]>()), Times.Once);
            _jsRuntimeMock.Verify(js => js.InvokeAsync<byte[]>("webCryptoInterop.pbkdf2", It.IsAny<object[]>()), Times.Once);
            _jsRuntimeMock.Verify(js => js.InvokeAsync<byte[]>("webCryptoInterop.sha256", It.IsAny<object[]>()), Times.Once);
            _jsRuntimeMock.Verify(js => js.InvokeAsync<byte[]>("webCryptoInterop.encryptAESGCM", It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task ECDSASignAndVerify_ShouldWorkTogether()
        {
            // Arrange
            var keyPair = new ECDSAKeyPair
            {
                PublicKey = new byte[] { 1, 2, 3, 4 },
                PrivateKey = new byte[] { 5, 6, 7, 8 }
            };
            var dataToSign = new byte[] { 9, 10, 11, 12 };
            var signature = new byte[] { 13, 14, 15, 16 };
            
            // Setup key generation
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<ECDSAKeyPair>("webCryptoInterop.generateECDSAKeyPair", It.IsAny<object[]>()))
                .ReturnsAsync(keyPair);
            
            // Setup signing
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<byte[]>("webCryptoInterop.signECDSA", It.IsAny<object[]>()))
                .ReturnsAsync(signature);
            
            // Setup verification
            _jsRuntimeMock
                .Setup(js => js.InvokeAsync<bool>("webCryptoInterop.verifyECDSA", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            
            // Act
            var generatedKeyPair = await _service.GenerateECDSAKeyPairAsync();
            var signatureResult = await _service.SignECDSAAsync(generatedKeyPair.PrivateKey, dataToSign);
            var verificationResult = await _service.VerifyECDSAAsync(generatedKeyPair.PublicKey, signatureResult, dataToSign);
            
            // Assert
            generatedKeyPair.Should().BeEquivalentTo(keyPair);
            signatureResult.Should().BeEquivalentTo(signature);
            verificationResult.Should().BeTrue();
        }

        #endregion
    }
}