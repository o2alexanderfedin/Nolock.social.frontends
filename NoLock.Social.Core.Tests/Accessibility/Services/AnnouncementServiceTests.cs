using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using NoLock.Social.Core.Accessibility.Services;
using NoLock.Social.Core.Accessibility.Interfaces;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using NoLock.Social.Core.Common.Constants;

namespace NoLock.Social.Core.Tests.Accessibility.Services
{
    public class AnnouncementServiceTests : IDisposable
    {
        private readonly Mock<ILogger<AnnouncementService>> _mockLogger;
        private readonly AnnouncementService _service;
        private readonly List<AnnouncementEventArgs> _capturedPoliteAnnouncements;
        private readonly List<AnnouncementEventArgs> _capturedAssertiveAnnouncements;

        public AnnouncementServiceTests()
        {
            _mockLogger = new Mock<ILogger<AnnouncementService>>();
            _service = new AnnouncementService(_mockLogger.Object);
            _capturedPoliteAnnouncements = new List<AnnouncementEventArgs>();
            _capturedAssertiveAnnouncements = new List<AnnouncementEventArgs>();

            // Subscribe to events to capture announcements
            _service.OnPoliteAnnouncement += (sender, args) => _capturedPoliteAnnouncements.Add(args);
            _service.OnAssertiveAnnouncement += (sender, args) => _capturedAssertiveAnnouncements.Add(args);
        }

