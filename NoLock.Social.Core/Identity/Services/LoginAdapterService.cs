using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoLock.Social.Core.Common.Extensions;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using NoLock.Social.Core.Identity.Interfaces;
using NoLock.Social.Core.Identity.Models;

namespace NoLock.Social.Core.Identity.Services
{
    /// <summary>
    /// Adapter service that provides login semantics over the existing identity unlock mechanism.
    /// This is the main orchestrator that ties together key derivation, session management,
    /// user tracking, and remember me functionality.
    /// </summary>
    public class LoginAdapterService : ILoginAdapterService, IDisposable
    {
        private readonly ISessionStateService _sessionState;
        private readonly IUserTrackingService _userTracking;
        private readonly IRememberMeService _rememberMe;
        private readonly IKeyDerivationService _keyDerivation;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<LoginAdapterService> _logger;

        // Reactive state management
        private readonly Subject<LoginStateChange> _stateChanges = new();
        private LoginState _currentState = new();
        private readonly object _stateLock = new();

        // Disposal tracking
        private bool _disposed;

        public LoginAdapterService(
            ISessionStateService sessionState,
            IUserTrackingService userTracking,
            IRememberMeService rememberMe,
            IKeyDerivationService keyDerivation,
            IJSRuntime jsRuntime,
            ILogger<LoginAdapterService> logger)
        {
            _sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));
            _userTracking = userTracking ?? throw new ArgumentNullException(nameof(userTracking));
            _rememberMe = rememberMe ?? throw new ArgumentNullException(nameof(rememberMe));
            _keyDerivation = keyDerivation ?? throw new ArgumentNullException(nameof(keyDerivation));
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to session state changes to sync login state
            _sessionState.SessionStateChanged += OnSessionStateChanged;
            
