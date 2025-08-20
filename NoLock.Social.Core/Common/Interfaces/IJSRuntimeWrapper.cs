using System.Threading.Tasks;

namespace NoLock.Social.Core.Common.Interfaces
{
    /// <summary>
    /// Wrapper interface for IJSRuntime to enable mocking of extension methods
    /// </summary>
    public interface IJSRuntimeWrapper
    {
        Task<T> InvokeAsync<T>(string identifier, params object[] args);
        Task InvokeVoidAsync(string identifier, params object[] args);
    }
}