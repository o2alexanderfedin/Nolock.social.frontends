using NoLock.Social.Core.Cryptography.Interfaces;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Service for managing cryptographic identity session state
    /// </summary>
    public class SessionStateService : ISessionStateService, IAsyncDisposable
    {
        private readonly ISecureMemoryManager _secureMemoryManager;
        private readonly IWebCryptoService _cryptoInterop;
        private readonly ReaderWriterLockSlim _lock = new();
        private IdentitySession? _currentSession;
        private SessionState _currentState = SessionState.Locked;
        private Timer? _timeoutTimer;
        private bool _disposed;

        public SessionStateService(ISecureMemoryManager secureMemoryManager, IWebCryptoService cryptoInterop)
        {
            _secureMemoryManager = secureMemoryManager ?? throw new ArgumentNullException(nameof(secureMemoryManager));
            _cryptoInterop = cryptoInterop ?? throw new ArgumentNullException(nameof(cryptoInterop));
            SessionTimeoutMinutes = 15; // Default timeout
        }

        public SessionState CurrentState
        {
            get
            {
                if (_disposed)
                    return SessionState.Locked;
                    
                _lock.EnterReadLock();
                try
                {
                    return _currentState;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
            private set
            {
                if (_disposed)
                    return;
                    
                _lock.EnterWriteLock();
                try
                {
                    _currentState = value;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        public IdentitySession? CurrentSession
        {
            get
            {
                if (_disposed)
                    return null;
                    
                _lock.EnterReadLock();
                try
                {
                    return _currentSession;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public bool IsUnlocked => CurrentState == SessionState.Unlocked;

        public int SessionTimeoutMinutes { get; set; }

        public event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;

        public async Task<bool> StartSessionAsync(string username, Ed25519KeyPair keyPair, ISecureBuffer privateKeyBuffer)
        {
            if (string.IsNullOrEmpty(username))
                throw new ArgumentException("Username cannot be null or empty", nameof(username));
            if (keyPair == null)
                throw new ArgumentNullException(nameof(keyPair));
            if (privateKeyBuffer == null)
                throw new ArgumentNullException(nameof(privateKeyBuffer));

            SessionState oldState;
            _lock.EnterUpgradeableReadLock();
            try
            {
                // Don't allow starting a new session if one is already active
                if (_currentSession != null && _currentState != SessionState.Locked)
                {
                    return false;
                }

                _lock.EnterWriteLock();
                try
                {
                    oldState = _currentState;

                    // Clear any existing session
                    if (_currentSession != null)
                    {
                        await EndSessionInternalAsync();
                    }

                    // Create new session
                    _currentSession = new IdentitySession
                    {
                        Username = username,
                        PublicKey = keyPair.PublicKey,
                        PrivateKeyBuffer = privateKeyBuffer,
                        CreatedAt = DateTime.UtcNow,
                        LastActivityAt = DateTime.UtcNow,
                        IsLocked = false
                    };

                    _currentState = SessionState.Unlocked;

                    // Start timeout timer
                    StartTimeoutTimer();
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }

            // Raise state change event after releasing the lock
            RaiseStateChanged(oldState, SessionState.Unlocked, "Session started");

            return true;
        }

        public Task LockSessionAsync()
        {
            SessionState oldState;
            _lock.EnterWriteLock();
            try
            {
                if (_currentSession == null || _currentState == SessionState.Locked)
                {
                    return Task.CompletedTask;
                }

                oldState = _currentState;
                _currentSession.IsLocked = true;
                _currentState = SessionState.Locked;

                // Stop timeout timer while locked
                StopTimeoutTimer();
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            // Raise state change event after releasing the lock
            RaiseStateChanged(oldState, SessionState.Locked, "Session locked");

            return Task.CompletedTask;
        }

        public async Task<bool> UnlockSessionAsync(string passphrase)
        {
            if (string.IsNullOrEmpty(passphrase))
                throw new ArgumentException("Passphrase cannot be null or empty", nameof(passphrase));

            SessionState oldState;
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_currentSession == null || _currentState != SessionState.Locked)
                {
                    return false;
                }

                oldState = _currentState;
                _currentState = SessionState.Unlocking;
                
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
            
            // Raise state change event after releasing lock
            RaiseStateChanged(oldState, SessionState.Unlocking, "Attempting unlock");

            try
            {
                // Get username for key derivation (need to access session)
                string username;
                byte[] publicKey;
                _lock.EnterReadLock();
                try
                {
                    username = _currentSession.Username;
                    publicKey = _currentSession.PublicKey;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
                
                // Derive key from passphrase and username using PBKDF2
                var saltData = System.Text.Encoding.UTF8.GetBytes(username.ToLowerInvariant());
                var salt = await _cryptoInterop.Sha256Async(saltData);
                var passwordBytes = System.Text.Encoding.UTF8.GetBytes(passphrase);
                var derivedKey = await _cryptoInterop.Pbkdf2Async(passwordBytes, salt, 600000, 32, "SHA-256");
                
                // Generate ECDSA key pair (simulating Ed25519)
                var ecdsaKeyPair = await _cryptoInterop.GenerateECDSAKeyPairAsync("P-256");
                var keyPair = new Ed25519KeyPair 
                { 
                    PublicKey = ecdsaKeyPair.PublicKey, 
                    PrivateKey = ecdsaKeyPair.PrivateKey 
                };

                // Verify the public key matches
                if (!keyPair.PublicKey.SequenceEqual(publicKey))
                {
                    _lock.EnterWriteLock();
                    try
                    {
                        _currentState = SessionState.Locked;
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
                    
                    RaiseStateChanged(SessionState.Unlocking, SessionState.Locked, "Invalid passphrase");
                    return false;
                }

                _lock.EnterWriteLock();
                try
                {
                    _currentSession.IsLocked = false;
                    _currentSession.LastActivityAt = DateTime.UtcNow;
                    _currentState = SessionState.Unlocked;

                    // Restart timeout timer
                    StartTimeoutTimer();
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
                
                RaiseStateChanged(SessionState.Unlocking, SessionState.Unlocked, "Session unlocked");
                return true;
            }
            catch (Exception ex)
            {
                _lock.EnterWriteLock();
                try
                {
                    _currentState = SessionState.Locked;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
                
                RaiseStateChanged(SessionState.Unlocking, SessionState.Locked, $"Unlock failed: {ex.Message}");
                return false;
            }
        }

        public async Task EndSessionAsync()
        {
            SessionState oldState;
            _lock.EnterWriteLock();
            try
            {
                oldState = await EndSessionInternalAsync();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            
            // Raise event after releasing lock
            if (oldState != SessionState.Locked)
            {
                RaiseStateChanged(oldState, SessionState.Locked, "Session ended");
            }
        }

        private async Task<SessionState> EndSessionInternalAsync()
        {
            if (_currentSession == null)
            {
                return _currentState;
            }

            var oldState = _currentState;

            // Clear private key buffer
            _currentSession.PrivateKeyBuffer?.Clear();

            // Clear session data
            if (_currentSession.PublicKey != null)
            {
                Array.Clear(_currentSession.PublicKey, 0, _currentSession.PublicKey.Length);
            }

            _currentSession = null;
            _currentState = SessionState.Locked;

            // Stop timeout timer
            StopTimeoutTimer();

            await Task.CompletedTask;
            
            return oldState;
        }

        public void UpdateActivity()
        {
            _lock.EnterWriteLock();
            try
            {
                if (_currentSession != null && !_currentSession.IsLocked)
                {
                    _currentSession.LastActivityAt = DateTime.UtcNow;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public async Task CheckTimeoutAsync()
        {
            SessionState? oldState = null;
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_currentSession == null || _currentSession.IsLocked)
                {
                    return;
                }

                var timeSinceActivity = DateTime.UtcNow - _currentSession.LastActivityAt;
                if (timeSinceActivity.TotalMinutes >= SessionTimeoutMinutes)
                {
                    _lock.EnterWriteLock();
                    try
                    {
                        oldState = _currentState;
                        _currentState = SessionState.Expired;
                        _currentSession.IsLocked = true;

                        StopTimeoutTimer();
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }

            // Raise event after releasing lock
            if (oldState.HasValue)
            {
                RaiseStateChanged(oldState.Value, SessionState.Expired, "Session timed out");
            }

            await Task.CompletedTask;
        }

        public TimeSpan GetRemainingTime()
        {
            _lock.EnterReadLock();
            try
            {
                if (_currentSession == null || _currentSession.IsLocked)
                {
                    return TimeSpan.Zero;
                }

                var timeSinceActivity = DateTime.UtcNow - _currentSession.LastActivityAt;
                var timeoutSpan = TimeSpan.FromMinutes(SessionTimeoutMinutes);
                var remaining = timeoutSpan - timeSinceActivity;

                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private void StartTimeoutTimer()
        {
            StopTimeoutTimer();

            if (SessionTimeoutMinutes > 0)
            {
                _timeoutTimer = new Timer(async _ => await CheckTimeoutAsync(), null,
                    TimeSpan.FromMinutes(1), // Check every minute
                    TimeSpan.FromMinutes(1));
            }
        }

        private void StopTimeoutTimer()
        {
            _timeoutTimer?.Dispose();
            _timeoutTimer = null;
        }

        private void RaiseStateChanged(SessionState oldState, SessionState newState, string? reason = null)
        {
            SessionStateChanged?.Invoke(this, new SessionStateChangedEventArgs
            {
                OldState = oldState,
                NewState = newState,
                Reason = reason
            });
        }

        public async Task ExtendSessionAsync()
        {
            if (CurrentState != SessionState.Unlocked || _currentSession == null)
                return;

            UpdateActivity();
            await Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            await EndSessionAsync();
            StopTimeoutTimer();
            _lock?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            // For synchronous disposal, we can't safely wait on async operations
            // Just clean up what we can synchronously
            
            // Clear the private key buffer if it exists
            _currentSession?.PrivateKeyBuffer?.Clear();
            
            StopTimeoutTimer();
            _lock?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}