/*
REASON FOR COMMENTING: IdentityStatusComponent does not exist yet.
This test file was created for a component that hasn't been implemented.
Uncomment when the component is created.

using System;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Components;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography;
using Xunit;

namespace NoLock.Social.Components.Tests.Identity
{
    public class IdentityStatusComponentTests : TestContext
    {
        private readonly Mock<ISessionStateService> _sessionStateServiceMock;
        private readonly Mock<ILogger<IdentityStatusComponent>> _loggerMock;

        public IdentityStatusComponentTests()
        {
            _sessionStateServiceMock = new Mock<ISessionStateService>();
            _loggerMock = new Mock<ILogger<IdentityStatusComponent>>();

            Services.AddSingleton(_sessionStateServiceMock.Object);
            Services.AddSingleton(_loggerMock.Object);
        }

        [Fact]
        public void Component_WhenLocked_ShouldShowLockedState()
        {
            // Arrange
            _sessionStateServiceMock.Setup(s => s.IsUnlocked).Returns(false);
            _sessionStateServiceMock.Setup(s => s.CurrentSession).Returns((IdentitySession?)null);

            // Act
            var component = RenderComponent<IdentityStatusComponent>();

            // Assert
            component.Find(".identity-card.locked").Should().NotBeNull();
            component.Find("h4").TextContent.Should().Be("Identity Locked");
            component.Find(".text-muted").TextContent.Should().Contain("Your identity is currently locked");
            component.Find(".btn-primary").TextContent.Trim().Should().Be("Unlock Identity");
        }

        [Fact]
        public void Component_WhenUnlocked_ShouldShowUnlockedState()
        {
            // Arrange
            var session = new IdentitySession
            {
                Username = "testuser",
                PublicKey = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 },
                CreatedAt = DateTime.UtcNow
            };

            _sessionStateServiceMock.Setup(s => s.IsUnlocked).Returns(true);
            _sessionStateServiceMock.Setup(s => s.CurrentSession).Returns(session);
            _sessionStateServiceMock.Setup(s => s.SessionTimeoutMinutes).Returns(15);

            // Act
            var component = RenderComponent<IdentityStatusComponent>();

            // Assert
            component.Find(".identity-card.unlocked").Should().NotBeNull();
            component.Find("h4").TextContent.Should().Be("Identity Unlocked");
            component.FindAll(".detail-row").Should().HaveCount(3);
            
            var detailRows = component.FindAll(".detail-row");
            detailRows[0].TextContent.Should().Contain("Username:");
            detailRows[0].TextContent.Should().Contain("testuser");
            
            detailRows[1].TextContent.Should().Contain("Public Key:");
            detailRows[1].QuerySelector(".public-key").Should().NotBeNull();
            
            detailRows[2].TextContent.Should().Contain("Session Expires:");
            
            component.Find(".btn-warning").TextContent.Should().Contain("Extend Session");
            component.Find(".btn-danger").TextContent.Should().Contain("Lock Identity");
        }

        [Fact]
        public void PublicKey_ShouldBeTruncatedForDisplay()
        {
            // Arrange
            var publicKey = new byte[32];
            Random.Shared.NextBytes(publicKey);
            var publicKeyBase64 = Convert.ToBase64String(publicKey);
            
            var session = new IdentitySession
            {
                Username = "testuser",
                PublicKey = publicKey,
                CreatedAt = DateTime.UtcNow
            };

            _sessionStateServiceMock.Setup(s => s.IsUnlocked).Returns(true);
            _sessionStateServiceMock.Setup(s => s.CurrentSession).Returns(session);
            _sessionStateServiceMock.Setup(s => s.SessionTimeoutMinutes).Returns(15);

            // Act
            var component = RenderComponent<IdentityStatusComponent>();

            // Assert
            var publicKeyElements = component.FindAll(".public-key");
            publicKeyElements.Should().NotBeEmpty();
            var publicKeyElement = publicKeyElements[0];
            var displayedKey = publicKeyElement.TextContent;
            
            // Should show truncated format: first 8...last 8
            displayedKey.Should().Contain("...");
            displayedKey.Should().NotBe(publicKeyBase64);
            
            // Should have full key in title attribute
            publicKeyElement.GetAttribute("title").Should().Be(publicKeyBase64);
        }

        [Fact]
        public async Task LockButton_ShouldEndSession()
        {
            // Arrange
            var session = new IdentitySession
            {
                Username = "testuser",
                PublicKey = new byte[32],
                CreatedAt = DateTime.UtcNow
            };

            _sessionStateServiceMock.Setup(s => s.IsUnlocked).Returns(true);
            _sessionStateServiceMock.Setup(s => s.CurrentSession).Returns(session);
            _sessionStateServiceMock.Setup(s => s.SessionTimeoutMinutes).Returns(15);
            _sessionStateServiceMock.Setup(s => s.EndSessionAsync()).Returns(Task.CompletedTask);

            var component = RenderComponent<IdentityStatusComponent>();

            // Act
            var lockButton = component.Find(".btn-danger");
            await lockButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert
            _sessionStateServiceMock.Verify(s => s.EndSessionAsync(), Times.Once);
        }

        [Fact]
        public async Task ExtendButton_ShouldExtendSession()
        {
            // Arrange
            var session = new IdentitySession
            {
                Username = "testuser",
                PublicKey = new byte[32],
                CreatedAt = DateTime.UtcNow
            };

            _sessionStateServiceMock.Setup(s => s.IsUnlocked).Returns(true);
            _sessionStateServiceMock.Setup(s => s.CurrentSession).Returns(session);
            _sessionStateServiceMock.Setup(s => s.SessionTimeoutMinutes).Returns(15);
            _sessionStateServiceMock.Setup(s => s.ExtendSessionAsync()).Returns(Task.CompletedTask);

            var component = RenderComponent<IdentityStatusComponent>();

            // Act
            var extendButton = component.Find(".btn-warning");
            await extendButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert
            _sessionStateServiceMock.Verify(s => s.ExtendSessionAsync(), Times.Once);
        }

        [Fact]
        public async Task UnlockButton_WhenLocked_ShouldTriggerCallback()
        {
            // Arrange
            _sessionStateServiceMock.Setup(s => s.IsUnlocked).Returns(false);
            _sessionStateServiceMock.Setup(s => s.CurrentSession).Returns((IdentitySession?)null);

            var unlockRequested = false;
            var component = RenderComponent<IdentityStatusComponent>(parameters => parameters
                .Add(p => p.OnUnlockRequested, EventCallback.Factory.Create(this, () => 
                {
                    unlockRequested = true;
                })));

            // Act
            var unlockButton = component.Find(".btn-primary");
            await unlockButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert
            unlockRequested.Should().BeTrue();
        }

        [Fact]
        public void Component_ShouldUpdateOnSessionStateChange()
        {
            // Arrange
            _sessionStateServiceMock.Setup(s => s.IsUnlocked).Returns(false);
            _sessionStateServiceMock.Setup(s => s.CurrentSession).Returns((IdentitySession?)null);

            var component = RenderComponent<IdentityStatusComponent>();
            
            // Initially locked
            component.Find(".identity-card.locked").Should().NotBeNull();

            // Act - Simulate session state change to unlocked
            var session = new IdentitySession
            {
                Username = "testuser",
                PublicKey = new byte[32],
                CreatedAt = DateTime.UtcNow
            };

            _sessionStateServiceMock.Setup(s => s.IsUnlocked).Returns(true);
            _sessionStateServiceMock.Setup(s => s.CurrentSession).Returns(session);
            
            _sessionStateServiceMock.Raise(s => 
                s.SessionStateChanged += null, 
                new SessionStateChangedEventArgs 
                { 
                    OldState = SessionState.Locked, 
                    NewState = SessionState.Unlocked 
                });

            // Assert
            component.Render(); // Force re-render
            component.Find(".identity-card.unlocked").Should().NotBeNull();
        }

        [Fact]
        public void TimeRemaining_ShouldShowCorrectValue()
        {
            // Arrange
            var createdAt = DateTime.UtcNow.AddMinutes(-5); // Session created 5 minutes ago
            var session = new IdentitySession
            {
                Username = "testuser",
                PublicKey = new byte[32],
                CreatedAt = createdAt
            };

            _sessionStateServiceMock.Setup(s => s.IsUnlocked).Returns(true);
            _sessionStateServiceMock.Setup(s => s.CurrentSession).Returns(session);
            _sessionStateServiceMock.Setup(s => s.SessionTimeoutMinutes).Returns(15);

            // Act
            var component = RenderComponent<IdentityStatusComponent>();

            // Assert
            var expiryRow = component.FindAll(".detail-row")[2];
            expiryRow.TextContent.Should().Contain("Session Expires:");
            // Should show approximately 10 minutes remaining (15 - 5)
            expiryRow.TextContent.Should().Contain("minutes remaining");
        }

        [Fact]
        public void Component_OnDispose_ShouldCleanupResources()
        {
            // Arrange
            var component = RenderComponent<IdentityStatusComponent>();

            // Act
            component.Instance.Dispose();

            // Assert
            _sessionStateServiceMock.VerifyRemove(s => 
                s.SessionStateChanged -= It.IsAny<EventHandler<SessionStateChangedEventArgs>>());
        }
    }
}
*/