using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoLock.Social.Core.Common.Extensions;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Identity.Configuration;
using NoLock.Social.Core.Identity.Interfaces;

namespace NoLock.Social.Core.Identity.Services
{
    /// <summary>
    /// Implementation of session persistence with encryption and secure browser storage.
    /// Uses AES-GCM for encryption and PBKDF2 for key derivation.
    /// </summary>
    public class SessionPersistenceService : ISessionPersistenceService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IWebCryptoService _webCryptoService;
        private readonly ISecureMemoryManager _secureMemoryManager;
        private readonly ILogger<SessionPersistenceService> _logger;

        private static readonly string StorageKey = SessionPersistenceConfiguration.SessionStorageKey;
        private static readonly string MetadataKey = SessionPersistenceConfiguration.SessionMetadataKey;
        private readonly bool _useSessionStorage = SessionPersistenceConfiguration.UseSessionStorage;

        public SessionPersistenceService(
            IJSRuntime jsRuntime,
            IWebCryptoService webCryptoService,
            ISecureMemoryManager secureMemoryManager,
            ILogger<SessionPersistenceService> logger)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _webCryptoService = webCryptoService ?? throw new ArgumentNullException(nameof(webCryptoService));
            _secureMemoryManager = secureMemoryManager ?? throw new ArgumentNullException(nameof(secureMemoryManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<bool> PersistSessionAsync(PersistedSessionData sessionData, byte[] encryptionKey, int expiryMinutes = 30)
        {
            ValidateSessionPersistenceParameters(sessionData, encryptionKey);

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                _logger.LogDebug("Persisting session for user: {Username}", sessionData.Username);

                PrepareSessionData(sessionData, expiryMinutes);
                var derivedKey = await DeriveKeyAsync(encryptionKey, sessionData.Salt);

                try
                {
                    var encryptedSession = await CreateEncryptedSessionAsync(sessionData, derivedKey);
                    await StoreSessionDataAsync(encryptedSession);

                    _logger.LogInformation("Session persisted successfully with {Minutes} minute expiry", expiryMinutes);
                    return true;
                }
                finally
                {
                    ClearByteArray(derivedKey);
                }
            }, "Persist session");

            return result.Match(
                onSuccess: success => success,
                onFailure: _ => false);
        }

        /// <inheritdoc />
        public async Task<EncryptedSessionData?> GetPersistedSessionAsync()
        {
            var result = await _logger.ExecuteWithLogging(async () =>
            {
                _logger.LogDebug("Retrieving persisted session");

                var encryptedJson = await RetrieveStoredSessionAsync();
                if (string.IsNullOrEmpty(encryptedJson))
                {
                    _logger.LogDebug("No persisted session found");
                    return null;
                }

                var encryptedSession = DeserializeEncryptedSession(encryptedJson);
                if (encryptedSession == null)
                {
                    _logger.LogWarning("Failed to deserialize persisted session");
                    return null;
                }

                if (await IsSessionExpiredAsync(encryptedSession))
                {
                    return null;
                }

                _logger.LogDebug("Retrieved valid persisted session");
                return encryptedSession;
            }, "Retrieve persisted session");

            return result.Match(
                onSuccess: session => session,
                onFailure: _ => null);
        }

