using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Components.Identity;
using NoLock.Social.Core.Identity.Interfaces;
using NoLock.Social.Core.Identity.Models;
using Xunit;

namespace NoLock.Social.Components.Tests.Identity
{
    public class LoginAdapterComponentTests : TestContext
    {
        private readonly Mock<ILoginAdapterService> _mockLoginService;
        private readonly Mock<IRememberMeService> _mockRememberMeService;
        private readonly Mock<ILogger<LoginAdapterComponent>> _mockLogger;
        private readonly Subject<LoginStateChange> _loginStateChanges;

        public LoginAdapterComponentTests()
        {
            _mockLoginService = new Mock<ILoginAdapterService>();
            _mockRememberMeService = new Mock<IRememberMeService>();
            _mockLogger = new Mock<ILogger<LoginAdapterComponent>>();
            _loginStateChanges = new Subject<LoginStateChange>();

            // Setup default login state
            _mockLoginService.Setup(x => x.CurrentLoginState)
                .Returns(new LoginState { IsLoggedIn = false });

            _mockLoginService.Setup(x => x.LoginStateChanges)
                .Returns(_loginStateChanges);

            // Register services
            Services.AddSingleton(_mockLoginService.Object);
            Services.AddSingleton(_mockRememberMeService.Object);
            Services.AddSingleton(_mockLogger.Object);
        }

        #region Initial Render Tests

        [Fact]
        public void Component_InitialRender_ShowsLoginForm()
        {
            // Act
            var component = RenderComponent<LoginAdapterComponent>();

            // Assert
            Assert.NotNull(component.Find(".login-card"));
            Assert.NotNull(component.Find("input#username"));
            Assert.NotNull(component.Find("input#passphrase"));
            Assert.NotNull(component.Find("input#rememberMe"));
            Assert.NotNull(component.Find("button[type='submit']"));
        }

        [Fact]
        public void Component_LoggedInState_ShowsLoggedInStatus()
        {
            // Arrange
            _mockLoginService.Setup(x => x.CurrentLoginState)
                .Returns(new LoginState
                {
                    IsLoggedIn = true,
                    Username = "testuser",
                    LoginTime = DateTime.UtcNow
                });

            // Act
            var component = RenderComponent<LoginAdapterComponent>();

            // Assert
            Assert.NotNull(component.Find(".logged-in-status"));
            Assert.Contains("Welcome, testuser!", component.Markup);
            Assert.NotNull(component.Find("button:contains('Logout')"));
        }

        [Fact]
        public async Task Component_RememberedUsername_PreFillsForm()
        {
            // Arrange
            _mockRememberMeService.Setup(x => x.GetRememberedUsernameAsync())
                .ReturnsAsync("remembereduser");

            // Act
            var component = RenderComponent<LoginAdapterComponent>();
            await Task.Delay(50); // Allow async initialization

            // Assert
            var usernameInput = component.Find("input#username");
            Assert.Equal("remembereduser", usernameInput.GetAttribute("value"));
            
            var rememberCheckbox = component.Find("input#rememberMe");
            Assert.Equal("True", rememberCheckbox.GetAttribute("value"));
            
            Assert.NotNull(component.Find("button:contains('Forget saved username')"));
        }

        #endregion

        #region Login Flow Tests

        [Fact]
        public async Task LoginForm_SuccessfulLogin_NewUser_ShowsWelcomeMessage()
        {
            // Arrange
            var loginResult = new LoginResult
            {
                Success = true,
                IsNewUser = true,
                UserInfo = new UserTrackingInfo { Exists = false }
            };

            _mockLoginService.Setup(x => x.LoginAsync("newuser", "passphrase123", false))
                .ReturnsAsync(loginResult);

            var component = RenderComponent<LoginAdapterComponent>(parameters => parameters
                .Add(p => p.ShowNewUserWelcome, true));

            // Act
            component.Find("input#username").Change("newuser");
            component.Find("input#passphrase").Change("passphrase123");
            await component.Find("form").SubmitAsync();

            // Assert
            Assert.Contains("Welcome to NoLock.Social, newuser!", component.Markup);
            Assert.Contains("Your identity has been created", component.Markup);
        }

