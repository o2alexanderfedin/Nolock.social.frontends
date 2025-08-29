using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;

namespace NoLock.Social.Core.Tests.Cryptography
{
    public class BrowserCompatibilityServiceTests
    {
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly BrowserCompatibilityService _sut;

        public BrowserCompatibilityServiceTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _sut = new BrowserCompatibilityService(_jsRuntimeMock.Object);
        }

        [Fact]
        public async Task IsWebCryptoAvailableAsync_WhenAvailable_ReturnsTrue()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("crypto.checkWebCryptoAvailability", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.IsWebCryptoAvailableAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsWebCryptoAvailableAsync_WhenNotAvailable_ReturnsFalse()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("crypto.checkWebCryptoAvailability", It.IsAny<object[]>()))
                .ReturnsAsync(false);

            // Act
            var result = await _sut.IsWebCryptoAvailableAsync();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsSecureContextAsync_WhenSecure_ReturnsTrue()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("crypto.checkSecureContext", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.IsSecureContextAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsSecureContextAsync_WhenNotSecure_ReturnsFalse()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("crypto.checkSecureContext", It.IsAny<object[]>()))
                .ReturnsAsync(false);

            // Act
            var result = await _sut.IsSecureContextAsync();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetCompatibilityInfoAsync_WhenFullyCompatible_ReturnsCompatibleInfo()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<BrowserCompatibilityInfo>("crypto.getBrowserCompatibilityInfo", It.IsAny<object[]>()))
                .ReturnsAsync(new BrowserCompatibilityInfo
                {
                    IsWebCryptoAvailable = true,
                    IsSecureContext = true,
                    BrowserName = "Chrome",
                    BrowserVersion = "120.0.0",
                    ErrorMessage = string.Empty
                });

            // Act
            var result = await _sut.GetCompatibilityInfoAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsCompatible.Should().BeTrue();
            result.IsWebCryptoAvailable.Should().BeTrue();
            result.IsSecureContext.Should().BeTrue();
            result.BrowserName.Should().Be("Chrome");
            result.BrowserVersion.Should().Be("120.0.0");
            result.ErrorMessage.Should().BeEmpty();
        }

        [Fact]
        public async Task GetCompatibilityInfoAsync_WhenNotSecure_ReturnsIncompatibleInfo()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<BrowserCompatibilityInfo>("crypto.getBrowserCompatibilityInfo", It.IsAny<object[]>()))
                .ReturnsAsync(new BrowserCompatibilityInfo
                {
                    IsWebCryptoAvailable = true,
                    IsSecureContext = false,
                    BrowserName = "Chrome",
                    BrowserVersion = "120.0.0",
                    ErrorMessage = "Application requires HTTPS for cryptographic operations"
                });

            // Act
            var result = await _sut.GetCompatibilityInfoAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsCompatible.Should().BeFalse();
            result.IsWebCryptoAvailable.Should().BeTrue();
            result.IsSecureContext.Should().BeFalse();
            result.ErrorMessage.Should().Contain("HTTPS");
        }

        [Fact]
        public async Task GetCompatibilityInfoAsync_WhenWebCryptoMissing_ReturnsIncompatibleInfo()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<BrowserCompatibilityInfo>("crypto.getBrowserCompatibilityInfo", It.IsAny<object[]>()))
                .ReturnsAsync(new BrowserCompatibilityInfo
                {
                    IsWebCryptoAvailable = false,
                    IsSecureContext = true,
                    BrowserName = "OldBrowser",
                    BrowserVersion = "1.0.0",
                    ErrorMessage = "Web Crypto API is not supported by this browser"
                });

            // Act
            var result = await _sut.GetCompatibilityInfoAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsCompatible.Should().BeFalse();
            result.IsWebCryptoAvailable.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Web Crypto API");
        }

        [Fact]
        public void Constructor_WithNullJsRuntime_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BrowserCompatibilityService(null!));
        }

        [Fact]
        public async Task IsWebCryptoAvailableAsync_WhenJsInteropThrows_ReturnsFalse()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("crypto.checkWebCryptoAvailability", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("JS Error"));

            // Act
            var result = await _sut.IsWebCryptoAvailableAsync();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsSecureContextAsync_WhenJsInteropThrows_ReturnsFalse()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("crypto.checkSecureContext", It.IsAny<object[]>()))
                .ThrowsAsync(new InvalidOperationException("Context error"));

            // Act
            var result = await _sut.IsSecureContextAsync();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetCompatibilityInfoAsync_WhenJsInteropThrows_ReturnsDefaultIncompatibleInfo()
        {
            // Arrange
            var expectedException = new JSException("Browser API not available");
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<BrowserCompatibilityInfo>("crypto.getBrowserCompatibilityInfo", It.IsAny<object[]>()))
                .ThrowsAsync(expectedException);

            // Act
            var result = await _sut.GetCompatibilityInfoAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsWebCryptoAvailable.Should().BeFalse();
            result.IsSecureContext.Should().BeFalse();
            result.IsCompatible.Should().BeFalse();
            result.BrowserName.Should().Be("Unknown");
            result.BrowserVersion.Should().Be("Unknown");
            result.ErrorMessage.Should().Contain("Failed to check browser compatibility");
            result.ErrorMessage.Should().Contain(expectedException.Message);
        }

        [Theory]
        [InlineData("Chrome", "120.0.0", true, true, true)]
        [InlineData("Firefox", "115.0", true, true, true)]
        [InlineData("Safari", "16.0", true, false, false)]
        [InlineData("Edge", "118.0", false, true, false)]
        [InlineData("OldBrowser", "1.0", false, false, false)]
        public async Task GetCompatibilityInfoAsync_WithVariousBrowserScenarios_ReturnsExpectedCompatibility(
            string browserName,
            string browserVersion,
            bool webCryptoAvailable,
            bool secureContext,
            bool expectedCompatible)
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<BrowserCompatibilityInfo>("crypto.getBrowserCompatibilityInfo", It.IsAny<object[]>()))
                .ReturnsAsync(new BrowserCompatibilityInfo
                {
                    IsWebCryptoAvailable = webCryptoAvailable,
                    IsSecureContext = secureContext,
                    BrowserName = browserName,
                    BrowserVersion = browserVersion,
                    ErrorMessage = !expectedCompatible ? "Compatibility check failed" : string.Empty
                });

            // Act
            var result = await _sut.GetCompatibilityInfoAsync();

            // Assert
            result.Should().NotBeNull();
            result.BrowserName.Should().Be(browserName);
            result.BrowserVersion.Should().Be(browserVersion);
            result.IsWebCryptoAvailable.Should().Be(webCryptoAvailable);
            result.IsSecureContext.Should().Be(secureContext);
            result.IsCompatible.Should().Be(expectedCompatible);
        }


        [Fact]
        public async Task IsWebCryptoAvailableAsync_WhenCalledMultipleTimes_WorksConsistently()
        {
            // Arrange
            var callCount = 0;
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<bool>("crypto.checkWebCryptoAvailability", It.IsAny<object[]>()))
                .ReturnsAsync(() => ++callCount % 2 == 1);

            // Act
            var result1 = await _sut.IsWebCryptoAvailableAsync();
            var result2 = await _sut.IsWebCryptoAvailableAsync();
            var result3 = await _sut.IsWebCryptoAvailableAsync();

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeFalse();
            result3.Should().BeTrue();
            _jsRuntimeMock.Verify(
                x => x.InvokeAsync<bool>("crypto.checkWebCryptoAvailability", It.IsAny<object[]>()), 
                Times.Exactly(3));
        }

        [Fact]
        public async Task GetCompatibilityInfoAsync_WithTimeoutException_ReturnsDefaultIncompatibleInfo()
        {
            // Arrange
            _jsRuntimeMock
                .Setup(x => x.InvokeAsync<BrowserCompatibilityInfo>("crypto.getBrowserCompatibilityInfo", It.IsAny<object[]>()))
                .ThrowsAsync(new TaskCanceledException("Operation timed out"));

            // Act
            var result = await _sut.GetCompatibilityInfoAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsCompatible.Should().BeFalse();
            result.BrowserName.Should().Be("Unknown");
            result.ErrorMessage.Should().Contain("Failed to check browser compatibility");
        }
    }
}