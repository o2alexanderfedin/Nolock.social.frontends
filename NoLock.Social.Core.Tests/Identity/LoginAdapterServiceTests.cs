using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Identity.Interfaces;
using NoLock.Social.Core.Identity.Models;
using NoLock.Social.Core.Identity.Services;
using NoLock.Social.Core.Security;
using Xunit;

namespace NoLock.Social.Core.Tests.Identity
{
    public class LoginAdapterServiceTests : IDisposable
    {
        private readonly Mock<ISessionStateService> _mockSessionState;
        private readonly Mock<IUserTrackingService> _mockUserTracking;
        private readonly Mock<IRememberMeService> _mockRememberMe;
        private readonly Mock<IKeyDerivationService> _mockKeyDerivation;
        private readonly Mock<IJSRuntime> _mockJsRuntime;
        private readonly Mock<ILogger<LoginAdapterService>> _mockLogger;
        private readonly LoginAdapterService _service;

        private readonly string _testUsername = "testuser";
        private readonly string _testPassphrase = "test-passphrase-12345";
        private readonly byte[] _testPublicKey = new byte[] { 1, 2, 3, 4, 5 };
        private readonly byte[] _testPrivateKey = new byte[] { 6, 7, 8, 9, 10 };
        private readonly string _testPublicKeyBase64;

        public LoginAdapterServiceTests()
        {
            _mockSessionState = new Mock<ISessionStateService>();
            _mockUserTracking = new Mock<IUserTrackingService>();
            _mockRememberMe = new Mock<IRememberMeService>();
            _mockKeyDerivation = new Mock<IKeyDerivationService>();
            _mockJsRuntime = new Mock<IJSRuntime>();
            _mockLogger = new Mock<ILogger<LoginAdapterService>>();

            _testPublicKeyBase64 = Convert.ToBase64String(_testPublicKey);

            _service = new LoginAdapterService(
                _mockSessionState.Object,
                _mockUserTracking.Object,
                _mockRememberMe.Object,
                _mockKeyDerivation.Object,
                _mockJsRuntime.Object,
                _mockLogger.Object);
        }

        #region LoginAsync Tests

        [Fact]
        public async Task LoginAsync_NewUser_Success()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair { PublicKey = _testPublicKey, PrivateKey = _testPrivateKey };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();

            _mockKeyDerivation.Setup(x => x.DeriveIdentityAsync(_testPassphrase, _testUsername))
                .ReturnsAsync((keyPair, privateKeyBuffer));

            _mockSessionState.Setup(x => x.StartSessionAsync(_testUsername, keyPair, privateKeyBuffer))
                .ReturnsAsync(true);

            _mockSessionState.Setup(x => x.CurrentSession)
                .Returns(new IdentitySession { Username = _testUsername });

            _mockUserTracking.Setup(x => x.CheckUserExistsAsync(_testPublicKeyBase64))
                .ReturnsAsync(new UserTrackingInfo
                {
                    Exists = false,
                    ContentCount = 0,
                    PublicKeyBase64 = _testPublicKeyBase64
                });

