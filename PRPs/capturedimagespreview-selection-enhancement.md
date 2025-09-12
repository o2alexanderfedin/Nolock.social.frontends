# CapturedImagesPreview Selection Enhancement PRP

**Feature**: Minimal Data Binding Enhancement for CapturedImagesPreview Component  
**Status**: Ready for Implementation  
**TRIZ Innovation Score**: 45/50 (Exceptional)  
**Estimated Lines**: 50-55 total changes  
**Breaking Changes**: Zero guaranteed  

---

## Goal

Enhance the CapturedImagesPreview component with optional data binding capabilities that enable parent components to track selected images while maintaining perfect backward compatibility and zero breaking changes.

## Why

- **User Experience Enhancement**: Users need visual feedback to identify selected/unselected image states
- **Data Binding Capability**: Parent components need to bind to selected CapturedImage objects for processing workflows  
- **Architectural Consistency**: Maintains existing component patterns while adding new capabilities
- **Mobile Performance**: Designed for limited hardware with CSS-only visual feedback
- **Zero Migration Cost**: Existing implementations continue working unchanged

## What

Implement a unified selection system that intelligently switches between:
- **Selection Mode**: When parent provides selection callbacks, enable visual selection with data binding
- **Fullscreen Mode**: When no selection callbacks provided, maintain existing fullscreen behavior (default)

### Success Criteria

- [ ] All existing CapturedImagesPreview usage works unchanged
- [ ] New selection parameters enable visual selection feedback
- [ ] Parent components can bind to selected image collections
- [ ] Click behavior automatically switches based on parameter presence
- [ ] Visual indicators only appear when selection is active
- [ ] Works perfectly on iOS Safari and Android Chrome
- [ ] No measurable performance impact on mobile devices
- [ ] All tests pass (existing + new)

## All Needed Context

### Documentation & References

```yaml
# MUST READ - Include these in your context window

- file: /Users/alexanderfedin/Projects/nolock.social/Nolock.social.frontend/NoLock.Social.Components/Camera/CapturedImagesPreview.razor
  why: Current implementation to modify, existing patterns to follow
  critical: Lines 22-24 (click handler), Lines 106-109 (current parameters), Lines 113-127 (event handling patterns)

- file: /Users/alexanderfedin/Projects/nolock.social/Nolock.social.frontend/docs/proposals/capturedimagespreview-selection-refactoring/enhanced-minimal-data-binding-design.md  
  why: Complete technical specification with TRIZ analysis and implementation details
  critical: Step-by-step implementation guide, CSS patterns, usage examples

- file: /Users/alexanderfedin/Projects/nolock.social/Nolock.social.frontend/NoLock.Social.Components/Camera/FullscreenImageViewer.razor
  why: EventCallback patterns to mirror - lines 36 (parameter declaration), 86 (invocation pattern)
  critical: "await OnClosed.InvokeAsync()" pattern

- file: /Users/alexanderfedin/Projects/nolock.social/Nolock.social.frontend/NoLock.Social.Components/Camera/CameraCapture.razor  
  why: EventCallback<T> patterns - lines 144-145 (strongly typed callbacks), 345-348 (HasDelegate checking)
  critical: "if (OnImageCaptured.HasDelegate)" pattern for optional callbacks

- file: /Users/alexanderfedin/Projects/nolock.social/Nolock.social.frontend/NoLock.Social.Components/DocumentManager.razor
  why: Conditional CSS class patterns to follow - line 149 'class="page-item @(SelectedPageIndex == index ? "selected" : "")"'
  critical: Ternary operator pattern for conditional classes

# External References (search performed)
- url: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/event-handling?view=aspnetcore-9.0
  section: EventCallback<T> best practices  
  critical: Strongly typed EventCallback<T> preferred, HasDelegate checking, no null checking needed

- url: https://bunit.dev/docs/interaction/trigger-event-handlers.html
  section: Testing EventCallbacks
  critical: Trigger through UI interactions not direct calls, use builder pattern for parameters
```

### Current Component Structure

**CapturedImagesPreview.razor** - Current implementation:
```csharp
// Current parameters (lines 106-109)
[Parameter] public IEnumerable<CapturedImage>? CapturedImages { get; set; }
[Parameter] public EventCallback<int> OnRemoveImage { get; set; }
[Parameter] public string? Title { get; set; }
[Parameter] public bool AllowRemove { get; set; } = true;

// Current click handler (lines 22-24)
@onclick="() => ShowFullscreen(image)" 
@onkeydown="@((e) => HandleThumbnailKeyDown(e, image))"

// Current event handling (lines 113-127)
private async Task ShowFullscreen(CapturedImage image) { ... }
private async Task HandleThumbnailKeyDown(KeyboardEventArgs e, CapturedImage image) { ... }
```

