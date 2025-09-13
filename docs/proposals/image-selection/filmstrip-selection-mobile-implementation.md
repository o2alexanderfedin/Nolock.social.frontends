# FilmStrip Component Mobile-First Selection Implementation

## Overview
Enhance the existing `FilmStrip.razor` component with mobile-first touch interactions while maintaining 99.4% C# implementation.

## Implementation Plan

### 1. Enhance FilmStrip.razor Component

#### Add Touch Event Handling (Pure C#)
```csharp
@* Add to FilmStrip.razor - modify the existing img element *@
<img src="@image.DataUrl" 
     class="film-thumbnail cursor-pointer" 
     alt="Captured image @(index + 1)"
     @onclick="() => HandleImageClick(image)"
     @ondblclick="() => HandleImageDoubleClick(image)"
     @onpointerdown="@(e => HandlePointerDown(e, image))"
     @onpointerup="@(e => HandlePointerUp(e, image))"
     @onpointercancel="HandlePointerCancel"
     @onkeydown="@((e) => HandleImageKeyDown(e, image))"
     tabindex="0" />
```

#### Add C# Touch Detection Logic
```csharp
@code {
    // Add these fields to existing code section
    private CancellationTokenSource? _longPressCts;
    private bool _isLongPressTriggered;
    private bool _isSelectionMode;
    private CapturedImage? _pressedImage;
    private DateTime _pointerDownTime;
    
    private async Task HandlePointerDown(PointerEventArgs e, CapturedImage image)
    {
        _pressedImage = image;
        _pointerDownTime = DateTime.UtcNow;
        _isLongPressTriggered = false;
        
        // Cancel any existing long press detection
        _longPressCts?.Cancel();
        _longPressCts = new CancellationTokenSource();
        
        // Start long press detection (500ms)
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, _longPressCts.Token);
                await InvokeAsync(async () =>
                {
                    if (_pressedImage == image)
                    {
                        _isLongPressTriggered = true;
                        _isSelectionMode = true;
                        
                        // Trigger haptic feedback
                        await TriggerHapticFeedback();
                        
                        // Toggle selection
                        if (OnImageSelectionToggled.HasValue && OnImageSelectionToggled.Value.HasDelegate)
                        {
                            await OnImageSelectionToggled.Value.InvokeAsync(image);
                        }
                        
                        StateHasChanged();
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // Expected when cancelled
            }
        });
    }
    
    private void HandlePointerUp(PointerEventArgs e, CapturedImage image)
    {
        _longPressCts?.Cancel();
        var duration = (DateTime.UtcNow - _pointerDownTime).TotalMilliseconds;
        
        // Quick tap (< 200ms) in normal mode opens preview
        if (!_isLongPressTriggered && !_isSelectionMode && duration < 200)
        {
            _ = ShowFullscreen(image);
        }
        
        _pressedImage = null;
    }
    
    private void HandlePointerCancel(PointerEventArgs e)
    {
        _longPressCts?.Cancel();
        _pressedImage = null;
    }
    
    // Modify existing HandleImageClick
    private async Task HandleImageClick(CapturedImage image)
    {
        // Skip if long press was triggered
        if (_isLongPressTriggered)
        {
            _isLongPressTriggered = false;
            return;
        }
        
        // In selection mode, toggle selection
        if (_isSelectionMode && OnImageSelectionToggled.HasValue && OnImageSelectionToggled.Value.HasDelegate)
        {
            await OnImageSelectionToggled.Value.InvokeAsync(image);
        }
        // In normal mode with selection capability, single click toggles
        else if (!_isSelectionMode && OnImageSelectionToggled.HasValue && OnImageSelectionToggled.Value.HasDelegate)
        {
            await OnImageSelectionToggled.Value.InvokeAsync(image);
        }
        // Fallback: open fullscreen
        else
        {
            await ShowFullscreen(image);
        }
    }
    
    private async Task TriggerHapticFeedback()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("window.haptics.trigger", 10);
        }
        catch
        {
            // Haptic not available - silent fail
        }
    }
    
    // Add disposal
    public void Dispose()
    {
        _longPressCts?.Cancel();
        _longPressCts?.Dispose();
    }
}
```

### 2. Add Selection Mode Visual Indicator

