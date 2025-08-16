using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NoLock.Social.Core.Accessibility.Interfaces;
using Microsoft.Extensions.Logging;

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
    private bool _focusTrapped = false;

    public FocusManagementService(IJSRuntime jsRuntime, ILogger<FocusManagementService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
        _moduleTask = new Lazy<Task<IJSObjectReference>>(() => 
            _jsRuntime.InvokeAsync<IJSObjectReference>("import", "/js/focus-management.js").AsTask());
    }

    public async Task StoreFocusAsync(ElementReference element)
    {
        try
        {
            _storedFocusElement = element;
            _logger.LogDebug("Focus element stored for restoration");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing focus element");
        }
    }

    public async Task RestoreFocusAsync()
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring focus");
        }
    }

    public async Task SetFocusAsync(ElementReference element)
    {
        try
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("setFocus", element);
            _logger.LogDebug("Focus set to specified element");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting focus to element");
        }
    }

    public async Task TrapFocusAsync(ElementReference container)
    {
        try
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("trapFocus", container);
            _focusTrapped = true;
            _logger.LogDebug("Focus trapped within container");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error trapping focus");
        }
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
        try
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("focusFirstElement", container);
            _logger.LogDebug("Focus moved to first element in container");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error focusing first element");
        }
    }

    public async Task FocusLastElementAsync(ElementReference container)
    {
        try
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("focusLastElement", container);
            _logger.LogDebug("Focus moved to last element in container");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error focusing last element");
        }
    }

    public async Task<bool> IsFocusedAsync(ElementReference element)
    {
        try
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<bool>("isFocused", element);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking focus state");
            return false;
        }
    }

    public async Task<ElementReference?> GetActiveElementAsync()
    {
        try
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<ElementReference?>("getActiveElement");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active element");
            return null;
        }
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