### Desired Enhanced Structure

```csharp
// Add these two optional parameters (maintains backward compatibility)
[Parameter] public Func<CapturedImage, bool>? IsImageSelected { get; set; }
[Parameter] public EventCallback<CapturedImage>? OnImageSelectionToggled { get; set; }

// Replace with unified click handler
@onclick="() => HandleImageClick(image)"
@onkeydown="@((e) => HandleImageKeyDown(e, image))"

// Add conditional CSS class
<div class="card @(GetSelectionCssClass(image))">

// Add conditional selection indicator  
@if (OnImageSelectionToggled.HasValue && IsImageSelected?.Invoke(image) == true)
{
    <div class="selection-indicator">
        <i class="bi bi-check-circle-fill"></i>
    </div>
}
```

### Known Gotchas & Library Quirks

```csharp
// CRITICAL: EventCallback<T> is a struct, not a class
// No null checking needed, but use HasDelegate for optional behavior
if (OnImageSelectionToggled.HasValue && OnImageSelectionToggled.Value.HasDelegate)
{
    await OnImageSelectionToggled.Value.InvokeAsync(image);
}

// CRITICAL: EventCallback<T> vs EventCallback
// Use strongly typed EventCallback<T> for better error feedback
EventCallback<CapturedImage>  // ✅ Preferred - strongly typed
EventCallback                 // ❌ Avoid - weakly typed

// CRITICAL: Conditional CSS pattern in this codebase
@(condition ? "selected-class" : "")  // ✅ Standard pattern
@(condition && "selected-class")      // ❌ Not used in this codebase

// CRITICAL: Bootstrap Icons in use
<i class="bi bi-check-circle-fill"></i>  // ✅ Use existing icon system
<i class="fas fa-check"></i>             // ❌ FontAwesome not used

// CRITICAL: Mobile performance - use CSS transforms not layout properties
transform: scale(0.95);  // ✅ Hardware accelerated
width: 95%;             // ❌ Causes layout thrashing
```

## Implementation Blueprint

### Data Models and Structure

Core models already exist and will not be modified:
```csharp
// NoLock.Social.Core.Camera.Models.CapturedImage - EXISTING, DO NOT MODIFY
public class CapturedImage 
{
    public string Id { get; set; }        // Used for selection tracking
    public string DataUrl { get; set; }   // Used for image display  
    public DateTime Timestamp { get; set; } // Used for metadata
    // ... other existing properties remain unchanged
}

// Parent component selection management pattern:
private HashSet<string> selectedImageIds = new();
private bool IsImageSelected(CapturedImage image) => selectedImageIds.Contains(image.Id);
```

### List of Tasks to Complete (In Order)

```yaml
Task 1 - Add Optional Parameters:
MODIFY NoLock.Social.Components/Camera/CapturedImagesPreview.razor:
  - FIND pattern: "[Parameter] public bool AllowRemove { get; set; } = true;" (line ~109)
  - INJECT after this line:
    - "[Parameter] public Func<CapturedImage, bool>? IsImageSelected { get; set; }"
    - "[Parameter] public EventCallback<CapturedImage>? OnImageSelectionToggled { get; set; }"
  - PRESERVE all existing parameters unchanged

Task 2 - Replace Click Handlers:
MODIFY NoLock.Social.Components/Camera/CapturedImagesPreview.razor:
  - FIND pattern: '@onclick="() => ShowFullscreen(image)"' (line ~22)
  - REPLACE with: '@onclick="() => HandleImageClick(image)"'
  - FIND pattern: '@onkeydown="@((e) => HandleThumbnailKeyDown(e, image))"' (line ~23)  
  - REPLACE with: '@onkeydown="@((e) => HandleImageKeyDown(e, image))"'

Task 3 - Add Unified Event Handler Methods:
MODIFY NoLock.Social.Components/Camera/CapturedImagesPreview.razor:
  - FIND pattern: "private async Task HandleRemoveImage(int index)" (line ~135)
  - INJECT before this method:
    - HandleImageClick method (unified click handler)
    - HandleImageKeyDown method (unified keyboard handler) 
    - GetSelectionCssClass helper method
  - PRESERVE existing ShowFullscreen and HandleThumbnailKeyDown methods

Task 4 - Add Conditional CSS Class:
MODIFY NoLock.Social.Components/Camera/CapturedImagesPreview.razor:
  - FIND pattern: '<div class="card">' (line ~17)
  - REPLACE with: '<div class="card @(GetSelectionCssClass(image))">'
  - MIRROR pattern from DocumentManager.razor line 149

Task 5 - Add Selection Indicator:  
MODIFY NoLock.Social.Components/Camera/CapturedImagesPreview.razor:
  - FIND pattern after '<img src="@image.DataUrl"' closing tag (around line ~24)
  - INJECT before the remove button conditional:
    - Selection indicator div with conditional rendering
    - Use Bootstrap Icons (bi bi-check-circle-fill)

Task 6 - Add CSS Styling:
MODIFY NoLock.Social.Components/Camera/CapturedImagesPreview.razor:
  - FIND pattern: "</style>" (line ~103)
  - INJECT before closing tag:
    - .card.selected styles (transform, box-shadow, border)
    - .selection-indicator styles (position, opacity, transitions)
    - Mobile-specific @media queries
    - Accessibility @media queries

Task 7 - Create Unit Tests:
CREATE NoLock.Social.Components.Tests/Camera/CapturedImagesPreviewSelectionTests.cs:
  - MIRROR pattern from existing test files
  - Test backward compatibility (existing behavior unchanged)
  - Test new selection functionality
  - Test EventCallback invocation patterns
  - Use bUnit TestContext and builder pattern

Task 8 - Integration Testing:
MODIFY existing usage in DocumentCapture.razor (if needed):
  - Test that existing usage continues working unchanged
  - Add example of new selection functionality
  - Verify mobile browser compatibility
```

