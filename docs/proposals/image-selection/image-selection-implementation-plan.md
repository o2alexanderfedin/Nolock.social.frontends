# Image Selection Implementation Plan

## Mobile-First Hybrid Approach

A pure Blazor implementation with minimal JavaScript for haptic feedback only.

## Core Implementation

### 1. ImageGalleryItem Component

```csharp
@* ImageGalleryItem.razor *@
<div class="image-item @(IsSelected ? "selected" : "") @(_isPressed ? "pressed" : "")"
     @onpointerdown="HandlePointerDown"
     @onpointerup="HandlePointerUp"
     @onpointercancel="HandlePointerCancel"
     @onclick="HandleClick"
     @oncontextmenu="HandleContextMenu"
     @oncontextmenu:preventDefault="true">
    
    <img src="@ImageUrl" alt="@Description" />
    
    @if (IsSelected)
    {
        <div class="selection-indicator">
            <span class="checkmark">✓</span>
            <span class="selection-number">@SelectionOrder</span>
        </div>
    }
</div>

@code {
    [Parameter] public string ImageUrl { get; set; } = "";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public int SelectionOrder { get; set; }
    [Parameter] public EventCallback<ImageSelectionEventArgs> OnSelectionChange { get; set; }
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    
    private bool _isPressed;
    private bool _longPressTriggered;
    private CancellationTokenSource? _longPressCts;
    
    private async Task HandlePointerDown(PointerEventArgs e)
    {
        _isPressed = true;
        _longPressTriggered = false;
        
        // Start long press detection
        _longPressCts?.Cancel();
        _longPressCts = new CancellationTokenSource();
        
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, _longPressCts.Token);
                await InvokeAsync(async () =>
                {
                    if (_isPressed)
                    {
                        _longPressTriggered = true;
                        await TriggerHapticFeedback();
                        await OnSelectionChange.InvokeAsync(new ImageSelectionEventArgs
                        {
                            ImageUrl = ImageUrl,
                            IsLongPress = true,
                            IsSelected = !IsSelected
                        });
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // Expected when cancelled
            }
        });
    }
    
    private async Task HandlePointerUp(PointerEventArgs e)
    {
        _isPressed = false;
        _longPressCts?.Cancel();
        StateHasChanged();
    }
    
    private void HandlePointerCancel(PointerEventArgs e)
    {
        _isPressed = false;
        _longPressCts?.Cancel();
        StateHasChanged();
    }
    
    private async Task HandleClick(MouseEventArgs e)
    {
        if (_longPressTriggered)
        {
            _longPressTriggered = false;
            return;
        }
        
        await OnSelectionChange.InvokeAsync(new ImageSelectionEventArgs
        {
            ImageUrl = ImageUrl,
            IsLongPress = false,
            IsSelected = !IsSelected
        });
    }
    
    private void HandleContextMenu(MouseEventArgs e)
    {
        // Context menu prevented via @oncontextmenu:preventDefault
    }
    
    private async Task TriggerHapticFeedback()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("window.haptics.trigger", "light");
        }
        catch
        {
            // Haptics not available
        }
    }
    
    public class ImageSelectionEventArgs
    {
        public string ImageUrl { get; set; } = "";
        public bool IsLongPress { get; set; }
        public bool IsSelected { get; set; }
    }
}
```

### 2. ImageGallery Container

