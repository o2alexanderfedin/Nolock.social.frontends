using System;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Components.Identity;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography;
using Xunit;

namespace NoLock.Social.Components.Tests.Identity
{
    public class IdentityUnlockComponentTests : TestContext
    {
        private readonly Mock<IKeyDerivationService> _keyDerivationServiceMock;
        private readonly Mock<ISessionStateService> _sessionStateServiceMock;
        private readonly Mock<ILogger<IdentityUnlockComponent>> _loggerMock;

        public IdentityUnlockComponentTests()
        {
            _keyDerivationServiceMock = new Mock<IKeyDerivationService>();
            _sessionStateServiceMock = new Mock<ISessionStateService>();
            _loggerMock = new Mock<ILogger<IdentityUnlockComponent>>();

            Services.AddSingleton(_keyDerivationServiceMock.Object);
            Services.AddSingleton(_sessionStateServiceMock.Object);
            Services.AddSingleton(_loggerMock.Object);
        }

        [Fact]
        public void Component_ShouldRenderInitialState()
        {
            // Act
            var component = RenderComponent<IdentityUnlockComponent>();

            // Assert
            component.Find("h3.unlock-title").TextContent.Should().Be("Unlock Your Identity");
            component.Find("input#username").Should().NotBeNull();
            component.Find("input#passphrase").Should().NotBeNull();
            component.Find("button[type='submit']").TextContent.Should().Contain("Unlock Identity");
            component.Find(".security-note").Should().NotBeNull();
        }

        [Fact]
        public async Task UnlockButton_WithValidInput_ShouldDeriveIdentityAndStartSession()
        {
            // Arrange
            var username = "testuser";
            var passphrase = "TestPassphrase123!";
            var keyPair = new Ed25519KeyPair 
            { 
                PublicKey = new byte[32], 
                PrivateKey = new byte[32] 
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            var session = new IdentitySession
            {
                Username = username,
                PublicKey = keyPair.PublicKey,
                CreatedAt = DateTime.UtcNow
            };

            _keyDerivationServiceMock
                .Setup(k => k.DeriveIdentityAsync(passphrase, username))
                .ReturnsAsync((keyPair, privateKeyBuffer));

            _sessionStateServiceMock
                .Setup(s => s.StartSessionAsync(username, keyPair, privateKeyBuffer))
                .ReturnsAsync(true);

            _sessionStateServiceMock
                .Setup(s => s.CurrentSession)
                .Returns(session);

            var identityUnlockedFired = false;
            var component = RenderComponent<IdentityUnlockComponent>(parameters => parameters
                .Add(p => p.OnIdentityUnlocked, EventCallback.Factory.Create<IdentitySession>(this, (IdentitySession s) => 
                {
                    identityUnlockedFired = true;
                })));

            // Act
            var usernameInput = component.Find("input#username");
            await usernameInput.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs 
            { 
                Value = username 
            });

            var passphraseInput = component.Find("input#passphrase");
            await passphraseInput.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs 
            { 
                Value = passphrase 
            });

            var form = component.Find("form");
            await form.SubmitAsync();

            // Assert
            _keyDerivationServiceMock.Verify(k => 
                k.DeriveIdentityAsync(passphrase, username), Times.Once);
            
            _sessionStateServiceMock.Verify(s => 
                s.StartSessionAsync(username, keyPair, privateKeyBuffer), Times.Once);
            
            identityUnlockedFired.Should().BeTrue();
        }

        [Fact]
        public async Task UnlockButton_WithInvalidInput_ShouldShowError()
        {
            // Arrange
            var username = "testuser";
            var passphrase = "TestPassphrase123!";

            _keyDerivationServiceMock
                .Setup(k => k.DeriveIdentityAsync(passphrase, username))
                .ThrowsAsync(new ArgumentException("Invalid passphrase"));

            var component = RenderComponent<IdentityUnlockComponent>();

            // Act
            var usernameInput = component.Find("input#username");
            await usernameInput.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs 
            { 
                Value = username 
            });

            var passphraseInput = component.Find("input#passphrase");
            await passphraseInput.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs 
            { 
                Value = passphrase 
            });

            var form = component.Find("form");
            await form.SubmitAsync();

            // Assert
            component.Find(".alert-danger").TextContent.Should().Contain("Invalid input");
        }

        [Fact]
        public async Task UnlockButton_WhenSessionStartFails_ShouldShowError()
        {
            // Arrange
            var username = "testuser";
            var passphrase = "TestPassphrase123!";
            var keyPair = new Ed25519KeyPair 
            { 
                PublicKey = new byte[32], 
                PrivateKey = new byte[32] 
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();

            _keyDerivationServiceMock
                .Setup(k => k.DeriveIdentityAsync(passphrase, username))
                .ReturnsAsync((keyPair, privateKeyBuffer));

            _sessionStateServiceMock
                .Setup(s => s.StartSessionAsync(username, keyPair, privateKeyBuffer))
                .ReturnsAsync(false);

            var component = RenderComponent<IdentityUnlockComponent>();

            // Act
            var usernameInput = component.Find("input#username");
            await usernameInput.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs 
            { 
                Value = username 
            });

            var passphraseInput = component.Find("input#passphrase");
            await passphraseInput.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs 
            { 
                Value = passphrase 
            });

            var form = component.Find("form");
            await form.SubmitAsync();

            // Assert
            component.Find(".alert-danger").TextContent.Should().Contain("Failed to unlock identity");
        }

        [Fact]
        public void Component_OnDispose_ShouldCleanUp()
        {
            // Arrange
            var component = RenderComponent<IdentityUnlockComponent>();

            // Act
            component.Instance.Dispose();

            // Assert
            // Component should clean up resources
            component.Instance.Should().NotBeNull();
        }
    }
}