        public void Dispose()
        {
            _service.OnPoliteAnnouncement -= (sender, args) => _capturedPoliteAnnouncements.Add(args);
            _service.OnAssertiveAnnouncement -= (sender, args) => _capturedAssertiveAnnouncements.Add(args);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new AnnouncementService(null!));
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithValidLogger_CreatesInstance()
        {
            // Act
            var service = new AnnouncementService(_mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeAssignableTo<IAnnouncementService>();
        }

        #endregion

        #region AnnouncePoliteAsync Tests

        [Fact]
        public async Task AnnouncePoliteAsync_WithValidMessage_FiresEventAndClearsAfterDelay()
        {
            // Arrange
            const string message = "Test polite announcement";
            const AnnouncementCategory category = AnnouncementCategory.General;

            // Act
            await _service.AnnouncePoliteAsync(message, category);

            // Assert
            _capturedPoliteAnnouncements.Should().HaveCount(2); // Initial and clear
            
            var initialAnnouncement = _capturedPoliteAnnouncements[0];
            initialAnnouncement.Message.Should().Be(message);
            initialAnnouncement.Category.Should().Be(category);
            initialAnnouncement.IsAssertive.Should().BeFalse();
            initialAnnouncement.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

            var clearAnnouncement = _capturedPoliteAnnouncements[1];
            clearAnnouncement.Message.Should().BeEmpty();
            clearAnnouncement.Category.Should().Be(category);
            clearAnnouncement.IsAssertive.Should().BeFalse();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AnnouncePoliteAsync_WithInvalidMessage_LogsWarningAndReturns(string? invalidMessage)
        {
            // Act
            await _service.AnnouncePoliteAsync(invalidMessage!);

            // Assert
            _capturedPoliteAnnouncements.Should().BeEmpty();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("empty message")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(AnnouncementCategory.General)]
        [InlineData(AnnouncementCategory.Navigation)]
        [InlineData(AnnouncementCategory.CameraOperation)]
        [InlineData(AnnouncementCategory.PageManagement)]
        public async Task AnnouncePoliteAsync_WithDifferentCategories_PreservesCategory(AnnouncementCategory category)
        {
            // Arrange
            const string message = "Category test";

            // Act
            await _service.AnnouncePoliteAsync(message, category);

            // Assert
            _capturedPoliteAnnouncements.Should().NotBeEmpty();
            _capturedPoliteAnnouncements[0].Category.Should().Be(category);
        }

        [Fact]
        public async Task AnnouncePoliteAsync_WithExceptionInHandler_LogsErrorAndContinues()
        {
            // Arrange
            const string message = "Test message";
            var exceptionMessage = "Handler error";
            _service.OnPoliteAnnouncement += (sender, args) => throw new InvalidOperationException(exceptionMessage);

            // Act
            await _service.AnnouncePoliteAsync(message);

            // Assert - Should log error but not throw
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error making polite announcement")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region AnnounceAssertiveAsync Tests

        [Fact]
        public async Task AnnounceAssertiveAsync_WithValidMessage_FiresEventAndClearsAfterLongerDelay()
        {
            // Arrange
            const string message = "Test assertive announcement";
            const AnnouncementCategory category = AnnouncementCategory.Error;

            // Act
            await _service.AnnounceAssertiveAsync(message, category);

            // Assert
            _capturedAssertiveAnnouncements.Should().HaveCount(2); // Initial and clear
            
            var initialAnnouncement = _capturedAssertiveAnnouncements[0];
            initialAnnouncement.Message.Should().Be(message);
            initialAnnouncement.Category.Should().Be(category);
            initialAnnouncement.IsAssertive.Should().BeTrue();
            initialAnnouncement.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

            var clearAnnouncement = _capturedAssertiveAnnouncements[1];
            clearAnnouncement.Message.Should().BeEmpty();
            clearAnnouncement.Category.Should().Be(category);
            clearAnnouncement.IsAssertive.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AnnounceAssertiveAsync_WithInvalidMessage_LogsWarningAndReturns(string? invalidMessage)
        {
            // Act
            await _service.AnnounceAssertiveAsync(invalidMessage!);

            // Assert
            _capturedAssertiveAnnouncements.Should().BeEmpty();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("empty message")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(AnnouncementCategory.Error)]
        [InlineData(AnnouncementCategory.OfflineStatus)]
        [InlineData(AnnouncementCategory.QualityFeedback)]
        [InlineData(AnnouncementCategory.VoiceCommand)]
        public async Task AnnounceAssertiveAsync_WithDifferentCategories_PreservesCategory(AnnouncementCategory category)
        {
            // Arrange
            const string message = "Category test";

            // Act
            await _service.AnnounceAssertiveAsync(message, category);

            // Assert
            _capturedAssertiveAnnouncements.Should().NotBeEmpty();
            _capturedAssertiveAnnouncements[0].Category.Should().Be(category);
        }

        #endregion

        #region GetCurrentAnnouncements Tests

        [Fact]
        public void GetCurrentAnnouncements_InitialState_ReturnsBothEmpty()
        {
            // Act
            var polite = _service.GetCurrentPoliteAnnouncement();
            var assertive = _service.GetCurrentAssertiveAnnouncement();

            // Assert
            polite.Should().BeEmpty();
            assertive.Should().BeEmpty();
        }

        [Fact]
        public async Task GetCurrentAnnouncements_DuringPoliteAnnouncement_ReturnsPoliteMessage()
        {
            // Arrange
            const string message = "Current polite";
            
            // Act - Start announcement and immediately check
            var announceTask = _service.AnnouncePoliteAsync(message);
            await Task.Delay(10); // Small delay to ensure announcement is set
            var polite = _service.GetCurrentPoliteAnnouncement();
            var assertive = _service.GetCurrentAssertiveAnnouncement();

            // Assert
            polite.Should().Be(message);
            assertive.Should().BeEmpty();
            
            await announceTask; // Clean up
        }

        [Fact]
        public async Task GetCurrentAnnouncements_DuringAssertiveAnnouncement_ReturnsAssertiveMessage()
        {
            // Arrange
            const string message = "Current assertive";
            
            // Act - Start announcement and immediately check
            var announceTask = _service.AnnounceAssertiveAsync(message);
            await Task.Delay(10); // Small delay to ensure announcement is set
            var polite = _service.GetCurrentPoliteAnnouncement();
            var assertive = _service.GetCurrentAssertiveAnnouncement();

            // Assert
            polite.Should().BeEmpty();
            assertive.Should().Be(message);
            
            await announceTask; // Clean up
        }

        #endregion

        #region ClearAnnouncementsAsync Tests

        [Fact]
        public async Task ClearAnnouncementsAsync_WithActiveAnnouncements_ClearsBothAndFiresEvents()
        {
            // Arrange - Set up announcements without waiting for clear
            var politeTask = Task.Run(async () => await _service.AnnouncePoliteAsync("Polite"));
            var assertiveTask = Task.Run(async () => await _service.AnnounceAssertiveAsync("Assertive"));
            await Task.Delay(50); // Give tasks time to start
            
            _capturedPoliteAnnouncements.Clear();
            _capturedAssertiveAnnouncements.Clear();

            // Act
            await _service.ClearAnnouncementsAsync();

            // Assert
            var polite = _service.GetCurrentPoliteAnnouncement();
            var assertive = _service.GetCurrentAssertiveAnnouncement();
            polite.Should().BeEmpty();
            assertive.Should().BeEmpty();

            // Should fire clear events
            _capturedPoliteAnnouncements.Should().ContainSingle();
            _capturedPoliteAnnouncements[0].Message.Should().BeEmpty();
            
            _capturedAssertiveAnnouncements.Should().ContainSingle();
            _capturedAssertiveAnnouncements[0].Message.Should().BeEmpty();
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task MultipleAnnouncements_InRapidSuccession_AllProcessedCorrectly()
        {
            // Arrange
            var messages = new[] { "First", "Second", "Third", "Fourth", "Fifth" };

            // Act
            var tasks = messages.Select(msg => _service.AnnouncePoliteAsync(msg)).ToArray();
            await Task.WhenAll(tasks);

            // Assert - Each message should produce 2 events (announce and clear)
            _capturedPoliteAnnouncements.Should().HaveCount(messages.Length * 2);
        }

        [Fact]
        public async Task MixedAnnouncements_PoliteAndAssertive_BothHandledIndependently()
        {
            // Arrange & Act
            var politeTask = _service.AnnouncePoliteAsync("Polite message");
            var assertiveTask = _service.AnnounceAssertiveAsync("Assertive message");
            
            await Task.WhenAll(politeTask, assertiveTask);

            // Assert
            _capturedPoliteAnnouncements.Should().HaveCount(2);
            _capturedPoliteAnnouncements[0].Message.Should().Be("Polite message");
            
            _capturedAssertiveAnnouncements.Should().HaveCount(2);
            _capturedAssertiveAnnouncements[0].Message.Should().Be("Assertive message");
        }

        [Fact]
        public async Task AnnouncePoliteAsync_WithNavigationCategory_UsesCorrectSettings()
        {
            // Act
            await _service.AnnouncePoliteAsync("Navigation update", AnnouncementCategory.Navigation);

            // Assert
            _capturedPoliteAnnouncements.Should().HaveCount(2);
            _capturedPoliteAnnouncements[0].Category.Should().Be(AnnouncementCategory.Navigation);
            _capturedPoliteAnnouncements[0].IsAssertive.Should().BeFalse();
        }

        [Fact]
        public async Task AnnounceAssertiveAsync_WithOfflineStatusCategory_UsesCorrectSettings()
        {
            // Act
            await _service.AnnounceAssertiveAsync("System offline", AnnouncementCategory.OfflineStatus);

            // Assert
            _capturedAssertiveAnnouncements.Should().HaveCount(2);
            _capturedAssertiveAnnouncements[0].Category.Should().Be(AnnouncementCategory.OfflineStatus);
            _capturedAssertiveAnnouncements[0].IsAssertive.Should().BeTrue();
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task VeryLongMessage_HandledCorrectly()
        {
            // Arrange
            var longMessage = new string('x', 10000);

            // Act
            await _service.AnnouncePoliteAsync(longMessage);

            // Assert
            _capturedPoliteAnnouncements.Should().HaveCount(2);
            _capturedPoliteAnnouncements[0].Message.Should().Be(longMessage);
        }

        [Fact]
        public async Task SpecialCharactersInMessage_PreservedCorrectly()
        {
            // Arrange
            const string specialMessage = "Test <>&\"'`~!@#$%^&*()_+-=[]{}|;:,.<>?/\\";

            // Act
            await _service.AnnouncePoliteAsync(specialMessage);

            // Assert
            _capturedPoliteAnnouncements[0].Message.Should().Be(specialMessage);
        }

        [Fact]
        public async Task UnicodeCharactersInMessage_PreservedCorrectly()
        {
            // Arrange
            const string unicodeMessage = "Test 测试 テスト тест 🎉 ✓ ♦";

            // Act
            await _service.AnnouncePoliteAsync(unicodeMessage);

            // Assert
            _capturedPoliteAnnouncements[0].Message.Should().Be(unicodeMessage);
        }

        #endregion
    }
}