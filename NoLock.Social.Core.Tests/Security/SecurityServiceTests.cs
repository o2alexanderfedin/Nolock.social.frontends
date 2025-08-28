using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using NoLock.Social.Core.Security;
using Xunit;

namespace NoLock.Social.Core.Tests.Security;

public class SecurityServiceTests
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly Mock<ILogger<SecurityService>> _loggerMock;
    private readonly SecurityService _service;

    public SecurityServiceTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _loggerMock = new Mock<ILogger<SecurityService>>();
        
        // Set up logger to avoid null reference exceptions
        _loggerMock
            .Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception, Delegate>((level, eventId, state, exception, formatter) => { });
            
        _service = new SecurityService(_jsRuntimeMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Configuration_ReturnsValidSecurityConfiguration()
    {
        // Act
        var config = _service.Configuration;

        // Assert
        Assert.NotNull(config);
        Assert.NotNull(config.ContentSecurityPolicy);
        Assert.NotNull(config.CookieSecurity);
        Assert.NotNull(config.Hsts);
        Assert.NotNull(config.SubresourceIntegrity);
    }

    [Fact]
    public async Task ApplySecurityHeadersAsync_AppliesCspViaJavaScript()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSVoidResult>(
                It.IsAny<string>(), 
                It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());

        // Act
        await _service.ApplySecurityHeadersAsync();

        // Assert
        _jsRuntimeMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "eval",
            It.Is<object[]>(args => args.Length == 1 && args[0].ToString()!.Contains("Content-Security-Policy"))), 
            Times.Once);
    }

    [Fact]
    public async Task ApplySecurityHeadersAsync_LogsInformationMessages()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSVoidResult>(
                It.IsAny<string>(), 
                It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());

        // Act
        await _service.ApplySecurityHeadersAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Applying security headers")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Security headers applied successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplySecurityHeadersAsync_ThrowsOnJavaScriptError()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSVoidResult>(
                It.IsAny<string>(), 
                It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("JavaScript error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.ApplySecurityHeadersAsync());
    }

    [Fact]
    public async Task ConfigureSecureCookiesAsync_ConfiguresCookieSettingsViaJavaScript()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSVoidResult>(
                It.IsAny<string>(), 
                It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());

        // Act
        await _service.ConfigureSecureCookiesAsync();

        // Assert
        _jsRuntimeMock.Verify(js => js.InvokeAsync<IJSVoidResult>(
            "eval",
            It.Is<object[]>(args => args.Length == 1 && args[0].ToString()!.Contains("document.__lookupSetter__"))),
            Times.Once);
    }

    [Fact]
    public async Task ConfigureSecureCookiesAsync_AppliesCorrectSecurityFlags()
    {
        // Arrange
        string? capturedScript = null;
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSVoidResult>(
                It.IsAny<string>(), 
                It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) => capturedScript = args[0].ToString())
            .ReturnsAsync(Mock.Of<IJSVoidResult>());

        // Act
        await _service.ConfigureSecureCookiesAsync();

        // Assert
        Assert.NotNull(capturedScript);
        Assert.Contains("Secure", capturedScript);
        Assert.Contains("HttpOnly", capturedScript);
        Assert.Contains("SameSite=Strict", capturedScript);
        Assert.Contains("Max-Age=86400", capturedScript);
    }

    [Fact]
    public async Task ConfigureSecureCookiesAsync_LogsInformationMessages()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSVoidResult>(
                It.IsAny<string>(), 
                It.IsAny<object[]>()))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());

        // Act
        await _service.ConfigureSecureCookiesAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Configuring secure cookie settings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Secure cookie settings configured successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfigureSecureCookiesAsync_ThrowsOnJavaScriptError()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<IJSVoidResult>(
                It.IsAny<string>(), 
                It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("JavaScript error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.ConfigureSecureCookiesAsync());
    }

    [Fact]
    public async Task ValidateCspAsync_ReturnsTrueWhenCspIsValid()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<bool>(
                "eval",
                It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ValidateCspAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateCspAsync_ReturnsFalseWhenCspIsInvalid()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<bool>(
                "eval",
                It.IsAny<object[]>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ValidateCspAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateCspAsync_ChecksForRequiredDirectives()
    {
        // Arrange
        string? capturedScript = null;
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<bool>(
                "eval",
                It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) => capturedScript = args[0].ToString())
            .ReturnsAsync(true);

        // Act
        await _service.ValidateCspAsync();

        // Assert
        Assert.NotNull(capturedScript);
        Assert.Contains("default-src", capturedScript);
        Assert.Contains("script-src", capturedScript);
        Assert.Contains("style-src", capturedScript);
        Assert.Contains("frame-ancestors", capturedScript);
    }

    [Fact]
    public async Task ValidateCspAsync_LogsValidationResult()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<bool>(
                "eval",
                It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // Act
        await _service.ValidateCspAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CSP validation result")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateCspAsync_ReturnsFalseOnException()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<bool>(
                "eval",
                It.IsAny<object[]>()))
            .Throws(new JSException("JavaScript error"));

        // Act
        var result = await _service.ValidateCspAsync();

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(true, "upgrade-insecure-requests")]
    [InlineData(false, null)]
    public void ContentSecurityPolicy_GeneratesPolicyString_WithUpgradeInsecureRequests(bool upgradeInsecure, string? expectedDirective)
    {
        // Arrange
        _service.Configuration.ContentSecurityPolicy.UpgradeInsecureRequests = upgradeInsecure;

        // Act
        var policyString = _service.Configuration.ContentSecurityPolicy.GeneratePolicyString();

        // Assert
        Assert.Contains("default-src 'self'", policyString);
        Assert.Contains("script-src 'self' 'unsafe-eval' 'unsafe-inline' 'wasm-unsafe-eval'", policyString);
        Assert.Contains("style-src 'self' 'unsafe-inline'", policyString);
        Assert.Contains("frame-ancestors 'none'", policyString);
        
        if (expectedDirective != null)
        {
            Assert.Contains(expectedDirective, policyString);
        }
        else
        {
            Assert.DoesNotContain("upgrade-insecure-requests", policyString);
        }
    }

    [Fact]
    public void CookieSecurity_HasCorrectDefaultSettings()
    {
        // Act
        var cookieConfig = _service.Configuration.CookieSecurity;

        // Assert
        Assert.True(cookieConfig.HttpOnly);
        Assert.True(cookieConfig.Secure);
        Assert.Equal("Strict", cookieConfig.SameSite);
        Assert.Equal(86400, cookieConfig.MaxAge);
    }

    [Theory]
    [InlineData(true, true, false, "max-age=31536000; includeSubDomains")]
    [InlineData(true, false, true, "max-age=31536000; preload")]
    [InlineData(true, true, true, "max-age=31536000; includeSubDomains; preload")]
    [InlineData(false, true, true, "")]
    public void HstsConfig_GeneratesCorrectHeaderValue(bool enabled, bool includeSubDomains, bool preload, string expectedHeader)
    {
        // Arrange
        var hstsConfig = _service.Configuration.Hsts;
        hstsConfig.Enabled = enabled;
        hstsConfig.IncludeSubDomains = includeSubDomains;
        hstsConfig.Preload = preload;

        // Act
        var headerValue = hstsConfig.GenerateHeaderValue();

        // Assert
        Assert.Equal(expectedHeader, headerValue);
    }

    [Fact]
    public void SubresourceIntegrity_ManagesResourceHashes()
    {
        // Arrange
        var sriConfig = _service.Configuration.SubresourceIntegrity;
        const string resourcePath = "/js/app.js";
        const string hash = "sha384-oqVuAfXRKap7fdgcCY5uykM6+R9GqQ8K/uxy9rx7HNQlGYl1kPzQho1wx4JwY8wC";

        // Act
        sriConfig.AddResourceHash(resourcePath, hash);
        var retrievedHash = sriConfig.GetIntegrityHash(resourcePath);
        var missingHash = sriConfig.GetIntegrityHash("/js/missing.js");

        // Assert
        Assert.Equal(hash, retrievedHash);
        Assert.Null(missingHash);
    }
}