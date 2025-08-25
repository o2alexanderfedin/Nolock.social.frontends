using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using NoLock.Social.Core.Identity.Interfaces;

namespace NoLock.Social.Core.Tests.Cryptography
{
    public class ReactiveSessionStateServiceTests : IDisposable
    {
        private readonly ReactiveSessionStateService _sut;
        private readonly Mock<IWebCryptoService> _cryptoServiceMock;
        private readonly Mock<ISecureMemoryManager> _secureMemoryManagerMock;
        private readonly Mock<ISessionPersistenceService> _sessionPersistenceMock;
        private readonly Mock<ILogger<ReactiveSessionStateService>> _loggerMock;

        public ReactiveSessionStateServiceTests()
        {
            _cryptoServiceMock = new Mock<IWebCryptoService>();
            _secureMemoryManagerMock = new Mock<ISecureMemoryManager>();
            _sessionPersistenceMock = new Mock<ISessionPersistenceService>();
            _loggerMock = new Mock<ILogger<ReactiveSessionStateService>>();
            
            _sut = new ReactiveSessionStateService(
                _cryptoServiceMock.Object,
                _secureMemoryManagerMock.Object,
                _sessionPersistenceMock.Object,
                _loggerMock.Object);
        }

        public void Dispose()
        {
            _sut?.Dispose();
        }

        [Fact]
        public async Task UpdateActivity_WhenUnlocked_ShouldNotCauseRecursiveLock()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            await _sut.StartSessionAsync("testuser", keyPair, privateKeyBuffer);

            // Act & Assert - This should not throw LockRecursionException
            var exception = Record.Exception(() => _sut.UpdateActivity());
            exception.Should().BeNull();
        }

        [Fact]
        public async Task CheckTimeoutAsync_WhenUnlocked_ShouldNotCauseRecursiveLock()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            await _sut.StartSessionAsync("testuser", keyPair, privateKeyBuffer);

            // Act & Assert - This should not throw LockRecursionException
            var exception = await Record.ExceptionAsync(async () => await _sut.CheckTimeoutAsync());
            exception.Should().BeNull();
        }

        [Fact]
        public async Task GetRemainingTime_AfterUpdateActivity_ShouldReturnCorrectTime()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            await _sut.StartSessionAsync("testuser", keyPair, privateKeyBuffer);
            _sut.SessionTimeoutMinutes = 30;

            // Act
            _sut.UpdateActivity();
            var remainingTime = _sut.GetRemainingTime();

            // Assert
            remainingTime.Should().BeCloseTo(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task ConcurrentOperations_ShouldNotCauseDeadlock()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            await _sut.StartSessionAsync("testuser", keyPair, privateKeyBuffer);

            var cancellationTokenSource = new CancellationTokenSource();
            var operationCount = 0;

            // Act - Run multiple concurrent operations for a limited time
            var tasks = new Task[10];
            for (int i = 0; i < 5; i++)
            {
                tasks[i * 2] = Task.Run(() =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        _sut.UpdateActivity();
                        Interlocked.Increment(ref operationCount);
                        Thread.Sleep(5);
                    }
                });

                tasks[i * 2 + 1] = Task.Run(async () =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        await _sut.CheckTimeoutAsync();
                        Interlocked.Increment(ref operationCount);
                        await Task.Delay(5);
                    }
                });
            }

            // Assert - Should complete without deadlock
            var allTasksCompleted = Task.WhenAll(tasks);
            var completedTask = await Task.WhenAny(
                allTasksCompleted,
                Task.Delay(TimeSpan.FromSeconds(5))
            );
            
            var completedInTime = completedTask == allTasksCompleted;
            
            // No deadlock should occur and operations should complete
            completedInTime.Should().BeTrue("Operations should complete without deadlock");
            operationCount.Should().BeGreaterThan(0, "Operations should have executed");
        }

        [Fact]
        public async Task GetRemainingTime_WhenLocked_ShouldReturnZero()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            await _sut.StartSessionAsync("testuser", keyPair, privateKeyBuffer);
            await _sut.LockSessionAsync();

            // Act
            var remainingTime = _sut.GetRemainingTime();

            // Assert
            remainingTime.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public async Task GetRemainingTime_WhenSessionExpired_ShouldReturnZero()
        {
            // Arrange
            await _sut.EndSessionAsync();

            // Act
            var remainingTime = _sut.GetRemainingTime();

            // Assert
            remainingTime.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public async Task UpdateActivity_WhenLocked_ShouldNotUpdateRemainingTime()
        {
            // Arrange
            var keyPair = new Ed25519KeyPair
            {
                PublicKey = new byte[32],
                PrivateKey = new byte[64]
            };
            var privateKeyBuffer = Mock.Of<ISecureBuffer>();
            await _sut.StartSessionAsync("testuser", keyPair, privateKeyBuffer);
            
            // Subscribe before locking to skip the initial value
            TimeSpan? lastEmittedTime = null;
            using var subscription = _sut.RemainingTimeStream
                .Skip(1) // Skip the initial value from BehaviorSubject
                .Subscribe(time => lastEmittedTime = time);
            
            await _sut.LockSessionAsync();

            // Act
            _sut.UpdateActivity();
            await Task.Delay(50); // Give time for any async operations

            // Assert - No time should be emitted when locked
            lastEmittedTime.Should().BeNull("UpdateActivity should not emit time when session is locked");
        }
    }
}