        [Fact]
        public async Task LoginForm_SuccessfulLogin_ReturningUser_ShowsWelcomeBack()
        {
            // Arrange
            var loginResult = new LoginResult
            {
                Success = true,
                IsNewUser = false,
                UserInfo = new UserTrackingInfo { Exists = true, ContentCount = 5 }
            };

            _mockLoginService.Setup(x => x.LoginAsync("returninguser", "passphrase123", false))
                .ReturnsAsync(loginResult);

            var component = RenderComponent<LoginAdapterComponent>();

            // Act
            component.Find("input#username").Change("returninguser");
            component.Find("input#passphrase").Change("passphrase123");
            await component.Find("form").SubmitAsync();

            // Assert
            Assert.Contains("Welcome back, returninguser!", component.Markup);
        }

        [Fact]
        public async Task LoginForm_FailedLogin_ShowsErrorMessage()
        {
            // Arrange
            var loginResult = new LoginResult
            {
                Success = false,
                ErrorMessage = "Invalid credentials"
            };

            _mockLoginService.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(loginResult);

            var component = RenderComponent<LoginAdapterComponent>();

            // Act
            component.Find("input#username").Change("user");
            component.Find("input#passphrase").Change("wrong");
            await component.Find("form").SubmitAsync();

            // Assert
            Assert.Contains("Invalid credentials", component.Markup);
            Assert.Contains("alert-danger", component.Markup);
        }

        [Fact]
        public async Task LoginForm_RememberUsername_CallsRememberService()
        {
            // Arrange
            var loginResult = new LoginResult { Success = true };
            _mockLoginService.Setup(x => x.LoginAsync("user", "pass", true))
                .ReturnsAsync(loginResult);

            var component = RenderComponent<LoginAdapterComponent>();

            // Act
            component.Find("input#username").Change("user");
            component.Find("input#passphrase").Change("pass");
            component.Find("input#rememberMe").Change(true);
            await component.Find("form").SubmitAsync();

            // Assert
            _mockLoginService.Verify(x => x.LoginAsync("user", "pass", true), Times.Once);
        }