#### Update FilmStrip.razor Markup
```razor
@* Add at the top of the component, after the FullscreenImageViewer *@
@if (_isSelectionMode)
{
    <div class="selection-mode-banner">
        <span>Selection Mode</span>
        <button class="btn btn-sm btn-outline-success" @onclick="ExitSelectionMode">Done</button>
    </div>
}

@code {
    private void ExitSelectionMode()
    {
        _isSelectionMode = false;
        StateHasChanged();
    }
}
```

### 3. Minimal JavaScript Haptic Module

Create `/wwwroot/js/haptics.js`:
```javascript
// Only 3 lines of JavaScript needed!
window.haptics = {
    trigger: (ms) => navigator.vibrate?.(ms)
};
```

Add to `index.html`:
```html
<script src="js/haptics.js"></script>
```

### 4. Enhanced CSS for Touch Targets

Add to existing FilmStrip styles:
```css
/* Ensure minimum touch target size (48px) */
.film-thumbnail {
    min-width: 48px;
    min-height: 48px;
    touch-action: manipulation; /* Prevent double-tap zoom */
    -webkit-touch-callout: none; /* Prevent long-press menu on iOS */
    user-select: none;
}

/* Visual feedback for pressed state */
.film-thumbnail:active {
    opacity: 0.8;
    transform: scale(0.98);
}

/* Selection mode banner */
.selection-mode-banner {
    position: sticky;
    top: 0;
    z-index: 100;
    background: #28a745;
    color: white;
    padding: 8px 16px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 8px;
    border-radius: 4px;
}

/* Enhanced selection indicator for mobile */
@media (max-width: 768px) {
    .selection-indicator {
        width: 32px;
        height: 32px;
        font-size: 1.2rem;
    }
    
    /* Larger touch targets on mobile */
    .film-strip .card {
        padding: 4px;
    }
}
```

### 5. Update DocumentCapture.razor Usage

The existing implementation already works correctly:
```csharp
// Already implemented in DocumentCapture.razor
private CapturedImage? selectedImage = null;

private bool IsImageSelected(CapturedImage image) => selectedImage?.Id == image.Id;

private void ToggleImageSelection(CapturedImage image)
{
    if (selectedImage?.Id == image.Id)
        selectedImage = null;
    else
        selectedImage = image;
    StateHasChanged();
}

// Add auto-select for new captures
private async Task HandleCameraImageCaptured(CapturedImage capturedImage)
{
    capturedImages.Add(capturedImage);
    selectedImage = capturedImage; // Auto-select new image
    StateHasChanged();
}
```

## Testing Checklist

### Mobile Touch Interactions
- [ ] Long press (500ms) enters selection mode with haptic feedback
- [ ] Tap in selection mode toggles image selection
- [ ] Quick tap in normal mode opens fullscreen preview
- [ ] Double-tap always opens fullscreen preview
- [ ] Selection indicator shows for selected images

### Desktop Interactions
- [ ] Single click toggles selection when selection handlers present
- [ ] Double-click opens fullscreen preview
- [ ] Keyboard navigation (Enter/Space) works

### Visual Feedback
- [ ] Selection mode banner appears after long press
- [ ] Selected images show green border and checkmark
- [ ] Touch targets are at least 48px
- [ ] Pressed state shows visual feedback

### Cross-Platform
- [ ] Test on iOS Safari
- [ ] Test on Android Chrome
- [ ] Test on Desktop Chrome/Edge
- [ ] Verify no context menus on long press

## Implementation Timeline

### Day 1 - Morning
1. Add pointer event handlers to FilmStrip.razor
2. Implement long-press detection in C#
3. Add selection mode state management
4. Test basic touch interactions

### Day 1 - Afternoon
1. Add haptic feedback (3-line JS module)
2. Implement selection mode banner
3. Update CSS for touch targets
4. Test on mobile devices

### Day 2
1. Integration testing with DocumentCapture page
2. Cross-browser testing
3. Performance optimization
4. Documentation update

## Success Metrics
- Long press detection < 100ms overhead
- Visual feedback < 16ms (60fps)
- Touch targets 100% compliant (48px minimum)
- Zero JavaScript errors
- Selection state consistency maintained

## Migration Notes
- No breaking changes to existing FilmStrip usage
- Selection parameters remain optional
- Backward compatible with existing implementations
- Progressive enhancement approach