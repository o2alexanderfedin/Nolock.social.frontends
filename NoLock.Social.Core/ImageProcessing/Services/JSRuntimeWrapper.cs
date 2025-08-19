using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using NoLock.Social.Core.ImageProcessing.Interfaces;

namespace NoLock.Social.Core.ImageProcessing.Services
{
    /// <summary>
    /// Concrete implementation of IJSRuntimeWrapper that delegates to IJSRuntime
    /// </summary>
    public class JSRuntimeWrapper : IJSRuntimeWrapper
    {
        private readonly IJSRuntime _jsRuntime;

        public JSRuntimeWrapper(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }

        public async Task<T> InvokeAsync<T>(string identifier, params object[] args)
        {
            return await _jsRuntime.InvokeAsync<T>(identifier, args);
        }

        public async Task InvokeVoidAsync(string identifier, params object[] args)
        {
            await _jsRuntime.InvokeVoidAsync(identifier, args);
        }
    }
}