```csharp
@* ImageGallery.razor *@
<div class="image-gallery @(_selectionMode ? "selection-mode" : "")">
    <div class="gallery-header">
        @if (_selectionMode)
        {
            <div class="selection-toolbar">
                <button @onclick="ClearSelection" class="btn-clear">Clear</button>
                <span class="selection-count">@_selectedImages.Count selected</span>
                <button @onclick="ConfirmSelection" class="btn-confirm">Done</button>
            </div>
        }
    </div>
    
    <div class="gallery-grid">
        @foreach (var image in Images)
        {
            <ImageGalleryItem ImageUrl="@image.Url"
                              Description="@image.Description"
                              IsSelected="@_selectedImages.ContainsKey(image.Url)"
                              SelectionOrder="@GetSelectionOrder(image.Url)"
                              OnSelectionChange="@(args => HandleSelectionChange(args))" />
        }
    </div>
</div>

@code {
    [Parameter] public List<ImageData> Images { get; set; } = new();
    [Parameter] public EventCallback<List<string>> OnSelectionConfirmed { get; set; }
    
    private bool _selectionMode;
    private Dictionary<string, int> _selectedImages = new();
    private int _nextSelectionOrder = 1;
    
    private async Task HandleSelectionChange(ImageGalleryItem.ImageSelectionEventArgs args)
    {
        if (args.IsLongPress && !_selectionMode)
        {
            // Enter selection mode
            _selectionMode = true;
            _selectedImages.Clear();
            _nextSelectionOrder = 1;
        }
        
        if (_selectionMode)
        {
            if (args.IsSelected && !_selectedImages.ContainsKey(args.ImageUrl))
            {
                _selectedImages[args.ImageUrl] = _nextSelectionOrder++;
            }
            else if (!args.IsSelected && _selectedImages.ContainsKey(args.ImageUrl))
            {
                var removedOrder = _selectedImages[args.ImageUrl];
                _selectedImages.Remove(args.ImageUrl);
                
                // Reorder remaining selections
                foreach (var key in _selectedImages.Keys.ToList())
                {
                    if (_selectedImages[key] > removedOrder)
                    {
                        _selectedImages[key]--;
                    }
                }
                _nextSelectionOrder--;
            }
        }
        else
        {
            // Single tap in normal mode - navigate or preview
            await HandleSingleImageTap(args.ImageUrl);
        }
        
        StateHasChanged();
    }
    
    private void ClearSelection()
    {
        _selectedImages.Clear();
        _selectionMode = false;
        _nextSelectionOrder = 1;
        StateHasChanged();
    }
    
    private async Task ConfirmSelection()
    {
        var orderedUrls = _selectedImages
            .OrderBy(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();
        
        await OnSelectionConfirmed.InvokeAsync(orderedUrls);
        ClearSelection();
    }
    
    private int GetSelectionOrder(string imageUrl)
    {
        return _selectedImages.TryGetValue(imageUrl, out var order) ? order : 0;
    }
    
    private async Task HandleSingleImageTap(string imageUrl)
    {
        // Navigation or preview logic here
    }
    
    public class ImageData
    {
        public string Url { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
```

### 3. JavaScript Haptic Wrapper

```javascript
// wwwroot/js/haptics.js
window.haptics = {
    trigger: function(intensity) {
        if (window.navigator && window.navigator.vibrate) {
            window.navigator.vibrate(intensity === 'light' ? 10 : 20);
        }
    }
};
```

### 4. Required CSS

```css
/* wwwroot/css/image-gallery.css */

.image-gallery {
    --selection-color: #2196F3;
    --touch-target-min: 48px;
}

.gallery-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
    gap: 8px;
    padding: 8px;
}

.image-item {
    position: relative;
    aspect-ratio: 1;
    border-radius: 8px;
    overflow: hidden;
    cursor: pointer;
    transition: transform 0.2s, box-shadow 0.2s;
    min-height: var(--touch-target-min);
    min-width: var(--touch-target-min);
}

.image-item img {
    width: 100%;
    height: 100%;
    object-fit: cover;
    pointer-events: none;
}

.image-item.pressed {
    transform: scale(0.95);
    opacity: 0.8;
}

.image-item.selected {
    box-shadow: 0 0 0 3px var(--selection-color);
    transform: scale(0.98);
}

.selection-indicator {
    position: absolute;
    top: 8px;
    right: 8px;
    width: 28px;
    height: 28px;
    background: var(--selection-color);
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    color: white;
    font-weight: bold;
    font-size: 14px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.2);
}

.selection-indicator .checkmark {
    display: none;
}

.selection-indicator .selection-number {
    display: block;
}

.selection-toolbar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 12px 16px;
    background: white;
    border-bottom: 1px solid #e0e0e0;
    position: sticky;
    top: 0;
    z-index: 10;
}

.selection-count {
    font-weight: 500;
    color: #666;
}

.btn-clear, .btn-confirm {
    padding: 8px 16px;
    border: none;
    border-radius: 4px;
    font-weight: 500;
    min-width: var(--touch-target-min);
    min-height: 36px;
}

.btn-clear {
    background: transparent;
    color: #666;
}

.btn-confirm {
    background: var(--selection-color);
    color: white;
}

/* Mobile-specific adjustments */
@media (max-width: 768px) {
    .gallery-grid {
        grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
        gap: 4px;
        padding: 4px;
    }
}

/* Disable text selection during interaction */
.image-gallery.selection-mode {
    -webkit-user-select: none;
    user-select: none;
    -webkit-touch-callout: none;
}
```

