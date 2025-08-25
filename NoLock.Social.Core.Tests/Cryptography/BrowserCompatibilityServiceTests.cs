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
    }
}