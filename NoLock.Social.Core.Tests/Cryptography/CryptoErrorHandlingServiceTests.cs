using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;

namespace NoLock.Social.Core.Tests.Cryptography
{
    public class CryptoErrorHandlingServiceTests
    {
        private readonly Mock<ILogger<CryptoErrorHandlingService>> _mockLogger;
        private readonly CryptoErrorHandlingService _errorHandler;

        public CryptoErrorHandlingServiceTests()
        {
            _mockLogger = new Mock<ILogger<CryptoErrorHandlingService>>();
            _errorHandler = new CryptoErrorHandlingService(_mockLogger.Object);
        }

        #region HandleErrorAsync Tests

        [Fact]
        public async Task HandleErrorAsync_ShouldCategorizeKeyDerivationError()
        {
            // Arrange
            var exception = new CryptoException("Key derivation failed");
            var context = new ErrorContext
            {
                Operation = "DeriveKey",
                Component = "KeyDerivationService"
            };

            // Act
            var result = await _errorHandler.HandleErrorAsync(exception, context);

            // Assert
            result.Should().NotBeNull();
            result.Category.Should().Be(ErrorCategory.KeyDerivation);
            result.UserMessage.Should().Contain("passphrase");
            result.RecoverySuggestions.Should().NotBeEmpty();
            result.IsCritical.Should().BeTrue();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("KeyDerivation")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task HandleErrorAsync_ShouldCategorizeSignatureError()
        {
            // Arrange
            var exception = new CryptoException("Signature verification failed");
            var context = new ErrorContext
            {
                Operation = "VerifySignature",
                Component = "VerificationService"
            };

            // Act
            var result = await _errorHandler.HandleErrorAsync(exception, context);

            // Assert
            result.Should().NotBeNull();
            result.Category.Should().Be(ErrorCategory.SignatureVerification);
            result.UserMessage.Should().Contain("signature");
            result.RecoverySuggestions.Should().Contain(s => s.Contains("tampered"));
            result.IsCritical.Should().BeTrue();
        }

        [Fact]
        public async Task HandleErrorAsync_ShouldCategorizeSessionError()
        {
            // Arrange
            var exception = new InvalidOperationException("Session expired");
            var context = new ErrorContext
            {
                Operation = "UnlockIdentity",
                Component = "SessionStateService"
            };

            // Act
            var result = await _errorHandler.HandleErrorAsync(exception, context);

            // Assert
            result.Should().NotBeNull();
            result.Category.Should().Be(ErrorCategory.SessionManagement);
            result.UserMessage.Should().Contain("Session");
            result.RecoverySuggestions.Should().Contain(s => s.Contains("unlock"));
            result.IsCritical.Should().BeFalse();
        }

        // Storage error test removed - StorageVerificationException no longer exists

        [Fact]
        public async Task HandleErrorAsync_ShouldCategorizeMemoryError()
        {
            // Arrange
            var exception = new OutOfMemoryException("Insufficient memory");
            var context = new ErrorContext
            {
                Operation = "AllocateSecureBuffer",
                Component = "SecureMemoryManager"
            };

            // Act
            var result = await _errorHandler.HandleErrorAsync(exception, context);

            // Assert
            result.Should().NotBeNull();
            result.Category.Should().Be(ErrorCategory.Memory);
            result.UserMessage.Should().Contain("Memory");
            result.RecoverySuggestions.Should().Contain(s => s.Contains("resources"));
            result.IsCritical.Should().BeTrue();
        }

        [Fact]
        public async Task HandleErrorAsync_ShouldNotLogSensitiveData()
        {
            // Arrange
            var sensitivePassphrase = "MySecretPassphrase123!";
            var exception = new CryptoException($"Failed with passphrase: {sensitivePassphrase}");
            var context = new ErrorContext
            {
                Operation = "DeriveKey",
                Component = "KeyDerivationService",
                AdditionalData = new Dictionary<string, object>
                {
                    { "passphrase", sensitivePassphrase },
                    { "privateKey", new byte[] { 1, 2, 3 } },
                    { "seed", "sensitive_seed_data" }
                }
            };

            // Act
            await _errorHandler.HandleErrorAsync(exception, context);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => !v.ToString()!.Contains(sensitivePassphrase)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region GetUserFriendlyMessage Tests

        [Theory]
        [InlineData(ErrorCategory.KeyDerivation, "Failed to process your passphrase")]
        [InlineData(ErrorCategory.SignatureGeneration, "Failed to generate signature")]
        [InlineData(ErrorCategory.SignatureVerification, "Failed to verify signature")]
        [InlineData(ErrorCategory.SessionManagement, "Session management error")]
        [InlineData(ErrorCategory.Storage, "Storage operation failed")]
        [InlineData(ErrorCategory.Memory, "Memory allocation error")]
        [InlineData(ErrorCategory.Initialization, "Initialization failed")]
        [InlineData(ErrorCategory.Unknown, "An unexpected error occurred")]
        public async Task GetUserFriendlyMessage_ShouldReturnAppropriateMessage(ErrorCategory category, string expectedSubstring)
        {
            // Act
            var message = await _errorHandler.GetUserFriendlyMessageAsync(category);

            // Assert
            message.Should().Contain(expectedSubstring);
        }

        #endregion

        #region GetRecoverySuggestions Tests

        [Fact]
        public async Task GetRecoverySuggestions_ShouldProvideKeyDerivationSuggestions()
        {
            // Arrange
            var category = ErrorCategory.KeyDerivation;

            // Act
            var suggestions = await _errorHandler.GetRecoverySuggestionsAsync(category);

            // Assert
            suggestions.Should().NotBeEmpty();
            suggestions.Should().Contain(s => s.Contains("passphrase"));
            suggestions.Should().Contain(s => s.Contains("username"));
        }

        [Fact]
        public async Task GetRecoverySuggestions_ShouldProvideSignatureVerificationSuggestions()
        {
            // Arrange
            var category = ErrorCategory.SignatureVerification;

            // Act
            var suggestions = await _errorHandler.GetRecoverySuggestionsAsync(category);

            // Assert
            suggestions.Should().NotBeEmpty();
            suggestions.Should().Contain(s => s.Contains("tampered") || s.Contains("corrupted"));
            suggestions.Should().Contain(s => s.Contains("source"));
        }

        [Fact]
        public async Task GetRecoverySuggestions_ShouldProvideSessionSuggestions()
        {
            // Arrange
            var category = ErrorCategory.SessionManagement;

            // Act
            var suggestions = await _errorHandler.GetRecoverySuggestionsAsync(category);

            // Assert
            suggestions.Should().NotBeEmpty();
            suggestions.Should().Contain(s => s.Contains("unlock") || s.Contains("re-authenticate"));
        }

        #endregion

        #region LogErrorAsync Tests

        [Fact]
        public async Task LogErrorAsync_ShouldLogWithoutSensitiveData()
        {
            // Arrange
            var errorInfo = new ErrorInfo
            {
                Category = ErrorCategory.KeyDerivation,
                UserMessage = "Failed to derive key",
                TechnicalDetails = "Argon2id failed with passphrase: [REDACTED]",
                IsCritical = true,
                ErrorCode = "CRYPTO_001"
            };

            // Act
            await _errorHandler.LogErrorAsync(errorInfo);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Critical,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => 
                        v.ToString()!.Contains("CRYPTO_") &&
                        !v.ToString()!.Contains("passphrase")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task LogErrorAsync_ShouldUseCorrectLogLevel()
        {
            // Arrange
            var criticalError = new ErrorInfo
            {
                Category = ErrorCategory.Memory,
                IsCritical = true
            };

            var nonCriticalError = new ErrorInfo
            {
                Category = ErrorCategory.SessionManagement,
                IsCritical = false
            };

            // Act
            await _errorHandler.LogErrorAsync(criticalError);
            await _errorHandler.LogErrorAsync(nonCriticalError);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Critical,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region SanitizeForLogging Tests

        [Fact]
        public async Task SanitizeForLogging_ShouldRemoveSensitiveKeys()
        {
            // Arrange
            var data = new Dictionary<string, object>
            {
                { "operation", "DeriveKey" },
                { "passphrase", "secret123" },
                { "privateKey", new byte[] { 1, 2, 3 } },
                { "publicKey", new byte[] { 4, 5, 6 } },
                { "seed", "sensitive_seed" },
                { "username", "john.doe" },
                { "timestamp", DateTime.UtcNow }
            };

            // Act
            var sanitized = await _errorHandler.SanitizeForLoggingAsync(data);

            // Assert
            sanitized.Should().ContainKey("operation");
            sanitized.Should().ContainKey("username");
            sanitized.Should().ContainKey("timestamp");
            sanitized.Should().NotContainKey("passphrase");
            sanitized.Should().NotContainKey("privateKey");
            sanitized.Should().NotContainKey("seed");
            sanitized["publicKey"].Should().Be("[REDACTED]");
        }

        #endregion
    }
}