## Implementation Steps

### Phase 1: Core Component Setup (Day 1)
1. Create `ImageGalleryItem.razor` component with pointer event handlers
2. Implement long-press detection using C# Task.Delay
3. Add visual feedback states (pressed, selected)
4. Wire up selection event callbacks

### Phase 2: Gallery Container (Day 1)
1. Create `ImageGallery.razor` container component
2. Implement selection mode toggling
3. Add selection tracking with order preservation
4. Build selection toolbar with clear/done actions

### Phase 3: Haptic Feedback (Day 1)
1. Add 3-line JavaScript haptic wrapper to wwwroot/js/haptics.js
2. Register script in index.html
3. Call from C# component on long-press detection

### Phase 4: Styling & Polish (Day 2)
1. Apply CSS with proper touch targets (48px minimum)
2. Add pressed and selected visual states
3. Implement selection indicators with numbers
4. Ensure mobile-responsive grid layout

### Phase 5: Integration (Day 2)
1. Replace existing image gallery implementations
2. Connect to parent pages/components
3. Handle selection confirmed events
4. Test with real image data

## Testing Checklist

### Functional Tests
- [ ] Long press (500ms) enters selection mode
- [ ] Tap in selection mode toggles image selection
- [ ] Selection numbers update correctly when images deselected
- [ ] Clear button resets all selections
- [ ] Done button returns ordered selection list
- [ ] Normal tap outside selection mode triggers navigation/preview

### Mobile-Specific Tests
- [ ] Test on iOS Safari (iPhone 12+)
- [ ] Test on Android Chrome (Pixel 5+)
- [ ] Verify touch targets are at least 48px
- [ ] Confirm no context menu appears on long press
- [ ] Check haptic feedback triggers (where supported)
- [ ] Verify no text selection during interactions

### Performance Tests
- [ ] Gallery with 100+ images scrolls smoothly
- [ ] Selection state updates are immediate
- [ ] No memory leaks from event handlers
- [ ] Component disposal cleans up properly

### Edge Cases
- [ ] Rapid tapping doesn't break selection state
- [ ] Pointer cancel (drag off element) handled correctly
- [ ] Works with keyboard navigation (desktop)
- [ ] Handles empty gallery gracefully

## Migration Phases

### Phase 1: Parallel Implementation (Week 1)
- Build new components alongside existing gallery
- No breaking changes to current functionality
- Feature flag to enable new selection mode

### Phase 2: Gradual Rollout (Week 2)
- Enable for specific pages/users
- Monitor performance and user feedback
- Fix any discovered issues

### Phase 3: Full Migration (Week 3)
- Replace all gallery instances
- Remove old implementation
- Update documentation

## Success Metrics
- Selection mode activation < 100ms after long press
- Visual feedback latency < 16ms (60fps)
- Zero JavaScript errors in production
- 100% touch target compliance (48px minimum)
- Selection state consistency across all interactions