using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Identity.Interfaces;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Reactive implementation of session state management using Rx.NET
    /// </summary>
    public class ReactiveSessionStateService : IReactiveSessionStateService, ISessionStateService
    {
        private readonly IWebCryptoService _cryptoService;
        private readonly ISecureMemoryManager _secureMemoryManager;
        private readonly ISessionPersistenceService _sessionPersistence;
        private readonly ILogger<ReactiveSessionStateService> _logger;
        
        private readonly ReaderWriterLockSlim _stateLock = new(LockRecursionPolicy.NoRecursion);
        private readonly Timer _timeoutTimer;
        
        // Reactive subjects
        private readonly BehaviorSubject<SessionState> _stateSubject;
        private readonly Subject<SessionStateChangedEventArgs> _stateChangesSubject;
        private readonly BehaviorSubject<TimeSpan> _remainingTimeSubject;
        private readonly Subject<TimeSpan> _timeoutWarningSubject;
        
        private SessionState _currentState = SessionState.Locked;
        private IdentitySession? _currentSession;
        private DateTime _lastActivity;
        private bool _disposed;

        // For backward compatibility with ISessionStateService
        public event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;

        public ReactiveSessionStateService(
            IWebCryptoService cryptoService,
            ISecureMemoryManager secureMemoryManager,
            ISessionPersistenceService sessionPersistence,
            ILogger<ReactiveSessionStateService> logger)
        {
            _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
            _secureMemoryManager = secureMemoryManager ?? throw new ArgumentNullException(nameof(secureMemoryManager));
            _sessionPersistence = sessionPersistence ?? throw new ArgumentNullException(nameof(sessionPersistence));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            SessionTimeoutMinutes = 30; // Default timeout
            
            // Initialize reactive subjects
            _stateSubject = new BehaviorSubject<SessionState>(_currentState);
            _stateChangesSubject = new Subject<SessionStateChangedEventArgs>();
            _remainingTimeSubject = new BehaviorSubject<TimeSpan>(TimeSpan.FromMinutes(SessionTimeoutMinutes));
            _timeoutWarningSubject = new Subject<TimeSpan>();
            
            // Setup timeout timer to check every 30 seconds
            _timeoutTimer = new Timer(async _ => await CheckTimeoutAsync(), null, 
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            
            // Subscribe to state changes for backward compatibility
            _stateChangesSubject.Subscribe(args => 
                SessionStateChanged?.Invoke(this, args));
            
            _logger.LogInformation("ReactiveSessionStateService initialized with {Timeout} minute timeout", 
                SessionTimeoutMinutes);
        }

        #region Properties

        public SessionState CurrentState
        {
            get
            {
                _stateLock.EnterReadLock();
                try
                {
                    return _currentState;
                }
                finally
                {
                    _stateLock.ExitReadLock();
                }
            }
        }

        public IdentitySession? CurrentSession
        {
            get
            {
                _stateLock.EnterReadLock();
                try
                {
                    return _currentSession;
                }
                finally
                {
                    _stateLock.ExitReadLock();
                }
            }
        }

        public bool IsUnlocked
        {
            get
            {
                _stateLock.EnterReadLock();
                try
                {
                    return _currentState == SessionState.Unlocked;
                }
                finally
                {
                    _stateLock.ExitReadLock();
                }
            }
        }

        public int SessionTimeoutMinutes { get; set; }

        #endregion

        #region Reactive Observables

        public IObservable<SessionStateChangedEventArgs> SessionStateChanges => 
            _stateChangesSubject.AsObservable();

        public IObservable<SessionState> StateStream => 
            _stateSubject.AsObservable();

        public IObservable<TimeSpan> RemainingTimeStream => 
            _remainingTimeSubject.AsObservable();

        public IObservable<TimeSpan> TimeoutWarningStream => 
            _timeoutWarningSubject.AsObservable();

        #endregion

        #region Session Management

        public async Task<bool> StartSessionAsync(string username, Ed25519KeyPair keyPair, ISecureBuffer privateKeyBuffer)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty", nameof(username));
            if (keyPair == null)
                throw new ArgumentNullException(nameof(keyPair));
            if (privateKeyBuffer == null)
                throw new ArgumentNullException(nameof(privateKeyBuffer));

            _logger.LogInformation("Starting session for user: {Username}", username);

            SessionState oldState;
            _stateLock.EnterWriteLock();
            try
            {
                // Clean up any existing session
                if (_currentSession != null)
                {
                    _currentSession.PrivateKeyBuffer?.Dispose();
                    _currentSession = null;
                }

                oldState = _currentState;

                _currentSession = new IdentitySession
                {
                    Username = username,
                    PublicKey = keyPair.PublicKey,
                    PrivateKeyBuffer = privateKeyBuffer,
                    CreatedAt = DateTime.UtcNow,
                    IsLocked = false
                };

                _currentState = SessionState.Unlocked;
                _lastActivity = DateTime.UtcNow;
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }

            // Emit state changes
            var args = new SessionStateChangedEventArgs { OldState = oldState, NewState = SessionState.Unlocked, Reason = "Session started" };
            _stateSubject.OnNext(SessionState.Unlocked);
            _stateChangesSubject.OnNext(args);
            _remainingTimeSubject.OnNext(TimeSpan.FromMinutes(SessionTimeoutMinutes));

            // Persist the session for page refresh survival
            try
            {
                var sessionData = new PersistedSessionData
                {
                    SessionId = _currentSession.SessionId,
                    Username = username,
                    PublicKey = keyPair.PublicKey,
                    EncryptedPrivateKey = privateKeyBuffer.Data, // This will be encrypted by the persistence service
                    CreatedAt = _currentSession.CreatedAt,
                    LastActivityAt = _lastActivity,
                    State = SessionState.Unlocked,
                    Version = 1
                };

                // Use a derived key from the public key for encryption (simplified approach)
                // In production, this should use a proper key derivation from the passphrase
                var encryptionKey = new byte[32];
                Array.Copy(keyPair.PublicKey, 0, encryptionKey, 0, Math.Min(32, keyPair.PublicKey.Length));

                await _sessionPersistence.PersistSessionAsync(sessionData, encryptionKey, SessionTimeoutMinutes);
                _logger.LogDebug("Session persisted for refresh survival");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist session, but continuing with in-memory session");
            }

            _logger.LogInformation("Session started successfully for user: {Username}", username);
            return true;
        }

        public async Task LockSessionAsync()
        {
            _logger.LogInformation("Locking session");

            SessionState oldState;
            _stateLock.EnterWriteLock();
            try
            {
                if (_currentState != SessionState.Unlocked)
                {
                    _logger.LogWarning("Cannot lock session - current state is {State}", _currentState);
                    return;
                }

                oldState = _currentState;
                _currentState = SessionState.Locked;
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }

            // Emit state changes
            var args = new SessionStateChangedEventArgs { OldState = oldState, NewState = SessionState.Locked, Reason = "Session locked" };
            _stateSubject.OnNext(SessionState.Locked);
            _stateChangesSubject.OnNext(args);

            await Task.CompletedTask;
            _logger.LogInformation("Session locked");
        }

        public async Task<bool> UnlockSessionAsync(string passphrase)
        {
            if (string.IsNullOrWhiteSpace(passphrase))
            {
                _logger.LogWarning("Unlock attempt with empty passphrase");
                return false;
            }

            _logger.LogInformation("Attempting to unlock session");

            SessionState oldState;
            bool unlocked = false;

            _stateLock.EnterWriteLock();
            try
            {
                if (_currentState != SessionState.Locked || _currentSession == null)
                {
                    _logger.LogWarning("Cannot unlock - session not in locked state or no session exists");
                    return false;
                }

                // In a real implementation, verify the passphrase here
                // For now, we'll assume it's correct
                oldState = _currentState;
                _currentState = SessionState.Unlocked;
                _lastActivity = DateTime.UtcNow;
                unlocked = true;
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }

            if (unlocked)
            {
                // Emit state changes
                var args = new SessionStateChangedEventArgs { OldState = oldState, NewState = SessionState.Unlocked, Reason = "Session unlocked" };
                _stateSubject.OnNext(SessionState.Unlocked);
                _stateChangesSubject.OnNext(args);
                _remainingTimeSubject.OnNext(TimeSpan.FromMinutes(SessionTimeoutMinutes));

                _logger.LogInformation("Session unlocked successfully");
            }

            await Task.CompletedTask;
            return unlocked;
        }

        public async Task EndSessionAsync()
        {
            _logger.LogInformation("Ending session");

            SessionState oldState;
            _stateLock.EnterWriteLock();
            try
            {
                oldState = _currentState;

                // Clean up session
                if (_currentSession != null)
                {
                    _currentSession.PrivateKeyBuffer?.Dispose();
                    _currentSession = null;
                }

                _currentState = SessionState.Expired;
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }

            // Clear persisted session data
            try
            {
                await _sessionPersistence.ClearPersistedSessionAsync();
                _logger.LogDebug("Cleared persisted session data");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear persisted session data");
            }

            // Emit state changes
            var args = new SessionStateChangedEventArgs { OldState = oldState, NewState = SessionState.Expired, Reason = "Session ended" };
            _stateSubject.OnNext(SessionState.Expired);
            _stateChangesSubject.OnNext(args);
            _remainingTimeSubject.OnNext(TimeSpan.Zero);

            await Task.CompletedTask;
            _logger.LogInformation("Session ended");
        }

        public void UpdateActivity()
        {
            _stateLock.EnterWriteLock();
            try
            {
                if (_currentState == SessionState.Unlocked)
                {
                    _lastActivity = DateTime.UtcNow;
                    var remaining = GetRemainingTimeNoLock();
                    _remainingTimeSubject.OnNext(remaining);
                    
                    _logger.LogDebug("Activity updated, {Minutes} minutes remaining", 
                        remaining.TotalMinutes);
                }
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }

        public async Task CheckTimeoutAsync()
        {
            if (_disposed) return;

            bool shouldTimeout = false;
            SessionState oldState = SessionState.Locked;

            _stateLock.EnterReadLock();
            try
            {
                if (_currentState == SessionState.Unlocked)
                {
                    var remaining = GetRemainingTimeNoLock();
                    _remainingTimeSubject.OnNext(remaining);

                    // Emit warning if less than 1 minute remaining
                    if (remaining.TotalMinutes <= 1 && remaining.TotalMinutes > 0)
                    {
                        _timeoutWarningSubject.OnNext(remaining);
                    }

                    if (remaining.TotalSeconds <= 0)
                    {
                        shouldTimeout = true;
                        oldState = _currentState;
                    }
                }
            }
            finally
            {
                _stateLock.ExitReadLock();
            }

            if (shouldTimeout)
            {
                _logger.LogWarning("Session timed out due to inactivity");
                await LockSessionAsync();
            }
        }

        public TimeSpan GetRemainingTime()
        {
            _stateLock.EnterReadLock();
            try
            {
                return GetRemainingTimeNoLock();
            }
            finally
            {
                _stateLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets the remaining time without acquiring a lock.
        /// This method should only be called when a lock is already held.
        /// </summary>
        private TimeSpan GetRemainingTimeNoLock()
        {
            if (_currentState != SessionState.Unlocked)
                return TimeSpan.Zero;

            var elapsed = DateTime.UtcNow - _lastActivity;
            var remaining = TimeSpan.FromMinutes(SessionTimeoutMinutes) - elapsed;
            
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        public async Task ExtendSessionAsync()
        {
            UpdateActivity();
            
            // Also extend the persisted session
            try
            {
                await _sessionPersistence.ExtendSessionExpiryAsync(SessionTimeoutMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extend persisted session expiry");
            }
            
            await Task.CompletedTask;
            _logger.LogInformation("Session extended by {Minutes} minutes", SessionTimeoutMinutes);
        }

        /// <summary>
        /// Attempt to restore a persisted session from storage
        /// </summary>
        public async Task<bool> TryRestoreSessionAsync()
        {
            try
            {
                _logger.LogInformation("Attempting to restore persisted session");

                // Check if there's a valid persisted session
                var hasSession = await _sessionPersistence.HasValidPersistedSessionAsync();
                if (!hasSession)
                {
                    _logger.LogDebug("No valid persisted session found");
                    return false;
                }

                // Get the encrypted session data
                var encryptedSession = await _sessionPersistence.GetPersistedSessionAsync();
                if (encryptedSession == null)
                {
                    _logger.LogDebug("Could not retrieve persisted session");
                    return false;
                }

                _logger.LogInformation("Found persisted session {SessionId}, expires at {ExpiresAt}", 
                    encryptedSession.Metadata.SessionId, encryptedSession.Metadata.ExpiresAt);

                // For now, we can't fully decrypt the session without the passphrase
                // But we can at least indicate that a session exists and prompt for unlock
                // This helps maintain awareness that there's an active session

                SessionState oldState;
                _stateLock.EnterWriteLock();
                try
                {
                    oldState = _currentState;
                    _currentState = SessionState.Locked;
                    
                    // Store basic session info (without keys)
                    _currentSession = new IdentitySession
                    {
                        SessionId = encryptedSession.Metadata.SessionId,
                        Username = "", // Will be populated on unlock
                        PublicKey = Array.Empty<byte>(), // Will be populated on unlock
                        CreatedAt = DateTime.UtcNow,
                        IsLocked = true,
                        PrivateKeyBuffer = null // Will be populated on unlock
                    };
                }
                finally
                {
                    _stateLock.ExitWriteLock();
                }

                // Emit state changes
                var args = new SessionStateChangedEventArgs 
                { 
                    OldState = oldState, 
                    NewState = SessionState.Locked, 
                    Reason = "Session restored from storage (locked)" 
                };
                _stateSubject.OnNext(SessionState.Locked);
                _stateChangesSubject.OnNext(args);

                var remainingTime = await _sessionPersistence.GetRemainingSessionTimeAsync();
                _remainingTimeSubject.OnNext(remainingTime);

                _logger.LogInformation("Session restored in locked state, awaiting unlock");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore persisted session");
                return false;
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogInformation("Disposing ReactiveSessionStateService");

            _timeoutTimer?.Dispose();
            
            _stateLock.EnterWriteLock();
            try
            {
                _currentSession?.PrivateKeyBuffer?.Dispose();
                _currentSession = null;
                _currentState = SessionState.Expired;
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }

            // Complete and dispose subjects
            _stateSubject.OnCompleted();
            _stateSubject.Dispose();
            
            _stateChangesSubject.OnCompleted();
            _stateChangesSubject.Dispose();
            
            _remainingTimeSubject.OnCompleted();
            _remainingTimeSubject.Dispose();
            
            _timeoutWarningSubject.OnCompleted();
            _timeoutWarningSubject.Dispose();

            _stateLock.Dispose();
            _disposed = true;
        }

        #endregion
    }
}