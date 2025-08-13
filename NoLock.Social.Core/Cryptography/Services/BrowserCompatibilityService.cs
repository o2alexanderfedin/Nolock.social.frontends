using Microsoft.JSInterop;
using NoLock.Social.Core.Cryptography.Interfaces;
using System;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Service for checking browser compatibility with required cryptographic APIs.
    /// </summary>
    public class BrowserCompatibilityService : IBrowserCompatibilityService
    {
        private readonly IJSRuntime _jsRuntime;

        public BrowserCompatibilityService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }

        public async Task<bool> IsWebCryptoAvailableAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("crypto.checkWebCryptoAvailability");
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsSecureContextAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("crypto.checkSecureContext");
            }
            catch
            {
                return false;
            }
        }

        public async Task<BrowserCompatibilityInfo> GetCompatibilityInfoAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<BrowserCompatibilityInfo>("crypto.getBrowserCompatibilityInfo");
            }
            catch (Exception ex)
            {
                // If we can't call the JS function, return basic incompatibility info
                return new BrowserCompatibilityInfo
                {
                    IsWebCryptoAvailable = false,
                    IsSecureContext = false,
                    BrowserName = "Unknown",
                    BrowserVersion = "Unknown",
                    ErrorMessage = $"Failed to check browser compatibility: {ex.Message}"
                };
            }
        }
    }
}