### Task Implementation Details

#### Task 1 - Add Optional Parameters
```csharp
// Add after line ~109 in @code section:

/// <summary>
/// Optional delegate to determine if an image is currently selected.
/// When provided, enables selection mode with visual feedback.
/// </summary>
[Parameter] public Func<CapturedImage, bool>? IsImageSelected { get; set; }

/// <summary>
/// Optional callback invoked when image selection is toggled.
/// When provided with IsImageSelected, enables selection mode.
/// </summary>
[Parameter] public EventCallback<CapturedImage>? OnImageSelectionToggled { get; set; }
```

#### Task 3 - Unified Event Handler Methods  
```csharp
// Add before HandleRemoveImage method:

private async Task HandleImageClick(CapturedImage image)
{
    // PATTERN: Intelligent behavior switching based on parameter presence
    // Selection takes precedence over fullscreen when both are possible
    if (OnImageSelectionToggled.HasValue && OnImageSelectionToggled.Value.HasDelegate)
    {
        await OnImageSelectionToggled.Value.InvokeAsync(image);
    }
    else
    {
        // FALLBACK: Default behavior maintains backward compatibility
        await ShowFullscreen(image);
    }
}

private async Task HandleImageKeyDown(KeyboardEventArgs e, CapturedImage image)
{
    if (e.Key == "Enter" || e.Key == " ")
    {
        await HandleImageClick(image);
    }
}

private string GetSelectionCssClass(CapturedImage image)
{
    // PATTERN: Follow conditional CSS pattern from DocumentManager.razor
    if (OnImageSelectionToggled.HasValue && IsImageSelected?.Invoke(image) == true)
        return "selected";
    return "";
}
```

#### Task 6 - CSS Styling
```css
/* Add before closing </style> tag: */

/* Selection visual feedback - only active when selection parameters provided */
.captured-images-preview .card.selected {
    border-color: #28a745;
    box-shadow: 0 0 0 2px rgba(40, 167, 69, 0.25);
    transform: scale(0.98);  /* Hardware accelerated */
    transition: all 0.2s cubic-bezier(0.4, 0.0, 0.2, 1);
}

.captured-images-preview .card.selected:hover {
    box-shadow: 0 0 0 2px rgba(40, 167, 69, 0.35), 0 0.125rem 0.25rem rgba(0, 0, 0, 0.075);
}

/* Selection indicator styling */
.selection-indicator {
    position: absolute;
    top: 4px;
    right: 4px;
    background: rgba(40, 167, 69, 0.9);
    color: white;
    border-radius: 50%;
    padding: 2px;
    font-size: 1rem;
    line-height: 1;
    z-index: 10;
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
}

/* Mobile optimization */
@media (max-width: 768px) {
    .selection-indicator {
        top: 2px;
        right: 2px;
        font-size: 0.85rem;
        padding: 1px;
    }
    
    .captured-images-preview .card.selected {
        transform: scale(0.99); /* Less aggressive scaling on mobile */
    }
}

/* Accessibility enhancements */
@media (prefers-reduced-motion: reduce) {
    .captured-images-preview .card.selected {
        transition: none;
        transform: none;
    }
}

/* High contrast support */
@media (prefers-contrast: high) {
    .captured-images-preview .card.selected {
        border-width: 2px;
        border-color: #28a745;
    }
    
    .selection-indicator {
        background: #28a745;
        border: 1px solid white;
    }
}
```