            // Attempt to restore persisted session on initialization
            _ = TryRestorePersistedSessionAsync();
        }

        /// <inheritdoc />
        public LoginState CurrentLoginState
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState;
                }
            }
        }

        /// <inheritdoc />
        public IObservable<LoginStateChange> LoginStateChanges => _stateChanges.AsObservable();

        /// <inheritdoc />
        public async Task<LoginResult> LoginAsync(string username, string passphrase, bool rememberUsername)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "Username is required"
                };
            }

            if (string.IsNullOrWhiteSpace(passphrase))
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "Passphrase is required"
                };
            }

            _logger.LogInformation("Starting login process for user: {Username}", username);

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                // Step 1: Derive keys from passphrase and username
                _logger.LogDebug("Deriving identity keys...");
                var (keyPair, privateKeyBuffer) = await _keyDerivation.DeriveIdentityAsync(passphrase, username);

                // Step 2: Start session with derived keys
                _logger.LogDebug("Starting session...");
                var sessionStarted = await _sessionState.StartSessionAsync(username, keyPair, privateKeyBuffer);
                
                if (!sessionStarted)
                {
                    _logger.LogWarning("Failed to start session for user: {Username}", username);
                    return new LoginResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to start session. Please try again."
                    };
                }

                // Step 3: Check if this is a returning user
                var publicKeyBase64 = Convert.ToBase64String(keyPair.PublicKey);
                _logger.LogDebug("Checking user existence...");
                var userInfo = await _userTracking.CheckUserExistsAsync(publicKeyBase64);

                // Step 4: Remember username if requested
                if (rememberUsername)
                {
                    _logger.LogDebug("Remembering username...");
                    await _rememberMe.RememberUsernameAsync(username);
                }
                else if (_rememberMe.IsUsernameRemembered)
                {
                    // Clear remembered username if user unchecked the box
                    _logger.LogDebug("Clearing remembered username as user opted out");
                    await _rememberMe.ClearRememberedDataAsync();
                }

                // Step 5: Update login state
                var previousState = CurrentLoginState;
                var newState = new LoginState
                {
                    IsLoggedIn = true,
                    IsLocked = false,
                    Username = username,
                    PublicKeyBase64 = publicKeyBase64,
                    LoginTime = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow,
                    IsNewUser = !userInfo.Exists
                };

                UpdateState(newState, LoginStateChangeReason.Login, previousState);

                // Step 6: Log success metrics
                if (userInfo.Exists)
                {
                    _logger.LogInformation(
                        "Returning user logged in successfully: {Username}, ContentCount: {ContentCount}, LastSeen: {LastSeen}",
                        username, userInfo.ContentCount, userInfo.LastSeen);
                }
                else
                {
                    _logger.LogInformation("New user logged in successfully: {Username}", username);
                }

                // Return success result
                return new LoginResult
                {
                    Success = true,
                    IsNewUser = !userInfo.Exists,
                    Session = _sessionState.CurrentSession,
                    UserInfo = userInfo
                };
            }, $"Login for user {username}");

            return result.Match(
                onSuccess: loginResult => loginResult,
                onFailure: ex => new LoginResult
                {
                    Success = false,
                    ErrorMessage = "An error occurred during login. Please try again."
                });
        }

        /// <inheritdoc />
        public async Task LogoutAsync()
        {
            _logger.LogInformation("Logging out user: {Username}", CurrentLoginState.Username);

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                // End the session, which clears all sensitive data
                await _sessionState.EndSessionAsync();

                // Update login state
                var previousState = CurrentLoginState;
                var newState = new LoginState
                {
                    IsLoggedIn = false,
                    IsLocked = false,
                    Username = null,
                    PublicKeyBase64 = null,
                    LoginTime = null,
                    LastActivity = null,
                    IsNewUser = false
                };

                UpdateState(newState, LoginStateChangeReason.Logout, previousState);

                _logger.LogInformation("User logged out successfully");
            }, "Logout");

            result.ThrowIfFailure();
        }

        /// <inheritdoc />
        public async Task LockAsync()
        {
            _logger.LogInformation("Locking session for user: {Username}", CurrentLoginState.Username);

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                // Lock the session (keeps keys in memory but requires unlock)
                await _sessionState.LockSessionAsync();

                // Update login state
                var previousState = CurrentLoginState;
                var newState = CreateStateWithChanges(previousState, state => 
                {
                    state.IsLocked = true;
                    state.LastActivity = DateTime.UtcNow;
                });

                UpdateState(newState, LoginStateChangeReason.Lock, previousState);

                _logger.LogInformation("Session locked successfully");
            }, "Session lock");

            result.ThrowIfFailure();
        }

        /// <inheritdoc />
        public async Task<bool> IsReturningUserAsync()
        {
            if (!CurrentLoginState.IsLoggedIn || string.IsNullOrWhiteSpace(CurrentLoginState.PublicKeyBase64))
            {
                return false;
            }

            var result = await _logger.ExecuteWithLogging(async () =>
            {
                var userInfo = await _userTracking.CheckUserExistsAsync(CurrentLoginState.PublicKeyBase64);
                return userInfo.Exists;
            }, "Check returning user");

            return result.Match(
                onSuccess: exists => exists,
                onFailure: _ => false);
        }

        /// <summary>
        /// Handle session state changes from the underlying SessionStateService
        /// </summary>
        private void OnSessionStateChanged(object? sender, SessionStateChangedEventArgs e)
        {
            _logger.LogDebug("Session state changed from {OldState} to {NewState}", e.OldState, e.NewState);

            // Map session state changes to login state changes
            var currentLogin = CurrentLoginState;
            
            switch (e.NewState)
            {
                case SessionState.Unlocked:
                    if (currentLogin.IsLocked)
                    {
                        var unlockedState = CreateStateWithChanges(currentLogin, state => 
                        {
                            state.IsLocked = false;
                            state.LastActivity = DateTime.UtcNow;
                        });
                        UpdateState(unlockedState, LoginStateChangeReason.Unlock, currentLogin);
                    }
                    break;

                case SessionState.Locked:
                    if (!currentLogin.IsLocked && currentLogin.IsLoggedIn)
                    {
                        var lockedState = CreateStateWithChanges(currentLogin, state => 
                        {
                            state.IsLocked = true;
                            state.LastActivity = DateTime.UtcNow;
                        });
                        UpdateState(lockedState, LoginStateChangeReason.Lock, currentLogin);
                    }
                    break;

                case SessionState.Expired:
                    if (currentLogin.IsLoggedIn)
                    {
                        var expiredState = new LoginState
                        {
                            IsLoggedIn = false,
                            IsLocked = false,
                            Username = null,
                            PublicKeyBase64 = null,
                            LoginTime = null,
                            LastActivity = null,
                            IsNewUser = false
                        };
                        UpdateState(expiredState, LoginStateChangeReason.Timeout, currentLogin);
                    }
                    break;
            }
        }

        /// <summary>
        /// Create a new LoginState based on an existing state with selective modifications.
        /// This eliminates code duplication in state management.
        /// </summary>
        private LoginState CreateStateWithChanges(LoginState source, Action<LoginState> modifications)
        {
            var newState = new LoginState
            {
                IsLoggedIn = source.IsLoggedIn,
                IsLocked = source.IsLocked,
                Username = source.Username,
                PublicKeyBase64 = source.PublicKeyBase64,
                LoginTime = source.LoginTime,
                LastActivity = source.LastActivity,
                IsNewUser = source.IsNewUser
            };
            
            modifications(newState);
            return newState;
        }

        /// <summary>
        /// Update the login state and emit change events
        /// </summary>
        private void UpdateState(LoginState newState, LoginStateChangeReason reason, LoginState previousState)
        {
            lock (_stateLock)
            {
                _currentState = newState;
            }

            var change = new LoginStateChange
            {
                PreviousState = previousState,
                NewState = newState,
                Reason = reason,
                Timestamp = DateTime.UtcNow
            };

            var result = _logger.ExecuteWithLogging(() =>
            {
                _stateChanges.OnNext(change);
                _logger.LogDebug("Login state change emitted: {Reason}", reason);
            }, "Emit state change");
            
            // Log error but don't throw - state emission is not critical
            result.OnFailure(ex => _logger.LogDebug("State change emission failed but continuing"));
        }

        /// <summary>
        /// Attempt to restore a persisted session on service initialization
        /// </summary>
        private async Task TryRestorePersistedSessionAsync()
        {
            var result = await _logger.ExecuteWithLogging(async () =>
            {
                _logger.LogInformation("Checking for persisted session to restore");

                // Check if the session state service supports restoration
                if (_sessionState is ReactiveSessionStateService reactiveSession)
                {
                    var restored = await reactiveSession.TryRestoreSessionAsync();
                    if (restored)
                    {
                        _logger.LogInformation("Persisted session found and restored in locked state");
                        
                        // Update login state to reflect the locked session
                        var previousState = CurrentLoginState;
                        var newState = new LoginState
                        {
                            IsLoggedIn = true, // User is logged in but needs to unlock
                            IsLocked = true,   // Session is locked
                            Username = "", // Will be populated on unlock
                            PublicKeyBase64 = "", // Will be populated on unlock
                            LoginTime = DateTime.UtcNow,
                            LastActivity = DateTime.UtcNow,
                            IsNewUser = false
                        };

                        UpdateState(newState, LoginStateChangeReason.SessionRestored, previousState);
                        
                        _logger.LogInformation("Login state updated to reflect restored locked session");
                    }
                    else
                    {
                        _logger.LogDebug("No persisted session found to restore");
                    }
                }
                else
                {
                    _logger.LogDebug("Session state service does not support persistence");
                }
            }, "Restore persisted session");
            
            // Log error but don't throw - restoration failure should not prevent service initialization
            result.OnFailure(ex => _logger.LogDebug("Session restoration failed but continuing"));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _logger.LogDebug("Disposing LoginAdapterService");

            // Unsubscribe from session state changes
            _sessionState.SessionStateChanged -= OnSessionStateChanged;

            // Complete the observable stream
            _stateChanges.OnCompleted();
            _stateChanges.Dispose();

            _disposed = true;
        }
    }
}