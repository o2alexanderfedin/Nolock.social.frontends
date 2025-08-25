using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Cryptography.Interfaces;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Implementation of cryptographic error handling service
    /// </summary>
    public class CryptoErrorHandlingService : ICryptoErrorHandlingService
    {
        private readonly ILogger<CryptoErrorHandlingService> _logger;
        
        // Sensitive keys that should never be logged
        private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "passphrase",
            "password",
            "privatekey",
            "private_key",
            "seed",
            "mnemonic",
            "secret",
            "pin",
            "key"
        };

        // Keys that should be redacted in logs
        private static readonly HashSet<string> RedactedKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "publickey",
            "public_key",
            "signature"
        };

        public CryptoErrorHandlingService(ILogger<CryptoErrorHandlingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ErrorInfo> HandleErrorAsync(Exception exception, ErrorContext context)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var category = CategorizeError(exception, context);
            var userMessage = await GetUserFriendlyMessageAsync(category);
            var suggestions = await GetRecoverySuggestionsAsync(category);
            var isCritical = DetermineIfCritical(category, exception);
            var errorCode = GenerateErrorCode(category);

            var errorInfo = new ErrorInfo
            {
                Category = category,
                UserMessage = userMessage,
                TechnicalDetails = SanitizeExceptionMessage(exception.Message),
                RecoverySuggestions = suggestions,
                IsCritical = isCritical,
                ErrorCode = errorCode,
                Timestamp = DateTime.UtcNow
            };

            // Log the error
            await LogErrorWithContextAsync(errorInfo, exception, context);

            return errorInfo;
        }

        public async Task<string> GetUserFriendlyMessageAsync(ErrorCategory category)
        {
            await Task.CompletedTask;

            return category switch
            {
                ErrorCategory.KeyDerivation => "Failed to process your passphrase. Please ensure you've entered it correctly.",
                ErrorCategory.SignatureGeneration => "Failed to generate signature for your content.",
                ErrorCategory.SignatureVerification => "Failed to verify signature. The content may have been tampered with.",
                ErrorCategory.SessionManagement => "Session management error. You may need to unlock your identity again.",
                ErrorCategory.Storage => "Storage operation failed. Please check your browser's storage settings.",
                ErrorCategory.Memory => "Memory allocation error. Please close some browser tabs and try again.",
                ErrorCategory.Initialization => "Initialization failed. Please refresh the page and try again.",
                ErrorCategory.BrowserCompatibility => "Your browser doesn't support required features. Please use a modern browser.",
                _ => "An unexpected error occurred. Please try again or contact support."
            };
        }

        public async Task<IReadOnlyList<string>> GetRecoverySuggestionsAsync(ErrorCategory category)
        {
            await Task.CompletedTask;

            return category switch
            {
                ErrorCategory.KeyDerivation => new[]
                {
                    "Verify your passphrase is correct",
                    "Check that your username matches exactly",
                    "Ensure you're using the correct case for both username and passphrase"
                },
                ErrorCategory.SignatureGeneration => new[]
                {
                    "Ensure your identity is unlocked",
                    "Try signing the content again",
                    "Check that the content is not too large"
                },
                ErrorCategory.SignatureVerification => new[]
                {
                    "The content may have been tampered with or corrupted",
                    "Verify the content source is trusted",
                    "Try retrieving the content again"
                },
                ErrorCategory.SessionManagement => new[]
                {
                    "Try unlocking your identity again",
                    "If the problem persists, refresh the page"
                },
                ErrorCategory.Storage => new[]
                {
                    "Check your browser's storage quota",
                    "Clear some browser storage if needed",
                    "Ensure private browsing mode is not enabled"
                },
                ErrorCategory.Memory => new[]
                {
                    "Close unnecessary browser tabs",
                    "Free up system resources",
                    "Try restarting your browser"
                },
                ErrorCategory.Initialization => new[]
                {
                    "Refresh the page",
                    "Clear browser cache",
                    "Check your internet connection"
                },
                ErrorCategory.BrowserCompatibility => new[]
                {
                    "Update your browser to the latest version",
                    "Try using Chrome, Firefox, or Edge",
                    "Ensure JavaScript is enabled"
                },
                _ => new[]
                {
                    "Try the operation again",
                    "If the problem persists, refresh the page",
                    "Contact support if the issue continues"
                }
            };
        }

        public async Task LogErrorAsync(ErrorInfo errorInfo)
        {
            if (errorInfo == null)
                throw new ArgumentNullException(nameof(errorInfo));

            await Task.CompletedTask;

            var logLevel = errorInfo.IsCritical ? LogLevel.Critical : LogLevel.Warning;
            
            _logger.Log(logLevel, 
                "Crypto error occurred: Category={Category}, Code={ErrorCode}, Message={UserMessage}",
                errorInfo.Category, errorInfo.ErrorCode, errorInfo.UserMessage);
        }

        public async Task<Dictionary<string, object>> SanitizeForLoggingAsync(Dictionary<string, object> data)
        {
            if (data == null)
                return new Dictionary<string, object>();

            await Task.CompletedTask;

            var sanitized = new Dictionary<string, object>();

            foreach (var kvp in data)
            {
                // Skip sensitive keys entirely
                if (SensitiveKeys.Contains(kvp.Key))
                    continue;

                // Redact certain keys
                if (RedactedKeys.Contains(kvp.Key))
                {
                    sanitized[kvp.Key] = "[REDACTED]";
                }
                else
                {
                    // For other keys, sanitize the value if it's a string
                    if (kvp.Value is string strValue)
                    {
                        sanitized[kvp.Key] = SanitizeString(strValue);
                    }
                    else
                    {
                        sanitized[kvp.Key] = kvp.Value;
                    }
                }
            }

            return sanitized;
        }

        private ErrorCategory CategorizeError(Exception exception, ErrorContext context)
        {
            // Check exception type first
            if (exception is CryptoException cryptoEx)
            {
                if (cryptoEx.Message.Contains("derivation", StringComparison.OrdinalIgnoreCase) ||
                    cryptoEx.Message.Contains("passphrase", StringComparison.OrdinalIgnoreCase))
                    return ErrorCategory.KeyDerivation;
                
                if (cryptoEx.Message.Contains("signature", StringComparison.OrdinalIgnoreCase))
                {
                    if (cryptoEx.Message.Contains("verification", StringComparison.OrdinalIgnoreCase) ||
                        cryptoEx.Message.Contains("verify", StringComparison.OrdinalIgnoreCase))
                        return ErrorCategory.SignatureVerification;
                    return ErrorCategory.SignatureGeneration;
                }
            }

            // StorageVerificationException removed - storage handled externally

            if (exception is OutOfMemoryException)
                return ErrorCategory.Memory;

            // Check context
            if (context.Component.Contains("Session", StringComparison.OrdinalIgnoreCase))
                return ErrorCategory.SessionManagement;

            if (context.Component.Contains("Storage", StringComparison.OrdinalIgnoreCase))
                return ErrorCategory.Storage;

            if (context.Component.Contains("BrowserCompatibility", StringComparison.OrdinalIgnoreCase))
                return ErrorCategory.BrowserCompatibility;

            if (context.Operation.Contains("Initialize", StringComparison.OrdinalIgnoreCase))
                return ErrorCategory.Initialization;

            if (context.Operation.Contains("Derive", StringComparison.OrdinalIgnoreCase))
                return ErrorCategory.KeyDerivation;

            if (context.Operation.Contains("Sign", StringComparison.OrdinalIgnoreCase))
                return ErrorCategory.SignatureGeneration;

            if (context.Operation.Contains("Verify", StringComparison.OrdinalIgnoreCase))
                return ErrorCategory.SignatureVerification;

            return ErrorCategory.Unknown;
        }

        private bool DetermineIfCritical(ErrorCategory category, Exception exception)
        {
            // Critical categories
            if (category == ErrorCategory.Memory ||
                category == ErrorCategory.KeyDerivation ||
                category == ErrorCategory.SignatureVerification ||
                category == ErrorCategory.Storage)
                return true;

            // Critical exception types
            if (exception is OutOfMemoryException ||
                exception is UnauthorizedAccessException ||
                exception is System.Security.SecurityException)
                return true;

            return false;
        }

        private string GenerateErrorCode(ErrorCategory category)
        {
            var prefix = category switch
            {
                ErrorCategory.KeyDerivation => "KD",
                ErrorCategory.SignatureGeneration => "SG",
                ErrorCategory.SignatureVerification => "SV",
                ErrorCategory.SessionManagement => "SM",
                ErrorCategory.Storage => "ST",
                ErrorCategory.Memory => "MEM",
                ErrorCategory.Initialization => "INIT",
                ErrorCategory.BrowserCompatibility => "BC",
                _ => "UNK"
            };

            var timestamp = DateTime.UtcNow.Ticks % 10000;
            return $"CRYPTO_{prefix}_{timestamp:D4}";
        }

        private string SanitizeExceptionMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            // Remove any potential sensitive data patterns
            var sanitized = message;

            // Remove anything that looks like a passphrase or key
            foreach (var sensitiveWord in SensitiveKeys)
            {
                var pattern = $@"\b{sensitiveWord}\b[:\s]*[^\s]+";
                sanitized = System.Text.RegularExpressions.Regex.Replace(
                    sanitized, pattern, $"{sensitiveWord}: [REDACTED]", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return sanitized;
        }

        private string SanitizeString(string value)
        {
            // Check if the string might contain sensitive data
            foreach (var sensitiveKey in SensitiveKeys)
            {
                if (value.Contains(sensitiveKey, StringComparison.OrdinalIgnoreCase))
                    return "[POTENTIALLY_SENSITIVE_DATA_REDACTED]";
            }

            return value;
        }

        private async Task LogErrorWithContextAsync(ErrorInfo errorInfo, Exception exception, ErrorContext context)
        {
            await Task.CompletedTask;

            var logLevel = errorInfo.IsCritical ? LogLevel.Error : LogLevel.Warning;
            
            var sanitizedContext = context.AdditionalData != null
                ? await SanitizeForLoggingAsync(context.AdditionalData)
                : new Dictionary<string, object>();

            _logger.Log(logLevel,
                exception,
                "{ErrorCategory} error in {Component}.{Operation}: {ErrorCode}",
                errorInfo.Category,
                context.Component,
                context.Operation,
                errorInfo.ErrorCode);

            if (sanitizedContext.Any())
            {
                _logger.LogDebug("Error context: {Context}", sanitizedContext);
            }
        }
    }
}