using Microsoft.AspNetCore.Components;

namespace NoLock.Social.Core.Accessibility.Interfaces;

/// <summary>
/// Service for managing focus states and keyboard navigation in camera workflows
/// </summary>
public interface IFocusManagementService
{
    /// <summary>
    /// Store the currently focused element reference
    /// </summary>
    Task StoreFocusAsync(ElementReference element);

    /// <summary>
    /// Restore focus to the previously stored element
    /// </summary>
    Task RestoreFocusAsync();

    /// <summary>
    /// Set focus to a specific element
    /// </summary>
    Task SetFocusAsync(ElementReference element);

    /// <summary>
    /// Trap focus within a container element
    /// </summary>
    Task TrapFocusAsync(ElementReference container);

    /// <summary>
    /// Release focus trap
    /// </summary>
    Task ReleaseFocusTrapAsync();

    /// <summary>
    /// Move focus to the first focusable element in a container
    /// </summary>
    Task FocusFirstElementAsync(ElementReference container);

    /// <summary>
    /// Move focus to the last focusable element in a container
    /// </summary>
    Task FocusLastElementAsync(ElementReference container);

    /// <summary>
    /// Check if an element is currently focused
    /// </summary>
    Task<bool> IsFocusedAsync(ElementReference element);

    /// <summary>
    /// Get the currently focused element
    /// </summary>
    Task<ElementReference?> GetActiveElementAsync();
}