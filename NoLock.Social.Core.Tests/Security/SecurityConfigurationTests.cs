using NoLock.Social.Core.Security;

namespace NoLock.Social.Core.Tests.Security;

public class SecurityConfigurationTests
{
    [Fact]
    public void ContentSecurityPolicy_GeneratePolicyString_ReturnsCorrectFormat()
    {
        // Arrange
        var csp = new ContentSecurityPolicyConfig
        {
            DefaultSrc = "'self'",
            ScriptSrc = "'self' 'unsafe-inline'",
            StyleSrc = "'self'",
            UpgradeInsecureRequests = true
        };
        
        // Act
        var policy = csp.GeneratePolicyString();
        
        // Assert
        Assert.Contains("default-src 'self'", policy);
        Assert.Contains("script-src 'self' 'unsafe-inline'", policy);
        Assert.Contains("style-src 'self'", policy);
        Assert.Contains("upgrade-insecure-requests", policy);
    }
    
    [Fact]
    public void ContentSecurityPolicy_WithoutUpgradeInsecureRequests_DoesNotIncludeDirective()
    {
        // Arrange
        var csp = new ContentSecurityPolicyConfig
        {
            UpgradeInsecureRequests = false
        };
        
        // Act
        var policy = csp.GeneratePolicyString();
        
        // Assert
        Assert.DoesNotContain("upgrade-insecure-requests;", policy);
    }
    
    [Fact]
    public void HstsConfig_GenerateHeaderValue_WithAllOptions_ReturnsCorrectFormat()
    {
        // Arrange
        var hsts = new HstsConfig
        {
            Enabled = true,
            MaxAge = 31536000,
            IncludeSubDomains = true,
            Preload = true
        };
        
        // Act
        var header = hsts.GenerateHeaderValue();
        
        // Assert
        Assert.Equal("max-age=31536000; includeSubDomains; preload", header);
    }
    
    [Fact]
    public void HstsConfig_WhenDisabled_ReturnsEmptyString()
    {
        // Arrange
        var hsts = new HstsConfig
        {
            Enabled = false
        };
        
        // Act
        var header = hsts.GenerateHeaderValue();
        
        // Assert
        Assert.Empty(header);
    }
    
    [Fact]
    public void HstsConfig_WithOnlyMaxAge_ReturnsSimpleFormat()
    {
        // Arrange
        var hsts = new HstsConfig
        {
            Enabled = true,
            MaxAge = 86400,
            IncludeSubDomains = false,
            Preload = false
        };
        
        // Act
        var header = hsts.GenerateHeaderValue();
        
        // Assert
        Assert.Equal("max-age=86400", header);
    }
    
    [Fact]
    public void SubresourceIntegrityConfig_AddAndGetResourceHash_WorksCorrectly()
    {
        // Arrange
        var sri = new SubresourceIntegrityConfig();
        var resourcePath = "/js/app.js";
        var hash = "sha384-oqVuAfXRKap7fdgcCY5uykM6+R9GqQ8K/uxy9rx7HNQlGYl1kPzQho1wx4JwY8wC";
        
        // Act
        sri.AddResourceHash(resourcePath, hash);
        var retrievedHash = sri.GetIntegrityHash(resourcePath);
        
        // Assert
        Assert.Equal(hash, retrievedHash);
    }
    
    [Fact]
    public void SubresourceIntegrityConfig_GetIntegrityHash_ForNonExistentResource_ReturnsNull()
    {
        // Arrange
        var sri = new SubresourceIntegrityConfig();
        
        // Act
        var hash = sri.GetIntegrityHash("/nonexistent.js");
        
        // Assert
        Assert.Null(hash);
    }
    
    [Fact]
    public void CookieSecurityConfig_DefaultValues_AreSecure()
    {
        // Arrange & Act
        var config = new CookieSecurityConfig();
        
        // Assert
        Assert.True(config.HttpOnly);
        Assert.True(config.Secure);
        Assert.Equal("Strict", config.SameSite);
        Assert.Equal(86400, config.MaxAge); // 24 hours
    }
    
    [Fact]
    public void SecurityConfiguration_DefaultConfiguration_HasSecureDefaults()
    {
        // Arrange & Act
        var config = new SecurityConfiguration();
        
        // Assert
        Assert.NotNull(config.ContentSecurityPolicy);
        Assert.NotNull(config.CookieSecurity);
        Assert.NotNull(config.Hsts);
        Assert.NotNull(config.SubresourceIntegrity);
        
        // Verify secure defaults
        Assert.True(config.CookieSecurity.HttpOnly);
        Assert.True(config.CookieSecurity.Secure);
        Assert.True(config.Hsts.Enabled);
        Assert.Equal("'none'", config.ContentSecurityPolicy.ObjectSrc);
        Assert.Equal("'none'", config.ContentSecurityPolicy.FrameSrc);
        Assert.Equal("'none'", config.ContentSecurityPolicy.FrameAncestors);
    }
}