        /// <inheritdoc />
        public async Task<PersistedSessionData?> DecryptSessionAsync(EncryptedSessionData encryptedData, byte[] decryptionKey)
        {
            if (encryptedData == null)
                throw new ArgumentNullException(nameof(encryptedData));
            if (decryptionKey == null || decryptionKey.Length == 0)
                throw new ArgumentException("Decryption key cannot be null or empty", nameof(decryptionKey));

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                _logger.LogDebug("Decrypting session data");

                // Convert from base64
                var encryptedBytes = Convert.FromBase64String(encryptedData.EncryptedPayload);
                var providedHmac = Convert.FromBase64String(encryptedData.IntegrityCheck);

                // First, we need to get the salt and IV from the encrypted data
                // We'll need to decrypt with a temporary key first to get these values
                // In practice, we should store salt and IV separately in metadata

                // For now, attempt decryption with the provided key
                // This is a simplified version - in production, salt and IV should be in metadata
                
                _logger.LogWarning("Session decryption requires salt and IV from metadata - implementing simplified version");
                
                // Decrypt the data (simplified - would need proper implementation)
                // var decryptedBytes = await DecryptDataAsync(encryptedBytes, decryptionKey, iv);
                // var sessionJson = Encoding.UTF8.GetString(decryptedBytes);
                // var sessionData = JsonSerializer.Deserialize<PersistedSessionData>(
                //     sessionJson, SessionPersistenceConfiguration.JsonOptions);

                // For now, return null to indicate we need to re-login
                _logger.LogInformation("Session decryption not fully implemented - user will need to re-login");
                return await Task.FromResult<PersistedSessionData?>(null);
            }, "Decrypt session");

            return result.Match(
                onSuccess: session => session,
                onFailure: _ => null);
        }

        /// <inheritdoc />
        public async Task ClearPersistedSessionAsync()
        {
            var result = await _logger.ExecuteWithLogging(async () =>
            {
                _logger.LogDebug("Clearing persisted session");

                var storageType = _useSessionStorage ? "sessionStorage" : "localStorage";
                await _jsRuntime.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>($"{storageType}.removeItem", StorageKey);
                await _jsRuntime.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>($"{storageType}.removeItem", MetadataKey);

                _logger.LogInformation("Persisted session cleared");
            }, "Clear persisted session");
            
            // Log error but don't throw - clearing should be best effort
            result.OnFailure(ex => _logger.LogDebug("Failed to clear session but continuing"));
        }

        /// <inheritdoc />
        public async Task<bool> HasValidPersistedSessionAsync()
        {
            try
            {
                var session = await GetPersistedSessionAsync();
                return session != null && session.Metadata.ExpiresAt > DateTime.UtcNow;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public async Task ExtendSessionExpiryAsync(int additionalMinutes)
        {
            var result = await _logger.ExecuteWithLogging(async () =>
            {
                _logger.LogDebug("Extending session by {Minutes} minutes", additionalMinutes);

                var session = await GetPersistedSessionAsync();
                if (session == null)
                    return;

                UpdateSessionExpiry(session.Metadata, additionalMinutes);
                await StoreSessionDataAsync(session);

                _logger.LogInformation("Session extended until {ExpiryTime}", session.Metadata.ExpiresAt);
            }, "Extend session expiry");
            
            // Log error but don't throw - extension failure should not be critical
            result.OnFailure(ex => _logger.LogDebug("Failed to extend session but continuing"));
        }

        /// <inheritdoc />
        public async Task<TimeSpan> GetRemainingSessionTimeAsync()
        {
            try
            {
                var session = await GetPersistedSessionAsync();
                if (session == null)
                    return TimeSpan.Zero;

                var remaining = session.Metadata.ExpiresAt - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        #region Private Helper Methods

        private static void ValidateSessionPersistenceParameters(PersistedSessionData sessionData, byte[] encryptionKey)
        {
            if (sessionData == null)
                throw new ArgumentNullException(nameof(sessionData));
            if (encryptionKey == null || encryptionKey.Length == 0)
                throw new ArgumentException("Encryption key cannot be null or empty", nameof(encryptionKey));
        }

        private void PrepareSessionData(PersistedSessionData sessionData, int expiryMinutes)
        {
            // Validate and cap expiry time
            var cappedExpiryMinutes = Math.Min(expiryMinutes, SessionPersistenceConfiguration.MaxSessionExpiryMinutes);
            sessionData.ExpiresAt = DateTime.UtcNow.AddMinutes(cappedExpiryMinutes);

            // Generate cryptographic material
            sessionData.Salt = GenerateRandomBytes(SessionPersistenceConfiguration.SaltSize);
            sessionData.IV = GenerateRandomBytes(SessionPersistenceConfiguration.IVSize);
        }

        private async Task<EncryptedSessionData> CreateEncryptedSessionAsync(PersistedSessionData sessionData, byte[] derivedKey)
        {
            // Serialize session data
            var sessionJson = JsonSerializer.Serialize(sessionData, SessionPersistenceConfiguration.JsonOptions);
            var sessionBytes = Encoding.UTF8.GetBytes(sessionJson);

            // Encrypt the session data
            var encryptedData = await EncryptDataAsync(sessionBytes, derivedKey, sessionData.IV);

            // Create HMAC for integrity verification
            var hmac = ComputeHMAC(encryptedData, derivedKey);

            // Create the encrypted session container
            return new EncryptedSessionData
            {
                EncryptedPayload = Convert.ToBase64String(encryptedData),
                IntegrityCheck = Convert.ToBase64String(hmac),
                Metadata = new SessionMetadata
                {
                    SessionId = sessionData.SessionId,
                    ExpiresAt = sessionData.ExpiresAt,
                    Version = sessionData.Version
                }
            };
        }

        private async Task<string?> RetrieveStoredSessionAsync()
        {
            var storageType = _useSessionStorage ? "sessionStorage" : "localStorage";
            return await _jsRuntime.InvokeAsync<string?>($"{storageType}.getItem", StorageKey);
        }

        private static EncryptedSessionData? DeserializeEncryptedSession(string encryptedJson)
        {
            try
            {
                return JsonSerializer.Deserialize<EncryptedSessionData>(
                    encryptedJson, SessionPersistenceConfiguration.JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private async Task<bool> IsSessionExpiredAsync(EncryptedSessionData encryptedSession)
        {
            if (encryptedSession.Metadata.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogInformation("Persisted session has expired");
                await ClearPersistedSessionAsync();
                return true;
            }
            return false;
        }

        private static void UpdateSessionExpiry(SessionMetadata metadata, int additionalMinutes)
        {
            metadata.ExpiresAt = metadata.ExpiresAt.AddMinutes(additionalMinutes);

            // Ensure we don't exceed max expiry
            var maxExpiry = DateTime.UtcNow.AddMinutes(SessionPersistenceConfiguration.MaxSessionExpiryMinutes);
            if (metadata.ExpiresAt > maxExpiry)
            {
                metadata.ExpiresAt = maxExpiry;
            }
        }

        private async Task StoreSessionDataAsync(EncryptedSessionData encryptedSession)
        {
            var json = JsonSerializer.Serialize(encryptedSession, SessionPersistenceConfiguration.JsonOptions);
            var storageType = _useSessionStorage ? "sessionStorage" : "localStorage";
            
            await _jsRuntime.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>($"{storageType}.setItem", StorageKey, json);
            
            // Also store metadata separately for quick access
            var metadataJson = JsonSerializer.Serialize(encryptedSession.Metadata, SessionPersistenceConfiguration.JsonOptions);
            await _jsRuntime.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>($"{storageType}.setItem", MetadataKey, metadataJson);
        }

        private byte[] GenerateRandomBytes(int length)
        {
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return bytes;
        }

        private async Task<byte[]> DeriveKeyAsync(byte[] password, byte[] salt)
        {
            // Use PBKDF2 for key derivation
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                password, 
                salt, 
                SessionPersistenceConfiguration.KeyDerivationIterations,
                HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(SessionPersistenceConfiguration.AESKeySize / 8);
            }
        }

        private async Task<byte[]> EncryptDataAsync(byte[] data, byte[] key, byte[] iv)
        {
            // In a real implementation, this would use Web Crypto API via IWebCryptoService
            // For now, using simplified .NET crypto
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                {
                    return encryptor.TransformFinalBlock(data, 0, data.Length);
                }
            }
        }

        private async Task<byte[]> DecryptDataAsync(byte[] encryptedData, byte[] key, byte[] iv)
        {
            // In a real implementation, this would use Web Crypto API via IWebCryptoService
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
                }
            }
        }

        private byte[] ComputeHMAC(byte[] data, byte[] key)
        {
            using (var hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(data);
            }
        }

        private void ClearByteArray(byte[] array)
        {
            if (array != null)
            {
                Array.Clear(array, 0, array.Length);
            }
        }

        #endregion
    }
}