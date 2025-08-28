using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NoLock.Social.Core.Accessibility.Interfaces;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Common.Extensions;

namespace NoLock.Social.Core.Accessibility.Services;

/// <summary>
/// Service for managing focus states and keyboard navigation in camera workflows
/// </summary>
public class FocusManagementService : IFocusManagementService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<FocusManagementService> _logger;
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;
    private ElementReference? _storedFocusElement;
    private bool _focusTrapped;

    public FocusManagementService(IJSRuntime jsRuntime, ILogger<FocusManagementService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
        _moduleTask = new Lazy<Task<IJSObjectReference>>(() => 
            _jsRuntime.InvokeAsync<IJSObjectReference>("import", "/js/focus-management.js").AsTask());
    }

    public async Task StoreFocusAsync(ElementReference element)
    {
        var result = _logger.ExecuteWithLogging(
            () =>
            {
                _storedFocusElement = element;
                _logger.LogDebug("Focus element stored for restoration");
            },
            "StoreFocus");
        
        // Await to maintain async behavior for interface compatibility
        await Task.CompletedTask;
    }

    public async Task RestoreFocusAsync()
    {
        await _logger.ExecuteWithLogging(async () =>
        {
            if (_storedFocusElement.HasValue)
            {
                await SetFocusAsync(_storedFocusElement.Value);
                _logger.LogDebug("Focus restored to stored element");
            }
            else
            {
                _logger.LogDebug("No stored focus element to restore");
            }
        }, "RestoreFocus");
    }

    public async Task SetFocusAsync(ElementReference element)
    {
        var result = await _logger.ExecuteWithLogging(async () =>
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("setFocus", element);
            _logger.LogDebug("Focus set to specified element");
        },
        "SetFocusAsync");
    }

    public async Task TrapFocusAsync(ElementReference container)
    {
        await _logger.ExecuteWithLogging(async () =>
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("trapFocus", container);
            _focusTrapped = true;
            _logger.LogDebug("Focus trapped within container");
        }, "TrapFocusAsync");
    }

    public async Task ReleaseFocusTrapAsync()
    {
        try
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("releaseFocusTrap");
            _focusTrapped = false;
            _logger.LogDebug("Focus trap released");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing focus trap");
        }
    }

    public async Task FocusFirstElementAsync(ElementReference container)
    {
        await _logger.ExecuteWithLogging(async () =>
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("focusFirstElement", container);
            _logger.LogDebug("Focus moved to first element in container");
        }, "FocusFirstElementAsync");
    }

    public async Task FocusLastElementAsync(ElementReference container)
    {
        await _logger.ExecuteWithLogging(async () =>
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("focusLastElement", container);
            _logger.LogDebug("Focus moved to last element in container");
        }, "FocusLastElementAsync");
    }

    public async Task<bool> IsFocusedAsync(ElementReference element)
    {
        var result = await _logger.ExecuteWithLogging(async () =>
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<bool>("isFocused", element);
        }, "IsFocusedAsync");
        
        return result.IsSuccess ? result.Value : false;
    }

    public async Task<ElementReference?> GetActiveElementAsync()
    {
        var result = await _logger.ExecuteWithLogging(async () =>
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<ElementReference?>("getActiveElement");
        }, "GetActiveElementAsync");
        
        return result.IsSuccess ? result.Value : null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_focusTrapped)
            {
                await ReleaseFocusTrapAsync();
            }

            if (_moduleTask.IsValueCreated)
            {
                var module = await _moduleTask.Value;
                await module.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing FocusManagementService");
        }
    }
}