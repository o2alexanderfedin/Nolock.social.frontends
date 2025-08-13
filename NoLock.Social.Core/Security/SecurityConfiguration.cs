namespace NoLock.Social.Core.Security;

/// <summary>
/// Provides security configuration settings for the application
/// </summary>
public class SecurityConfiguration
{
    /// <summary>
    /// Content Security Policy configuration
    /// </summary>
    public ContentSecurityPolicyConfig ContentSecurityPolicy { get; set; } = new();
    
    /// <summary>
    /// Cookie security settings
    /// </summary>
    public CookieSecurityConfig CookieSecurity { get; set; } = new();
    
    /// <summary>
    /// HSTS configuration for production
    /// </summary>
    public HstsConfig Hsts { get; set; } = new();
    
    /// <summary>
    /// Subresource Integrity configuration
    /// </summary>
    public SubresourceIntegrityConfig SubresourceIntegrity { get; set; } = new();
}

/// <summary>
/// Content Security Policy configuration
/// </summary>
public class ContentSecurityPolicyConfig
{
    public string DefaultSrc { get; set; } = "'self'";
    public string ScriptSrc { get; set; } = "'self' 'unsafe-eval' 'unsafe-inline' 'wasm-unsafe-eval'";
    public string StyleSrc { get; set; } = "'self' 'unsafe-inline'";
    public string ImgSrc { get; set; } = "'self' data: https:";
    public string ConnectSrc { get; set; } = "'self' wss: https:";
    public string FontSrc { get; set; } = "'self' data:";
    public string ObjectSrc { get; set; } = "'none'";
    public string MediaSrc { get; set; } = "'self'";
    public string FrameSrc { get; set; } = "'none'";
    public string BaseUri { get; set; } = "'self'";
    public string FormAction { get; set; } = "'self'";
    public string FrameAncestors { get; set; } = "'none'";
    public bool UpgradeInsecureRequests { get; set; } = true;
    
    public string GeneratePolicyString()
    {
        var policy = new List<string>
        {
            $"default-src {DefaultSrc}",
            $"script-src {ScriptSrc}",
            $"style-src {StyleSrc}",
            $"img-src {ImgSrc}",
            $"connect-src {ConnectSrc}",
            $"font-src {FontSrc}",
            $"object-src {ObjectSrc}",
            $"media-src {MediaSrc}",
            $"frame-src {FrameSrc}",
            $"base-uri {BaseUri}",
            $"form-action {FormAction}",
            $"frame-ancestors {FrameAncestors}"
        };
        
        if (UpgradeInsecureRequests)
        {
            policy.Add("upgrade-insecure-requests");
        }
        
        return string.Join("; ", policy);
    }
}

/// <summary>
/// Cookie security configuration
/// </summary>
public class CookieSecurityConfig
{
    public bool HttpOnly { get; set; } = true;
    public bool Secure { get; set; } = true;
    public string SameSite { get; set; } = "Strict";
    public int? MaxAge { get; set; } = 86400; // 24 hours
}

/// <summary>
/// HTTP Strict Transport Security configuration
/// </summary>
public class HstsConfig
{
    public bool Enabled { get; set; } = true;
    public int MaxAge { get; set; } = 31536000; // 1 year
    public bool IncludeSubDomains { get; set; } = true;
    public bool Preload { get; set; } = false;
    
    public string GenerateHeaderValue()
    {
        if (!Enabled) return string.Empty;
        
        var parts = new List<string> { $"max-age={MaxAge}" };
        
        if (IncludeSubDomains)
        {
            parts.Add("includeSubDomains");
        }
        
        if (Preload)
        {
            parts.Add("preload");
        }
        
        return string.Join("; ", parts);
    }
}

/// <summary>
/// Subresource Integrity configuration
/// </summary>
public class SubresourceIntegrityConfig
{
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string> ResourceHashes { get; set; } = new();
    
    public void AddResourceHash(string resourcePath, string hash)
    {
        ResourceHashes[resourcePath] = hash;
    }
    
    public string? GetIntegrityHash(string resourcePath)
    {
        return ResourceHashes.TryGetValue(resourcePath, out var hash) ? hash : null;
    }
}