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
    /// Enhanced implementation of session persistence that stores only metadata.
    /// The actual keys are re-derived from the passphrase when needed.
    /// This approach is more secure as we never store private keys, even encrypted.
    /// </summary>
    public class SecureSessionPersistenceService : ISessionPersistenceService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IWebCryptoService _webCryptoService;
        private readonly ISecureMemoryManager _secureMemoryManager;
        private readonly ILogger<SecureSessionPersistenceService> _logger;

        private static readonly string StorageKey = SessionPersistenceConfiguration.SessionStorageKey;
        private static readonly string MetadataKey = SessionPersistenceConfiguration.SessionMetadataKey;
        private readonly bool _useSessionStorage = SessionPersistenceConfiguration.UseSessionStorage;

        public SecureSessionPersistenceService(
            IJSRuntime jsRuntime,
            IWebCryptoService webCryptoService,
            ISecureMemoryManager secureMemoryManager,
            ILogger<SecureSessionPersistenceService> logger)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _webCryptoService = webCryptoService ?? throw new ArgumentNullException(nameof(webCryptoService));
            _secureMemoryManager = secureMemoryManager ?? throw new ArgumentNullException(nameof(secureMemoryManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<bool> PersistSessionAsync(PersistedSessionData sessionData, byte[] encryptionKey, int expiryMinutes = 30)
        {
            if (sessionData == null)
                throw new ArgumentNullException(nameof(sessionData));

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                _logger.LogDebug("Persisting session metadata for user: {Username}", sessionData.Username);

                // Validate expiry time
                expiryMinutes = Math.Min(expiryMinutes, SessionPersistenceConfiguration.MaxSessionExpiryMinutes);
                var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

                // Create secure session metadata (no private keys stored)
                var secureMetadata = new SecureSessionMetadata
                {
                    SessionId = sessionData.SessionId,
                    Username = sessionData.Username,
                    PublicKeyBase64 = Convert.ToBase64String(sessionData.PublicKey),
                    CreatedAt = sessionData.CreatedAt,
                    ExpiresAt = expiresAt,
                    LastActivityAt = sessionData.LastActivityAt,
                    State = sessionData.State,
                    Version = 2, // Version 2 indicates secure metadata only
                    
                    // Store a verification hash to validate the session on restoration
                    // This helps ensure the user provides the correct passphrase
                    VerificationHash = ComputeVerificationHash(sessionData.Username, sessionData.PublicKey)
                };

                // Serialize and store the metadata
                var json = JsonSerializer.Serialize(secureMetadata, SessionPersistenceConfiguration.JsonOptions);
                var storageType = _useSessionStorage ? "sessionStorage" : "localStorage";
                await _jsRuntime.InvokeVoidAsync($"{storageType}.setItem", MetadataKey, json);

                _logger.LogInformation("Session metadata persisted successfully with {Minutes} minute expiry", expiryMinutes);
                return true;
            }, "Persist session metadata");

            return result.Match(
                onSuccess: success => success,
                onFailure: _ => false);
        }

        /// <inheritdoc />
        public async Task<EncryptedSessionData?> GetPersistedSessionAsync()
        {
            var result = await _logger.ExecuteWithLogging(async () =>
            {
                _logger.LogDebug("Retrieving persisted session metadata");

                var storageType = _useSessionStorage ? "sessionStorage" : "localStorage";
                var metadataJson = await _jsRuntime.InvokeAsync<string?>($"{storageType}.getItem", MetadataKey);

                if (string.IsNullOrEmpty(metadataJson))
                {
                    _logger.LogDebug("No persisted session metadata found");
                    return null;
                }

                var metadata = JsonSerializer.Deserialize<SecureSessionMetadata>(
                    metadataJson, SessionPersistenceConfiguration.JsonOptions);

                if (metadata == null)
                {
                    _logger.LogWarning("Failed to deserialize persisted session metadata");
                    return null;
                }

                // Check if session has expired
                if (metadata.ExpiresAt <= DateTime.UtcNow)
                {
                    _logger.LogInformation("Persisted session has expired");
                    await ClearPersistedSessionAsync();
                    return null;
                }

                // Convert to EncryptedSessionData format for compatibility
                var encryptedData = new EncryptedSessionData
                {
                    EncryptedPayload = "", // No encrypted payload in secure version
                    IntegrityCheck = metadata.VerificationHash,
                    Metadata = new SessionMetadata
                    {
                        SessionId = metadata.SessionId,
                        ExpiresAt = metadata.ExpiresAt,
                        Version = metadata.Version
                    }
                };

                _logger.LogDebug("Retrieved valid persisted session metadata");
                return encryptedData;
            }, "Retrieve session metadata");

            return result.Match(
                onSuccess: session => session,
                onFailure: _ => null);
        }

        /// <inheritdoc />
        public async Task<PersistedSessionData?> DecryptSessionAsync(EncryptedSessionData encryptedData, byte[] decryptionKey)
        {
            // In the secure version, we don't decrypt anything
            // The session data is re-derived from the passphrase when the user unlocks
            _logger.LogDebug("Secure session persistence does not store encrypted keys - session will be re-derived on unlock");
            return null;
        }

        /// <inheritdoc />
        public async Task ClearPersistedSessionAsync()
        {
            var result = await _logger.ExecuteWithLogging(async () =>
            {
                _logger.LogDebug("Clearing persisted session metadata");

                var storageType = _useSessionStorage ? "sessionStorage" : "localStorage";
                await _jsRuntime.InvokeVoidAsync($"{storageType}.removeItem", StorageKey);
                await _jsRuntime.InvokeVoidAsync($"{storageType}.removeItem", MetadataKey);

                _logger.LogInformation("Persisted session metadata cleared");
            }, "Clear session metadata");
            
            // Log error but don't throw - clearing should be best effort
            result.OnFailure(ex => _logger.LogDebug("Failed to clear metadata but continuing"));
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

                var storageType = _useSessionStorage ? "sessionStorage" : "localStorage";
                var metadataJson = await _jsRuntime.InvokeAsync<string?>($"{storageType}.getItem", MetadataKey);

                if (string.IsNullOrEmpty(metadataJson))
                    return;

                var metadata = JsonSerializer.Deserialize<SecureSessionMetadata>(
                    metadataJson, SessionPersistenceConfiguration.JsonOptions);

                if (metadata == null)
                    return;

                // Update expiry time
                metadata.ExpiresAt = metadata.ExpiresAt.AddMinutes(additionalMinutes);
                metadata.LastActivityAt = DateTime.UtcNow;

                // Ensure we don't exceed max expiry
                var maxExpiry = DateTime.UtcNow.AddMinutes(SessionPersistenceConfiguration.MaxSessionExpiryMinutes);
                if (metadata.ExpiresAt > maxExpiry)
                {
                    metadata.ExpiresAt = maxExpiry;
                }

                // Re-store the metadata
                var json = JsonSerializer.Serialize(metadata, SessionPersistenceConfiguration.JsonOptions);
                await _jsRuntime.InvokeVoidAsync($"{storageType}.setItem", MetadataKey, json);

                _logger.LogInformation("Session extended until {ExpiryTime}", metadata.ExpiresAt);
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

        /// <summary>
        /// Get the stored session metadata for display purposes
        /// </summary>
        public async Task<SecureSessionMetadata?> GetSessionMetadataAsync()
        {
            try
            {
                var storageType = _useSessionStorage ? "sessionStorage" : "localStorage";
                var metadataJson = await _jsRuntime.InvokeAsync<string?>($"{storageType}.getItem", MetadataKey);

                if (string.IsNullOrEmpty(metadataJson))
                    return null;

                return JsonSerializer.Deserialize<SecureSessionMetadata>(
                    metadataJson, SessionPersistenceConfiguration.JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        #region Private Helper Methods

        private string ComputeVerificationHash(string username, byte[] publicKey)
        {
            // Create a simple verification hash from username and public key
            // This is used to verify the session belongs to the correct user
            var combined = username + Convert.ToBase64String(publicKey);
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return Convert.ToBase64String(hashBytes);
            }
        }

        #endregion
    }

    /// <summary>
    /// Secure session metadata that doesn't contain any private keys
    /// </summary>
    public class SecureSessionMetadata
    {
        public string SessionId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PublicKeyBase64 { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public SessionState State { get; set; }
        public int Version { get; set; }
        public string VerificationHash { get; set; } = string.Empty;
    }
}