        [Fact]
        public async Task LoginForm_Processing_ShowsLoadingState()
        {
            // Arrange
            var tcs = new TaskCompletionSource<LoginResult>();
            _mockLoginService.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(tcs.Task);

            var component = RenderComponent<LoginAdapterComponent>();

            // Act
            component.Find("input#username").Change("user");
            component.Find("input#passphrase").Change("pass");
            var submitTask = component.Find("form").SubmitAsync();

            // Assert - while processing
            Assert.Contains("Deriving cryptographic keys", component.Markup);
            Assert.Contains("progress-bar", component.Markup);
            Assert.Contains("Processing...", component.Markup);
            
            var submitButton = component.Find("button[type='submit']");
            Assert.Equal("true", submitButton.GetAttribute("disabled"));

            // Complete the login
            tcs.SetResult(new LoginResult { Success = true });
            await submitTask;
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void LoginForm_EmptyUsername_ShowsValidationError()
        {
            // Arrange
            var component = RenderComponent<LoginAdapterComponent>();

            // Act
            component.Find("input#username").Change("");
            component.Find("input#passphrase").Change("validpassphrase");
            component.Find("form").Submit();

            // Assert
            Assert.Contains("Username is required", component.Markup);
        }

        [Fact]
        public void LoginForm_ShortUsername_ShowsValidationError()
        {
            // Arrange
            var component = RenderComponent<LoginAdapterComponent>();

            // Act
            component.Find("input#username").Change("ab");
            component.Find("input#passphrase").Change("validpassphrase");
            component.Find("form").Submit();

            // Assert
            Assert.Contains("Username must be between 3 and 50 characters", component.Markup);
        }

        [Fact]
        public void LoginForm_InvalidUsernameCharacters_ShowsValidationError()
        {
            // Arrange
            var component = RenderComponent<LoginAdapterComponent>();

            // Act
            component.Find("input#username").Change("user@name");
            component.Find("input#passphrase").Change("validpassphrase");
            component.Find("form").Submit();

            // Assert
            Assert.Contains("Username can only contain letters, numbers, hyphens, and underscores", component.Markup);
        }

        [Fact]
        public void LoginForm_ShortPassphrase_ShowsValidationError()
        {
            // Arrange
            var component = RenderComponent<LoginAdapterComponent>();

            // Act
            component.Find("input#username").Change("validuser");
            component.Find("input#passphrase").Change("short");
            component.Find("form").Submit();

            // Assert
            Assert.Contains("Passphrase must be at least 12 characters", component.Markup);
        }

        #endregion

        #region Logout and Lock Tests

        [Fact]
        public async Task LogoutButton_CallsLogoutService()
        {
            // Arrange
            _mockLoginService.Setup(x => x.CurrentLoginState)
                .Returns(new LoginState { IsLoggedIn = true, Username = "user" });
            
            _mockLoginService.Setup(x => x.LogoutAsync())
                .Returns(Task.CompletedTask);

            var logoutCalled = false;
            var component = RenderComponent<LoginAdapterComponent>(parameters => parameters
                .Add(p => p.OnLogout, () => { logoutCalled = true; return Task.CompletedTask; }));

            // Act
            await component.Find("button:contains('Logout')").ClickAsync();

            // Assert
            _mockLoginService.Verify(x => x.LogoutAsync(), Times.Once);
            Assert.True(logoutCalled);
        }

        [Fact]
        public async Task LockButton_CallsLockService()
        {
            // Arrange
            _mockLoginService.Setup(x => x.CurrentLoginState)
                .Returns(new LoginState { IsLoggedIn = true, IsLocked = false, Username = "user" });
            
            _mockLoginService.Setup(x => x.LockAsync())
                .Returns(Task.CompletedTask);

            var lockCalled = false;
            var component = RenderComponent<LoginAdapterComponent>(parameters => parameters
                .Add(p => p.OnLock, () => { lockCalled = true; return Task.CompletedTask; }));

            // Act
            await component.Find("button:contains('Lock Session')").ClickAsync();

            // Assert
            _mockLoginService.Verify(x => x.LockAsync(), Times.Once);
            Assert.True(lockCalled);
        }

        #endregion

        #region Remember Me Tests

        [Fact]
        public async Task ForgetUsernameButton_ClearsRememberedData()
        {
            // Arrange
            _mockRememberMeService.Setup(x => x.GetRememberedUsernameAsync())
                .ReturnsAsync("remembereduser");
            
            _mockRememberMeService.Setup(x => x.ClearRememberedDataAsync())
                .Returns(Task.CompletedTask);

            var component = RenderComponent<LoginAdapterComponent>();
            await Task.Delay(50); // Allow async initialization

            // Act
            await component.Find("button:contains('Forget saved username')").ClickAsync();

            // Assert
            _mockRememberMeService.Verify(x => x.ClearRememberedDataAsync(), Times.Once);
            Assert.Contains("Saved username has been forgotten", component.Markup);
            
            var usernameInput = component.Find("input#username");
            Assert.Equal("", usernameInput.GetAttribute("value"));
        }

        #endregion

        #region Reactive State Updates

        [Fact]
        public void StateChange_UpdatesUIReactively()
        {
            // Arrange
            var component = RenderComponent<LoginAdapterComponent>();
            
            // Initial state - not logged in
            Assert.NotNull(component.Find(".login-card"));

            // Act - Simulate login state change
            _mockLoginService.Setup(x => x.CurrentLoginState)
                .Returns(new LoginState
                {
                    IsLoggedIn = true,
                    Username = "reactiveuser",
                    LoginTime = DateTime.UtcNow
                });
            
            _loginStateChanges.OnNext(new LoginStateChange
            {
                PreviousState = new LoginState { IsLoggedIn = false },
                NewState = new LoginState { IsLoggedIn = true, Username = "reactiveuser" },
                Reason = LoginStateChangeReason.Login
            });

            // Assert - UI should update
            component.WaitForAssertion(() =>
            {
                Assert.NotNull(component.Find(".logged-in-status"));
                Assert.Contains("Welcome, reactiveuser!", component.Markup);
            });
        }

        #endregion

        #region Security Tests

        [Fact]
        public async Task LoginForm_ClearsPassphraseAfterSuccessfulLogin()
        {
            // Arrange
            var loginResult = new LoginResult { Success = true };
            _mockLoginService.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(loginResult);

            var component = RenderComponent<LoginAdapterComponent>();

            // Act
            var passphraseInput = component.Find("input#passphrase");
            passphraseInput.Change("sensitive-passphrase");
            await component.Find("form").SubmitAsync();

            // Assert - passphrase should be cleared
            Assert.Equal("", passphraseInput.GetAttribute("value"));
        }

        [Fact]
        public void Component_ShowsSecurityWarnings()
        {
            // Arrange & Act
            var component = RenderComponent<LoginAdapterComponent>();

            // Assert
            Assert.Contains("Your identity is derived from your username and passphrase", component.Markup);
            Assert.Contains("There is no password reset", component.Markup);
            Assert.Contains("your passphrase IS your identity", component.Markup);
            Assert.Contains("never stores your passphrase", component.Markup);
        }

        #endregion

        public override void Dispose()
        {
            _loginStateChanges?.Dispose();
            base.Dispose();
        }
    }
}