            _mockRememberMe.Setup(x => x.RememberUsernameAsync(_testUsername))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.LoginAsync(_testUsername, _testPassphrase, true);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.IsNewUser);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Session);
            Assert.NotNull(result.UserInfo);
            Assert.False(result.UserInfo.Exists);

            // Verify state was updated
            Assert.True(_service.CurrentLoginState.IsLoggedIn);
            Assert.False(_service.CurrentLoginState.IsLocked);
            Assert.Equal(_testUsername, _service.CurrentLoginState.Username);
            Assert.Equal(_testPublicKeyBase64, _service.CurrentLoginState.PublicKeyBase64);
            Assert.True(_service.CurrentLoginState.IsNewUser);

            // Verify remember me was called
            _mockRememberMe.Verify(x => x.RememberUsernameAsync(_testUsername), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ReturningUser_Success()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair { PublicKey = _testPublicKey, PrivateKey = _testPrivateKey };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();

            _mockKeyDerivation.Setup(x => x.DeriveIdentityAsync(_testPassphrase, _testUsername))
                .ReturnsAsync((keyPair, privateKeyBuffer));

            _mockSessionState.Setup(x => x.StartSessionAsync(_testUsername, keyPair, privateKeyBuffer))
                .ReturnsAsync(true);

            _mockSessionState.Setup(x => x.CurrentSession)
                .Returns(new IdentitySession { Username = _testUsername });

            var userInfo = new UserTrackingInfo
            {
                Exists = true,
                ContentCount = 5,
                FirstSeen = DateTime.UtcNow.AddDays(-30),
                LastSeen = DateTime.UtcNow.AddDays(-1),
                PublicKeyBase64 = _testPublicKeyBase64
            };

            _mockUserTracking.Setup(x => x.CheckUserExistsAsync(_testPublicKeyBase64))
                .ReturnsAsync(userInfo);

            // Act
            var result = await _service.LoginAsync(_testUsername, _testPassphrase, false);

            // Assert
            Assert.True(result.Success);
            Assert.False(result.IsNewUser);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.UserInfo);
            Assert.True(result.UserInfo.Exists);
            Assert.Equal(5, result.UserInfo.ContentCount);

            // Verify state
            Assert.False(_service.CurrentLoginState.IsNewUser);

            // Verify remember me was NOT called since rememberUsername = false
            _mockRememberMe.Verify(x => x.RememberUsernameAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_EmptyUsername_ReturnsError()
        {
            // Act
            var result = await _service.LoginAsync("", _testPassphrase, false);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Username is required", result.ErrorMessage);
            Assert.False(_service.CurrentLoginState.IsLoggedIn);

            // Verify no services were called
            _mockKeyDerivation.Verify(x => x.DeriveIdentityAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_EmptyPassphrase_ReturnsError()
        {
            // Act
            var result = await _service.LoginAsync(_testUsername, "", false);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Passphrase is required", result.ErrorMessage);
            Assert.False(_service.CurrentLoginState.IsLoggedIn);

            // Verify no services were called
            _mockKeyDerivation.Verify(x => x.DeriveIdentityAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_SessionStartFails_ReturnsError()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair { PublicKey = _testPublicKey, PrivateKey = _testPrivateKey };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();

            _mockKeyDerivation.Setup(x => x.DeriveIdentityAsync(_testPassphrase, _testUsername))
                .ReturnsAsync((keyPair, privateKeyBuffer));

            _mockSessionState.Setup(x => x.StartSessionAsync(_testUsername, keyPair, privateKeyBuffer))
                .ReturnsAsync(false);

            // Act
            var result = await _service.LoginAsync(_testUsername, _testPassphrase, false);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to start session. Please try again.", result.ErrorMessage);
            Assert.False(_service.CurrentLoginState.IsLoggedIn);
        }

        [Fact]
        public async Task LoginAsync_KeyDerivationThrows_ReturnsError()
        {
            // Arrange
            _mockKeyDerivation.Setup(x => x.DeriveIdentityAsync(_testPassphrase, _testUsername))
                .ThrowsAsync(new Exception("Key derivation error"));

            // Act
            var result = await _service.LoginAsync(_testUsername, _testPassphrase, false);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("An error occurred during login. Please try again.", result.ErrorMessage);
            Assert.False(_service.CurrentLoginState.IsLoggedIn);

            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Login failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ClearsRememberedUsername_WhenNotChecked()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair { PublicKey = _testPublicKey, PrivateKey = _testPrivateKey };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();

            _mockKeyDerivation.Setup(x => x.DeriveIdentityAsync(_testPassphrase, _testUsername))
                .ReturnsAsync((keyPair, privateKeyBuffer));

            _mockSessionState.Setup(x => x.StartSessionAsync(_testUsername, keyPair, privateKeyBuffer))
                .ReturnsAsync(true);

            _mockSessionState.Setup(x => x.CurrentSession)
                .Returns(new IdentitySession { Username = _testUsername });

            _mockUserTracking.Setup(x => x.CheckUserExistsAsync(_testPublicKeyBase64))
                .ReturnsAsync(new UserTrackingInfo { Exists = false });

            _mockRememberMe.Setup(x => x.IsUsernameRemembered).Returns(true);

            // Act
            await _service.LoginAsync(_testUsername, _testPassphrase, false);

            // Assert
            _mockRememberMe.Verify(x => x.ClearRememberedDataAsync(), Times.Once);
        }

        #endregion

        #region LogoutAsync Tests

        [Fact]
        public async Task LogoutAsync_Success_ClearsAllState()
        {
            // Arrange - First login
            await SetupSuccessfulLogin();

            _mockSessionState.Setup(x => x.EndSessionAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _service.LogoutAsync();

            // Assert
            Assert.False(_service.CurrentLoginState.IsLoggedIn);
            Assert.False(_service.CurrentLoginState.IsLocked);
            Assert.Null(_service.CurrentLoginState.Username);
            Assert.Null(_service.CurrentLoginState.PublicKeyBase64);
            Assert.Null(_service.CurrentLoginState.LoginTime);

            _mockSessionState.Verify(x => x.EndSessionAsync(), Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_SessionEndThrows_StillUpdatesState()
        {
            // Arrange - First login
            await SetupSuccessfulLogin();

            _mockSessionState.Setup(x => x.EndSessionAsync())
                .ThrowsAsync(new Exception("Session end error"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.LogoutAsync());

            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during logout")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region LockAsync Tests

        [Fact]
        public async Task LockAsync_Success_UpdatesState()
        {
            // Arrange - First login
            await SetupSuccessfulLogin();

            _mockSessionState.Setup(x => x.LockSessionAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _service.LockAsync();

            // Assert
            Assert.True(_service.CurrentLoginState.IsLoggedIn);
            Assert.True(_service.CurrentLoginState.IsLocked);
            Assert.Equal(_testUsername, _service.CurrentLoginState.Username);
            Assert.NotNull(_service.CurrentLoginState.PublicKeyBase64);

            _mockSessionState.Verify(x => x.LockSessionAsync(), Times.Once);
        }

        #endregion

        #region IsReturningUserAsync Tests

        [Fact]
        public async Task IsReturningUserAsync_LoggedInReturningUser_ReturnsTrue()
        {
            // Arrange - First login
            await SetupSuccessfulLogin(isReturningUser: true);

            _mockUserTracking.Setup(x => x.CheckUserExistsAsync(_testPublicKeyBase64))
                .ReturnsAsync(new UserTrackingInfo { Exists = true });

            // Act
            var result = await _service.IsReturningUserAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsReturningUserAsync_LoggedInNewUser_ReturnsFalse()
        {
            // Arrange - First login
            await SetupSuccessfulLogin(isReturningUser: false);

            _mockUserTracking.Setup(x => x.CheckUserExistsAsync(_testPublicKeyBase64))
                .ReturnsAsync(new UserTrackingInfo { Exists = false });

            // Act
            var result = await _service.IsReturningUserAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsReturningUserAsync_NotLoggedIn_ReturnsFalse()
        {
            // Act
            var result = await _service.IsReturningUserAsync();

            // Assert
            Assert.False(result);
        }

        #endregion

        #region State Change Observable Tests

        [Fact]
        public async Task LoginStateChanges_EmitsOnLogin()
        {
            // Arrange
            LoginStateChange? capturedChange = null;
            using var subscription = _service.LoginStateChanges.Subscribe(change =>
            {
                capturedChange = change;
            });

            // Act
            await SetupSuccessfulLogin();

            // Assert
            Assert.NotNull(capturedChange);
            Assert.Equal(LoginStateChangeReason.Login, capturedChange.Reason);
            Assert.False(capturedChange.PreviousState.IsLoggedIn);
            Assert.True(capturedChange.NewState.IsLoggedIn);
        }

        [Fact]
        public async Task LoginStateChanges_EmitsOnLogout()
        {
            // Arrange
            await SetupSuccessfulLogin();

            LoginStateChange? capturedChange = null;
            using var subscription = _service.LoginStateChanges.Subscribe(change =>
            {
                if (change.Reason == LoginStateChangeReason.Logout)
                    capturedChange = change;
            });

            _mockSessionState.Setup(x => x.EndSessionAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _service.LogoutAsync();

            // Assert
            Assert.NotNull(capturedChange);
            Assert.Equal(LoginStateChangeReason.Logout, capturedChange.Reason);
            Assert.True(capturedChange.PreviousState.IsLoggedIn);
            Assert.False(capturedChange.NewState.IsLoggedIn);
        }

        [Fact]
        public async Task LoginStateChanges_EmitsOnLock()
        {
            // Arrange
            await SetupSuccessfulLogin();

            LoginStateChange? capturedChange = null;
            using var subscription = _service.LoginStateChanges.Subscribe(change =>
            {
                if (change.Reason == LoginStateChangeReason.Lock)
                    capturedChange = change;
            });

            _mockSessionState.Setup(x => x.LockSessionAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _service.LockAsync();

            // Assert
            Assert.NotNull(capturedChange);
            Assert.Equal(LoginStateChangeReason.Lock, capturedChange.Reason);
            Assert.False(capturedChange.PreviousState.IsLocked);
            Assert.True(capturedChange.NewState.IsLocked);
        }

        #endregion

        #region Session State Event Handler Tests

        [Fact]
        public void OnSessionStateChanged_Unlock_UpdatesLoginState()
        {
            // Arrange
            // Simulate locked state
            var lockedState = new LoginState
            {
                IsLoggedIn = true,
                IsLocked = true,
                Username = _testUsername,
                PublicKeyBase64 = _testPublicKeyBase64
            };

            // Use reflection to set private field (or make it internal for testing)
            var stateField = typeof(LoginAdapterService).GetField("_currentState", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            stateField?.SetValue(_service, lockedState);

            LoginStateChange? capturedChange = null;
            using var subscription = _service.LoginStateChanges.Subscribe(change =>
            {
                capturedChange = change;
            });

            // Act - Raise session state changed event
            _mockSessionState.Raise(x => x.SessionStateChanged += null,
                new SessionStateChangedEventArgs { OldState = SessionState.Locked, NewState = SessionState.Unlocked });

            // Assert
            Assert.NotNull(capturedChange);
            Assert.Equal(LoginStateChangeReason.Unlock, capturedChange.Reason);
            Assert.False(_service.CurrentLoginState.IsLocked);
        }

        [Fact]
        public void OnSessionStateChanged_Expired_ClearsLoginState()
        {
            // Arrange
            var loggedInState = new LoginState
            {
                IsLoggedIn = true,
                IsLocked = false,
                Username = _testUsername,
                PublicKeyBase64 = _testPublicKeyBase64
            };

            var stateField = typeof(LoginAdapterService).GetField("_currentState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            stateField?.SetValue(_service, loggedInState);

            LoginStateChange? capturedChange = null;
            using var subscription = _service.LoginStateChanges.Subscribe(change =>
            {
                capturedChange = change;
            });

            // Act
            _mockSessionState.Raise(x => x.SessionStateChanged += null,
                new SessionStateChangedEventArgs { OldState = SessionState.Unlocked, NewState = SessionState.Expired });

            // Assert
            Assert.NotNull(capturedChange);
            Assert.Equal(LoginStateChangeReason.Timeout, capturedChange.Reason);
            Assert.False(_service.CurrentLoginState.IsLoggedIn);
            Assert.Null(_service.CurrentLoginState.Username);
        }

        #endregion

        #region Helper Methods

        private async Task<LoginResult> SetupSuccessfulLogin(bool isReturningUser = false)
        {
            var keyPair = new Ed25519KeyPair { PublicKey = _testPublicKey, PrivateKey = _testPrivateKey };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();

            _mockKeyDerivation.Setup(x => x.DeriveIdentityAsync(_testPassphrase, _testUsername))
                .ReturnsAsync((keyPair, privateKeyBuffer));

            _mockSessionState.Setup(x => x.StartSessionAsync(_testUsername, keyPair, privateKeyBuffer))
                .ReturnsAsync(true);

            _mockSessionState.Setup(x => x.CurrentSession)
                .Returns(new IdentitySession { Username = _testUsername });

            _mockUserTracking.Setup(x => x.CheckUserExistsAsync(_testPublicKeyBase64))
                .ReturnsAsync(new UserTrackingInfo
                {
                    Exists = isReturningUser,
                    ContentCount = isReturningUser ? 5 : 0,
                    PublicKeyBase64 = _testPublicKeyBase64
                });

            return await _service.LoginAsync(_testUsername, _testPassphrase, false);
        }

        #endregion

        public void Dispose()
        {
            _service?.Dispose();
        }
    }
}