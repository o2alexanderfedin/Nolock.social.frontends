using NoLock.Social.Core.Cryptography.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Service for managing cryptographic identity session state
    /// </summary>
    public class SessionStateService : ISessionStateService
    {
        private readonly ISecureMemoryManager _secureMemoryManager;
        private readonly ICryptoJSInteropService _cryptoInterop;
        private readonly ReaderWriterLockSlim _lock = new();
        private IdentitySession? _currentSession;
        private SessionState _currentState = SessionState.Locked;
        private Timer? _timeoutTimer;
        private bool _disposed;

        public SessionStateService(ISecureMemoryManager secureMemoryManager, ICryptoJSInteropService cryptoInterop)
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
                    var oldState = _currentState;

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

                    // Raise state change event
                    RaiseStateChanged(oldState, _currentState, "Session started");

                    return true;
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
        }

        public Task LockSessionAsync()
        {
            _lock.EnterWriteLock();
            try
            {
                if (_currentSession == null || _currentState == SessionState.Locked)
                {
                    return Task.CompletedTask;
                }

                var oldState = _currentState;
                _currentSession.IsLocked = true;
                _currentState = SessionState.Locked;

                // Stop timeout timer while locked
                StopTimeoutTimer();

                RaiseStateChanged(oldState, _currentState, "Session locked");

                return Task.CompletedTask;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public async Task<bool> UnlockSessionAsync(string passphrase)
        {
            if (string.IsNullOrEmpty(passphrase))
                throw new ArgumentException("Passphrase cannot be null or empty", nameof(passphrase));

            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_currentSession == null || _currentState != SessionState.Locked)
                {
                    return false;
                }

                var oldState = _currentState;
                _currentState = SessionState.Unlocking;
                RaiseStateChanged(oldState, _currentState, "Attempting unlock");

                try
                {
                    // Derive key from passphrase and username
                    var derivedKey = await _cryptoInterop.DeriveKeyArgon2idAsync(passphrase, _currentSession.Username);
                    var keyPair = await _cryptoInterop.GenerateEd25519KeyPairFromSeedAsync(derivedKey);

                    // Verify the public key matches
                    if (!keyPair.PublicKey.SequenceEqual(_currentSession.PublicKey))
                    {
                        _currentState = SessionState.Locked;
                        RaiseStateChanged(SessionState.Unlocking, _currentState, "Invalid passphrase");
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

                        RaiseStateChanged(SessionState.Unlocking, _currentState, "Session unlocked");
                        return true;
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
                }
                catch (Exception ex)
                {
                    _currentState = SessionState.Locked;
                    RaiseStateChanged(SessionState.Unlocking, _currentState, $"Unlock failed: {ex.Message}");
                    return false;
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public async Task EndSessionAsync()
        {
            _lock.EnterWriteLock();
            try
            {
                await EndSessionInternalAsync();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private async Task EndSessionInternalAsync()
        {
            if (_currentSession == null)
            {
                return;
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

            RaiseStateChanged(oldState, _currentState, "Session ended");

            await Task.CompletedTask;
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
                        var oldState = _currentState;
                        _currentState = SessionState.Expired;
                        _currentSession.IsLocked = true;

                        StopTimeoutTimer();

                        RaiseStateChanged(oldState, _currentState, "Session timed out");
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

        public void Dispose()
        {
            if (_disposed)
                return;

            EndSessionAsync().Wait();
            StopTimeoutTimer();
            _lock?.Dispose();
            _disposed = true;
        }
    }
}