### Integration Points

```yaml
EXISTING_USAGE_PATTERNS:
  - file: NoLock.Social.Web/Pages/DocumentCapture.razor
  - pattern: '<CapturedImagesPreview CapturedImages="capturedImages" OnRemoveImage="HandleRemoveImage" />'
  - behavior: CONTINUES WORKING UNCHANGED (fullscreen mode by default)

NEW_USAGE_PATTERN:
  - file: NoLock.Social.Web/Pages/DocumentCapture.razor (example)
  - pattern: |
      <CapturedImagesPreview 
          CapturedImages="capturedImages"
          IsImageSelected="@(img => selectedImages.Contains(img))"
          OnImageSelectionToggled="@HandleImageSelectionToggled"
          OnRemoveImage="HandleRemoveImage" />
  - behavior: Enables selection mode with visual feedback

PARENT_COMPONENT_IMPLEMENTATION:
  - add_field: "private HashSet<CapturedImage> selectedImages = new();"
  - add_method: |
      private async Task HandleImageSelectionToggled(CapturedImage image)
      {
          if (selectedImages.Contains(image))
              selectedImages.Remove(image);
          else
              selectedImages.Add(image);
          StateHasChanged();
      }
```

## Validation Loop

### Level 1: Syntax & Build Validation

```bash
# Run these FIRST - fix any errors before proceeding
cd /Users/alexanderfedin/Projects/nolock.social/Nolock.social.frontend/NoLock.Social.Components
dotnet build

# Expected: No errors. If errors, READ the error and fix.
# Common errors:
# - Missing using statements for EventCallback
# - Syntax errors in conditional expressions  
# - Missing parameter documentation
```

### Level 2: Unit Tests

```csharp
// CREATE NoLock.Social.Components.Tests/Camera/CapturedImagesPreviewSelectionTests.cs

using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using NoLock.Social.Core.Camera.Models;
using Xunit;

namespace NoLock.Social.Components.Tests.Camera
{
    public class CapturedImagesPreviewSelectionTests : TestContext
    {
        [Fact]
        public void CapturedImagesPreview_WithoutSelectionParameters_ShowsFullscreen()
        {
            // Arrange
            var images = new List<CapturedImage>
            {
                new() { Id = "1", DataUrl = "data:image/png;base64,test", Timestamp = DateTime.UtcNow }
            };

            // Act - No selection parameters provided (backward compatibility)
            var component = RenderComponent<CapturedImagesPreview>(parameters => parameters
                .Add(p => p.CapturedImages, images));

            // Trigger click event
            var imageElement = component.Find(".captured-image-thumbnail");
            imageElement.Click();

            // Assert - Should trigger fullscreen (existing behavior)
            // Note: This test verifies click triggers ShowFullscreen, not selection
            component.Find(".selection-indicator").Should().BeNull("No selection indicator without selection parameters");
        }

        [Fact]
        public void CapturedImagesPreview_WithSelectionParameters_EnablesSelection()
        {
            // Arrange
            var images = new List<CapturedImage>
            {
                new() { Id = "1", DataUrl = "data:image/png;base64,test", Timestamp = DateTime.UtcNow }
            };
            
            var selectedImages = new HashSet<string>();
            var selectionToggled = false;

            // Act - With selection parameters provided
            var component = RenderComponent<CapturedImagesPreview>(parameters => parameters
                .Add(p => p.CapturedImages, images)
                .Add(p => p.IsImageSelected, img => selectedImages.Contains(img.Id))
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img =>
                {
                    selectionToggled = true;
                    if (selectedImages.Contains(img.Id))
                        selectedImages.Remove(img.Id);
                    else
                        selectedImages.Add(img.Id);
                })));

            // Trigger click event
            var imageElement = component.Find(".captured-image-thumbnail");
            imageElement.Click();

            // Assert - Should trigger selection, not fullscreen
            selectionToggled.Should().BeTrue("Selection callback should be invoked");
            selectedImages.Should().Contain("1", "Image should be selected");
        }

        [Fact]
        public void CapturedImagesPreview_SelectedImage_ShowsSelectionIndicator()
        {
            // Arrange
            var images = new List<CapturedImage>
            {
                new() { Id = "1", DataUrl = "data:image/png;base64,test", Timestamp = DateTime.UtcNow }
            };
            
            var selectedImages = new HashSet<string> { "1" }; // Pre-select image

            // Act
            var component = RenderComponent<CapturedImagesPreview>(parameters => parameters
                .Add(p => p.CapturedImages, images)
                .Add(p => p.IsImageSelected, img => selectedImages.Contains(img.Id))
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img => { })));

            // Assert
            var selectionIndicator = component.Find(".selection-indicator");
            selectionIndicator.Should().NotBeNull("Selected image should show selection indicator");
            
            var cardElement = component.Find(".card");
            cardElement.GetClasses().Should().Contain("selected", "Selected image should have 'selected' CSS class");
        }

        [Fact]
        public void CapturedImagesPreview_KeyboardInteraction_TriggersSelection()
        {
            // Arrange - Same as previous test but for keyboard interaction
            var images = new List<CapturedImage>
            {
                new() { Id = "1", DataUrl = "data:image/png;base64,test", Timestamp = DateTime.UtcNow }
            };
            
            var selectionToggled = false;

            var component = RenderComponent<CapturedImagesPreview>(parameters => parameters
                .Add(p => p.CapturedImages, images)
                .Add(p => p.IsImageSelected, img => false)
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img =>
                {
                    selectionToggled = true;
                })));

            // Act - Simulate Enter key press
            var imageElement = component.Find(".captured-image-thumbnail");
            imageElement.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

            // Assert
            selectionToggled.Should().BeTrue("Enter key should trigger selection");
        }
    }
}
```

