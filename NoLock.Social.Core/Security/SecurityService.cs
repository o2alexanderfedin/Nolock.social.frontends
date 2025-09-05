using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NoLock.Social.Core.Common.Extensions;

namespace NoLock.Social.Core.Security;

/// <summary>
/// Service for managing application security settings and headers
/// </summary>
public interface ISecurityService
{
    /// <summary>
    /// Get the current security configuration
    /// </summary>
    SecurityConfiguration Configuration { get; }
    
    /// <summary>
    /// Apply security headers via JavaScript interop
    /// </summary>
    Task ApplySecurityHeadersAsync();
    
    /// <summary>
    /// Configure secure cookie settings
    /// </summary>
    Task ConfigureSecureCookiesAsync();
    
    /// <summary>
    /// Validate Content Security Policy
    /// </summary>
    Task<bool> ValidateCspAsync();
}

/// <summary>
/// Implementation of security service
/// </summary>
public class SecurityService : ISecurityService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<SecurityService> _logger;
    private readonly SecurityConfiguration _configuration;
    
    public SecurityService(IJSRuntime jsRuntime, ILogger<SecurityService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
        _configuration = new SecurityConfiguration();
    }
    
    public SecurityConfiguration Configuration => _configuration;
    
    public async Task ApplySecurityHeadersAsync()
    {
        var result = await _logger.ExecuteWithLogging(async () =>
        {
            _logger.LogInformation("Applying security headers");
            
            // Apply CSP via JavaScript (for dynamic updates)
            var cspPolicy = _configuration.ContentSecurityPolicy.GeneratePolicyString();
            await _jsRuntime.InvokeVoidAsync("eval", $@"
                (function() {{
                    // Create meta tag for CSP if it doesn't exist
                    let cspMeta = document.querySelector('meta[http-equiv=""Content-Security-Policy""]');
                    if (!cspMeta) {{
                        cspMeta = document.createElement('meta');
                        cspMeta.httpEquiv = 'Content-Security-Policy';
                        document.head.appendChild(cspMeta);
                    }}
                    cspMeta.content = '{cspPolicy}';
                    
                    // Log CSP application
                    console.log('CSP applied:', cspMeta.content);
                }})();
            ");
            
            _logger.LogInformation("Security headers applied successfully");
        },
        "ApplySecurityHeadersAsync");
        
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("Failed to apply security headers", result.Exception);
        }
    }
    
    public async Task ConfigureSecureCookiesAsync()
    {
        var result = await _logger.ExecuteWithLogging(async () =>
        {
            _logger.LogInformation("Configuring secure cookie settings");
            
            var cookieConfig = _configuration.CookieSecurity;
            await _jsRuntime.InvokeVoidAsync("eval", $@"
                (function() {{
                    // Override document.cookie setter to enforce security
                    const originalCookieSetter = document.__lookupSetter__('cookie');
                    if (originalCookieSetter) {{
                        document.__defineSetter__('cookie', function(value) {{
                            // Ensure secure flags are set
                            let cookieString = value;
                            
                            // Add secure flag if not present and on HTTPS
                            if (window.location.protocol === 'https:' && !cookieString.includes('Secure')) {{
                                cookieString += '; Secure';
                            }}
                            
                            // Add HttpOnly flag if not present (note: may not work from JS)
                            if (!cookieString.includes('HttpOnly')) {{
                                cookieString += '; HttpOnly';
                            }}
                            
                            // Add SameSite flag if not present
                            if (!cookieString.includes('SameSite')) {{
                                cookieString += '; SameSite={cookieConfig.SameSite}';
                            }}
                            
                            // Add Max-Age if configured
                            if ({(cookieConfig.MaxAge.HasValue ? "true" : "false")} && !cookieString.includes('Max-Age')) {{
                                cookieString += '; Max-Age={cookieConfig.MaxAge ?? 86400}';
                            }}
                            
                            originalCookieSetter.call(this, cookieString);
                        }});
                    }}
                    
                    console.log('Secure cookie settings configured');
                }})();
            ");
            
            _logger.LogInformation("Secure cookie settings configured successfully");
        },
        "ConfigureSecureCookiesAsync");
        
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("Failed to configure secure cookies", result.Exception);
        }
    }
    
    public async Task<bool> ValidateCspAsync()
    {
        var result = await _logger.ExecuteWithLogging(async () =>
        {
            _logger.LogInformation("Validating Content Security Policy");
            
            var validationResult = await _jsRuntime.InvokeAsync<bool>("eval", @"
                (function() {
                    // Check if CSP is active
                    const cspMeta = document.querySelector('meta[http-equiv=""Content-Security-Policy""]');
                    if (!cspMeta || !cspMeta.content) {
                        console.warn('CSP meta tag not found');
                        return false;
                    }
                    
                    // Validate CSP directives
                    const content = cspMeta.content;
                    const requiredDirectives = [
                        'default-src',
                        'script-src',
                        'style-src',
                        'frame-ancestors'
                    ];
                    
                    for (const directive of requiredDirectives) {
                        if (!content.includes(directive)) {
                            console.warn(`CSP missing required directive: ${directive}`);
                            return false;
                        }
                    }
                    
                    console.log('CSP validation successful');
                    return true;
                })();
            ");
            
            _logger.LogInformation("CSP validation result: {Result}", validationResult);
            return validationResult;
        },
        "ValidateCspAsync");
        
        return result.IsSuccess ? result.Value : false;
    }
}