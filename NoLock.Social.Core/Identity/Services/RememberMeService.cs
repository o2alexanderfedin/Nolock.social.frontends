using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoLock.Social.Core.Common.Extensions;
using NoLock.Social.Core.Identity.Interfaces;
using NoLock.Social.Core.Identity.Storage;
using NoLock.Social.Core.Identity.Configuration;

namespace NoLock.Social.Core.Identity.Services
{
    /// <summary>
    /// Service implementation for "Remember Me" functionality.
    /// Stores ONLY the username in localStorage for convenience.
    /// Never stores passphrases, keys, or any sensitive session data.
    /// </summary>
    public class RememberMeService : IRememberMeService
    {
        private static readonly string STORAGE_KEY = LoginConfiguration.StorageKey;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<RememberMeService> _logger;
        
        // Cache the remembered state to avoid repeated JS interop calls
        private bool? _isUsernameRemembered;
        private string? _cachedUsername;

        public RememberMeService(
            IJSRuntime jsRuntime,
            ILogger<RememberMeService> logger)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsUsernameRemembered => _isUsernameRemembered ?? false;

        /// <inheritdoc />
        public async Task RememberUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username cannot be null or empty", nameof(username));
            }

            _logger.LogDebug("Remembering username for user: {Username}", username);

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                var data = new RememberedUserData
                {
                    Username = username,
                    LastUsed = DateTime.UtcNow,
                    Preferences = new() // Can be extended with non-sensitive preferences
                };

                var json = JsonSerializer.Serialize(data, LoginConfiguration.JsonOptions);

                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", STORAGE_KEY, json);
                
                // Update cache
                _isUsernameRemembered = true;
                _cachedUsername = username;
                
                _logger.LogInformation("Username remembered successfully");
            },
            "Failed to remember username");

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException("Failed to save username to local storage", result.Exception);
            }
        }

        /// <inheritdoc />
        public async Task<string?> GetRememberedUsernameAsync()
        {
            // Return cached value if available
            if (_cachedUsername != null)
            {
                _logger.LogDebug("Returning cached username");
                return _cachedUsername;
            }

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", STORAGE_KEY);
                
                if (string.IsNullOrEmpty(json))
                {
                    _logger.LogDebug("No remembered username found");
                    _isUsernameRemembered = false;
                    return (string?)null;
                }

                RememberedUserData? data = null;
                try
                {
                    data = JsonSerializer.Deserialize<RememberedUserData>(json, LoginConfiguration.JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize remembered user data - data may be corrupted");
                    // Clear corrupted data
                    await ClearRememberedDataAsync();
                    return (string?)null;
                }

                if (data == null || string.IsNullOrWhiteSpace(data.Username))
                {
                    _logger.LogWarning("Invalid or corrupted remembered user data");
                    _isUsernameRemembered = false;
                    return (string?)null;
                }

                // Check if the remembered data is too old (configurable expiry)
                var daysSinceLastUse = (DateTime.UtcNow - data.LastUsed).TotalDays;
                if (daysSinceLastUse > LoginConfiguration.RememberMeExpiryDays)
                {
                    _logger.LogInformation("Remembered username expired (last used {Days} days ago)", daysSinceLastUse);
                    await ClearRememberedDataAsync();
                    return (string?)null;
                }

                _logger.LogDebug("Retrieved remembered username: {Username}", data.Username);
                
                // Update cache
                _isUsernameRemembered = true;
                _cachedUsername = data.Username;
                
                return data.Username;
            },
            "Failed to retrieve remembered username");

            return result.IsSuccess ? result.Value : null;
        }

        /// <inheritdoc />
        public async Task ClearRememberedDataAsync()
        {
            _logger.LogDebug("Clearing remembered user data");

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", STORAGE_KEY);
                
                // Clear cache
                _isUsernameRemembered = false;
                _cachedUsername = null;
                
                _logger.LogInformation("Remembered user data cleared");
            },
            "Failed to clear remembered user data");
            
            // Don't throw - clearing is best effort, result is intentionally ignored
        }

        /// <summary>
        /// Update the last used timestamp for the remembered username.
        /// Called when a remembered username is successfully used for login.
        /// </summary>
        public async Task UpdateLastUsedAsync()
        {
            if (!IsUsernameRemembered || string.IsNullOrWhiteSpace(_cachedUsername))
            {
                return;
            }

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                var data = new RememberedUserData
                {
                    Username = _cachedUsername,
                    LastUsed = DateTime.UtcNow,
                    Preferences = new()
                };

                var json = JsonSerializer.Serialize(data, LoginConfiguration.JsonOptions);

                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", STORAGE_KEY, json);
                _logger.LogDebug("Updated last used timestamp for remembered username");
            },
            "Failed to update last used timestamp");
            
            // Don't throw - this is a non-critical operation, result is intentionally ignored
        }
    }
}