```bash
# Run and iterate until passing:
cd /Users/alexanderfedin/Projects/nolock.social/Nolock.social.frontend/NoLock.Social.Components.Tests
dotnet test --filter "CapturedImagesPreviewSelectionTests" -v
# If failing: Read error, understand root cause, fix code, re-run
```

### Level 3: Integration Test

```bash
# Build and run the application
cd /Users/alexanderfedin/Projects/nolock.social/Nolock.social.frontend/NoLock.Social.Web
dotnet run

# Manual testing checklist:
# 1. Navigate to /document-capture
# 2. Verify existing functionality works (capture images, click for fullscreen, remove images)
# 3. Test on desktop browsers (Chrome, Firefox, Safari, Edge)
# 4. Test on mobile browsers (iOS Safari, Android Chrome)
# 5. Verify no performance degradation
# 6. Test keyboard navigation (Tab, Enter, Space)
# 7. Test accessibility (screen reader compatibility, high contrast)

# Expected behavior:
# - All existing functionality works unchanged
# - No visual selection indicators appear (no selection parameters provided)
# - Click behavior remains fullscreen viewing
# - Performance identical to previous version
```

## Final Validation Checklist

- [ ] All tests pass: `dotnet test --filter "CapturedImagesPreview" -v`
- [ ] No build errors: `dotnet build` (entire solution)
- [ ] No breaking changes: All existing CapturedImagesPreview usage works unchanged
- [ ] Selection functionality: New parameters enable visual selection correctly
- [ ] Mobile performance: Works smoothly on iOS Safari and Android Chrome
- [ ] CSS styling: Selection indicators only appear when appropriate
- [ ] Accessibility: Keyboard navigation and screen reader support maintained
- [ ] Cross-browser: Works on Chrome 90+, Firefox 88+, Safari 14+, Edge 90+
- [ ] Documentation: Inline XML documentation added for new parameters

---

## Anti-Patterns to Avoid

- ❌ Don't break existing API contracts - all parameters must be optional
- ❌ Don't call EventCallback.InvokeAsync() without checking HasDelegate first
- ❌ Don't use EventCallback instead of EventCallback<T> for strongly typed scenarios  
- ❌ Don't add visual indicators when no selection parameters provided
- ❌ Don't use layout-affecting CSS properties for animations (use transform instead)
- ❌ Don't test by calling methods directly - trigger through UI interactions
- ❌ Don't ignore mobile browser testing - iOS Safari and Android Chrome critical
- ❌ Don't skip accessibility media queries - high contrast and reduced motion required

## Confidence Score: 9/10

This PRP provides comprehensive context including:
- ✅ Complete technical specification from design document (45/50 TRIZ score)
- ✅ All necessary codebase patterns from research (EventCallback, CSS, testing)
- ✅ External best practices from 2025 documentation
- ✅ Step-by-step implementation blueprint with exact code changes
- ✅ Comprehensive validation strategy with executable commands
- ✅ Mobile-first optimization and accessibility requirements
- ✅ Zero breaking changes guarantee with backward compatibility testing

The agent has all necessary context for one-pass implementation success.