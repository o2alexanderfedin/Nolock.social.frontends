using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.OCR.Services;
using NoLock.Social.Core.Storage;
using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    public class ContentAddressableStorageEntryServiceTests
    {
        private readonly Mock<IContentAddressableStorage<TestDataType>> _mockBytesCas;
        private readonly Mock<IContentAddressableStorage<ContentMetadata>> _mockMetadataCas;
        private readonly Mock<IContentAddressableStorage<SignedTarget>> _mockSignedTargetCas;
        private readonly Mock<ISessionStateService> _mockSessionState;
        private readonly Mock<ISigningService> _mockSigningService;
        private readonly ContentAddressableStorageEntryService<TestDataType> _service;
        private readonly IdentitySession _testSession;

        public ContentAddressableStorageEntryServiceTests()
        {
            _mockBytesCas = new Mock<IContentAddressableStorage<TestDataType>>();
            _mockMetadataCas = new Mock<IContentAddressableStorage<ContentMetadata>>();
            _mockSignedTargetCas = new Mock<IContentAddressableStorage<SignedTarget>>();
            _mockSessionState = new Mock<ISessionStateService>();
            _mockSigningService = new Mock<ISigningService>();
            
            _testSession = new IdentitySession
            {
                PublicKey = new byte[] { 1, 2, 3, 4 },
                PrivateKeyBuffer = new TestSecureBuffer { Data = new byte[] { 5, 6, 7, 8 } }
            };

            _service = new ContentAddressableStorageEntryService<TestDataType>(
                _mockBytesCas.Object,
                _mockMetadataCas.Object,
                _mockSignedTargetCas.Object,
                _mockSessionState.Object,
                _mockSigningService.Object);
        }

        [Theory]
        [InlineData(false, null, "Session not unlocked")]
        [InlineData(true, null, "Session is null")]
        public async Task SubmitAsync_InvalidSessionState_ThrowsSecurityException(
            bool isUnlocked, IdentitySession currentSession, string scenario)
        {
            // Arrange
            var testValue = new TestDataType { Data = "test data" };
            var documentType = "receipt";

            _mockSessionState.Setup(x => x.IsUnlocked).Returns(isUnlocked);
            _mockSessionState.Setup(x => x.CurrentSession).Returns(currentSession);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<SecurityException>(
                async () => await _service.SubmitAsync(testValue, documentType));
            
            // Verify no storage operations were attempted
            _mockBytesCas.Verify(x => x.StoreAsync(It.IsAny<TestDataType>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockMetadataCas.Verify(x => x.StoreAsync(It.IsAny<ContentMetadata>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockSignedTargetCas.Verify(x => x.StoreAsync(It.IsAny<SignedTarget>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SubmitAsync_SessionWithNullPrivateKeyBuffer_ThrowsSecurityException()
        {
            // Arrange
            var testValue = new TestDataType { Data = "test data" };
            var documentType = "receipt";
            var sessionWithNullBuffer = new IdentitySession
            {
                PublicKey = new byte[] { 1, 2, 3, 4 },
                PrivateKeyBuffer = null
            };

            _mockSessionState.Setup(x => x.IsUnlocked).Returns(true);
            _mockSessionState.Setup(x => x.CurrentSession).Returns(sessionWithNullBuffer);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<SecurityException>(
                async () => await _service.SubmitAsync(testValue, documentType));
        }

        [Fact]
        public async Task SubmitAsync_SessionWithNullPrivateKeyData_ThrowsSecurityException()
        {
            // Arrange
            var testValue = new TestDataType { Data = "test data" };
            var documentType = "receipt";
            var sessionWithNullData = new IdentitySession
            {
                PublicKey = new byte[] { 1, 2, 3, 4 },
                PrivateKeyBuffer = new TestSecureBuffer { Data = null }
            };

            _mockSessionState.Setup(x => x.IsUnlocked).Returns(true);
            _mockSessionState.Setup(x => x.CurrentSession).Returns(sessionWithNullData);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<SecurityException>(
                async () => await _service.SubmitAsync(testValue, documentType));
        }

        [Fact]
        public async Task SubmitAsync_ValidSessionAndData_CompletesFullWorkflow()
        {
            // Arrange
            var testValue = new TestDataType { Data = "test data" };
            var documentType = "receipt";
            var valueHash = "value-hash-123";
            var metadataHash = "metadata-hash-456";
            var signedHash = "signed-hash-789";

            _mockSessionState.Setup(x => x.IsUnlocked).Returns(true);
            _mockSessionState.Setup(x => x.CurrentSession).Returns(_testSession);

            _mockBytesCas.Setup(x => x.StoreAsync(testValue, default))
                .ReturnsAsync(valueHash);

            _mockMetadataCas.Setup(x => x.StoreAsync(It.IsAny<ContentMetadata>(), default))
                .ReturnsAsync(metadataHash);

            var expectedSigned = new SignedTarget();
            _mockSigningService.Setup(x => x.SignAsync(metadataHash, It.IsAny<Ed25519KeyPair>()))
                .ReturnsAsync(expectedSigned);

            _mockSignedTargetCas.Setup(x => x.StoreAsync(expectedSigned, default))
                .ReturnsAsync(signedHash);

            // Act
            var result = await _service.SubmitAsync(testValue, documentType);

            // Assert
            Assert.Equal(signedHash, result);

            // Verify all storage operations were called
            _mockBytesCas.Verify(x => x.StoreAsync(testValue, default), Times.Once);
            _mockMetadataCas.Verify(x => x.StoreAsync(It.IsAny<ContentMetadata>(), default), Times.Once);
            _mockSignedTargetCas.Verify(x => x.StoreAsync(expectedSigned, default), Times.Once);
        }

        [Theory]
        [InlineData("receipt")]
        [InlineData("check")]
        [InlineData("custom-document-type")]
        public async Task SubmitAsync_DifferentDocumentTypes_CreatesCorrectMetadata(string documentType)
        {
            // Arrange
            var testValue = new TestDataType { Data = "test data" };
            var valueHash = "value-hash-123";
            var metadataHash = "metadata-hash-456";
            var signedHash = "signed-hash-789";

            _mockSessionState.Setup(x => x.IsUnlocked).Returns(true);
            _mockSessionState.Setup(x => x.CurrentSession).Returns(_testSession);

            _mockBytesCas.Setup(x => x.StoreAsync(testValue, default))
                .ReturnsAsync(valueHash);

            ContentMetadata capturedMetadata = null!;
            _mockMetadataCas.Setup(x => x.StoreAsync(It.IsAny<ContentMetadata>(), default))
                .Callback<ContentMetadata, CancellationToken>((metadata, _) => capturedMetadata = metadata)
                .ReturnsAsync(metadataHash);

            var expectedSigned = new SignedTarget();
            _mockSigningService.Setup(x => x.SignAsync(metadataHash, It.IsAny<Ed25519KeyPair>()))
                .ReturnsAsync(expectedSigned);

            _mockSignedTargetCas.Setup(x => x.StoreAsync(expectedSigned, default))
                .ReturnsAsync(signedHash);

            // Act
            await _service.SubmitAsync(testValue, documentType);

            // Assert
            Assert.NotNull(capturedMetadata);
            Assert.Single(capturedMetadata.References);
            Assert.Equal(valueHash, capturedMetadata.References.First().Hash);
            Assert.Equal(documentType, capturedMetadata.References.First().MimeType);
        }

        [Fact]
        public async Task SubmitAsync_ValidData_CreatesCorrectKeyPair()
        {
            // Arrange
            var testValue = new TestDataType { Data = "test data" };
            var documentType = "receipt";
            var valueHash = "value-hash-123";
            var metadataHash = "metadata-hash-456";
            var signedHash = "signed-hash-789";

            _mockSessionState.Setup(x => x.IsUnlocked).Returns(true);
            _mockSessionState.Setup(x => x.CurrentSession).Returns(_testSession);

            _mockBytesCas.Setup(x => x.StoreAsync(testValue, default))
                .ReturnsAsync(valueHash);

            _mockMetadataCas.Setup(x => x.StoreAsync(It.IsAny<ContentMetadata>(), default))
                .ReturnsAsync(metadataHash);

            Ed25519KeyPair capturedKeyPair = default;
            _mockSigningService.Setup(x => x.SignAsync(metadataHash, It.IsAny<Ed25519KeyPair>()))
                .Callback<string, Ed25519KeyPair>((_, keyPair) => capturedKeyPair = keyPair)
                .ReturnsAsync(new SignedTarget());

            _mockSignedTargetCas.Setup(x => x.StoreAsync(It.IsAny<SignedTarget>(), default))
                .ReturnsAsync(signedHash);

            // Act
            await _service.SubmitAsync(testValue, documentType);

            // Assert
            Assert.Equal(_testSession.PublicKey, capturedKeyPair.PublicKey);
            Assert.Equal(_testSession.PrivateKeyBuffer!.Data, capturedKeyPair.PrivateKey);
        }

        [Theory]
        [InlineData("StoreAsync bytes fails", nameof(IContentAddressableStorage<TestDataType>))]
        [InlineData("StoreAsync metadata fails", nameof(IContentAddressableStorage<ContentMetadata>))]
        [InlineData("SignAsync fails", nameof(ISigningService))]
        [InlineData("StoreAsync signed target fails", nameof(IContentAddressableStorage<SignedTarget>))]
        public async Task SubmitAsync_StorageOperationFails_PropagatesException(
            string scenario, string failingService)
        {
            // Arrange
            var testValue = new TestDataType { Data = "test data" };
            var documentType = "receipt";
            var expectedException = new InvalidOperationException($"Storage operation failed: {scenario}");

            _mockSessionState.Setup(x => x.IsUnlocked).Returns(true);
            _mockSessionState.Setup(x => x.CurrentSession).Returns(_testSession);

            // Setup successful operations before the failing one
            if (failingService == nameof(IContentAddressableStorage<TestDataType>))
            {
                _mockBytesCas.Setup(x => x.StoreAsync(testValue, default))
                    .ThrowsAsync(expectedException);
            }
            else
            {
                _mockBytesCas.Setup(x => x.StoreAsync(testValue, default))
                    .ReturnsAsync("value-hash-123");
            }

            if (failingService != nameof(IContentAddressableStorage<TestDataType>))
            {
                if (failingService == nameof(IContentAddressableStorage<ContentMetadata>))
                {
                    _mockMetadataCas.Setup(x => x.StoreAsync(It.IsAny<ContentMetadata>(), default))
                        .ThrowsAsync(expectedException);
                }
                else
                {
                    _mockMetadataCas.Setup(x => x.StoreAsync(It.IsAny<ContentMetadata>(), default))
                        .ReturnsAsync("metadata-hash-456");
                }
            }

            if (!failingService.Contains("IContentAddressableStorage"))
            {
                if (failingService == nameof(ISigningService))
                {
                    _mockSigningService.Setup(x => x.SignAsync(It.IsAny<string>(), It.IsAny<Ed25519KeyPair>()))
                        .ThrowsAsync(expectedException);
                }
                else
                {
                    _mockSigningService.Setup(x => x.SignAsync(It.IsAny<string>(), It.IsAny<Ed25519KeyPair>()))
                        .ReturnsAsync(new SignedTarget());
                }

                if (failingService != nameof(ISigningService))
                {
                    _mockSignedTargetCas.Setup(x => x.StoreAsync(It.IsAny<SignedTarget>(), default))
                        .ThrowsAsync(expectedException);
                }
            }

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.SubmitAsync(testValue, documentType));
            
            Assert.Equal(expectedException.Message, ex.Message);
        }

        [Fact]
        public async Task SubmitAsync_ConcurrentCalls_HandleCorrectly()
        {
            // Arrange
            var testValue1 = new TestDataType { Data = "test data 1" };
            var testValue2 = new TestDataType { Data = "test data 2" };
            var documentType = "receipt";

            _mockSessionState.Setup(x => x.IsUnlocked).Returns(true);
            _mockSessionState.Setup(x => x.CurrentSession).Returns(_testSession);

            // Setup different return values for concurrent calls
            _mockBytesCas.Setup(x => x.StoreAsync(testValue1, default))
                .ReturnsAsync("value-hash-1");
            _mockBytesCas.Setup(x => x.StoreAsync(testValue2, default))
                .ReturnsAsync("value-hash-2");

            _mockMetadataCas.Setup(x => x.StoreAsync(It.IsAny<ContentMetadata>(), default))
                .ReturnsAsync("metadata-hash");

            _mockSigningService.Setup(x => x.SignAsync(It.IsAny<string>(), It.IsAny<Ed25519KeyPair>()))
                .ReturnsAsync(new SignedTarget());

            _mockSignedTargetCas.Setup(x => x.StoreAsync(It.IsAny<SignedTarget>(), default))
                .ReturnsAsync("signed-hash");

            // Act
            var task1 = _service.SubmitAsync(testValue1, documentType).AsTask();
            var task2 = _service.SubmitAsync(testValue2, documentType).AsTask();
            var results = await Task.WhenAll(task1, task2);

            // Assert
            Assert.Equal(2, results.Length);
            Assert.All(results, result => Assert.Equal("signed-hash", result));

            // Verify both values were stored
            _mockBytesCas.Verify(x => x.StoreAsync(testValue1, default), Times.Once);
            _mockBytesCas.Verify(x => x.StoreAsync(testValue2, default), Times.Once);
        }

        // Test data classes
        public class TestDataType
        {
            public string Data { get; set; } = string.Empty;
        }

        public class TestSecureBuffer : ISecureBuffer
        {
            private byte[]? _data;
            private bool _throwOnDataAccess;
            
            public byte[] Data 
            { 
                get 
                {
                    if (_throwOnDataAccess)
                        throw new InvalidOperationException("Data access not allowed");
                    return _data ?? Array.Empty<byte>(); 
                }
                set => _data = value; 
            }
            
            public void SetThrowOnDataAccess(bool shouldThrow)
            {
                _throwOnDataAccess = shouldThrow;
            }
            
            public int Size => _data?.Length ?? 0;
            
            public bool IsCleared { get; private set; }
            
            public void Clear()
            {
                if (_data != null)
                {
                    Array.Clear(_data, 0, _data.Length);
                    IsCleared = true;
                }
            }
            
            public void CopyTo(ISecureBuffer destination)
            {
                if (_data != null && destination != null)
                {
                    Buffer.BlockCopy(_data, 0, destination.Data, 0, Math.Min(_data.Length, destination.Size));
                }
            }
            
            public ReadOnlySpan<byte> AsSpan()
            {
                return _data != null ? new ReadOnlySpan<byte>(_data) : ReadOnlySpan<byte>.Empty;
            }
            
            public void Dispose() 
            { 
                Clear();
            }
        }
    }
}