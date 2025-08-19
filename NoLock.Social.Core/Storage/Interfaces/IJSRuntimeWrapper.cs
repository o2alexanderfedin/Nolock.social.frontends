using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace NoLock.Social.Core.Storage.Interfaces
{
    /// <summary>
    /// Wrapper interface for IJSRuntime to enable testability
    /// </summary>
    public interface IJSRuntimeWrapper
    {
        ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object?[]? args);
        ValueTask InvokeVoidAsync(string identifier, params object?[]? args);
    }

    /// <summary>
    /// Default implementation that delegates to IJSRuntime
    /// </summary>
    public class JSRuntimeWrapper : IJSRuntimeWrapper
    {
        private readonly IJSRuntime _jsRuntime;

        public JSRuntimeWrapper(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new System.ArgumentNullException(nameof(jsRuntime));
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object?[]? args)
        {
            return _jsRuntime.InvokeAsync<TValue>(identifier, args);
        }

        public ValueTask InvokeVoidAsync(string identifier, params object?[]? args)
        {
            return _jsRuntime.InvokeVoidAsync(identifier, args);
        }
    }
}