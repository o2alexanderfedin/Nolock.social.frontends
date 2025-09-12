# PRP: Fullscreen Image Preview Popup

## Feature Overview

Implement a fullscreen image preview popup component for the NoLock.Social Blazor WebAssembly application that displays captured images in fullscreen mode with mobile-responsive behavior and touch interaction support.

**Key Requirements:**
- Click thumbnail to open fullscreen preview
- Fullscreen image display with smooth transitions
- Mobile device rotation detection and optimization
- Close popup with ESC key or backdrop click
- Preserve image quality in fullscreen mode
- Mobile-first responsive design with touch support

## Architecture Decision: Simplified Approach

Based on the approved architecture document (Version 2.0), this implementation uses **BlazorPro.BlazorSize** NuGet package instead of custom JavaScript interop, following the **Productive Laziness Principle** and **KISS/DRY/YAGNI/TRIZ** guidelines.

## Research Findings & Context

### 1. Existing Codebase Patterns to Follow

#### Modal Pattern (from `/NoLock.Social.Components/Identity/LoginModal.razor`)
```csharp
@if (ShowModal)
{
    <div class="modal-backdrop" @onclick="HandleBackdropClick">
        <div class="modal-dialog" @onclick:stopPropagation="true">
            <!-- Modal content -->
        </div>
    </div>
}

private async Task HandleBackdropClick()
{
    // Handle backdrop click logic
}
```

#### CSS Patterns (from `/NoLock.Social.Web/wwwroot/css/glass-modal.css`)
```css
.modal-backdrop {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background-color: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1050;
    animation: fadeIn 0.3s ease-in-out;
}

.glass-modal {
    background: rgba(255, 255, 255, 0.27);
    border-radius: 16px;
    backdrop-filter: blur(5.5px);
    -webkit-backdrop-filter: blur(5.5px);
}
```

#### Integration Point (from `/NoLock.Social.Components/Camera/CapturedImagesPreview.razor`)
Current thumbnail structure:
```csharp
<img src="@image.DataUrl" 
     class="card-img-top captured-image-thumbnail" 
     alt="Captured image @(index + 1)" />
```
**Integration Point:** Add click handler to this img element.

#### Data Model (from `/NoLock.Social.Core/Camera/Models/CapturedImage.cs`)
```csharp
public class CapturedImage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ImageData { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Quality { get; set; }
    public string DataUrl => ImageUrl; // Use this for image display
}
```

#### Service Registration Pattern (from `/NoLock.Social.Web/Program.cs`)
```csharp
builder.Services.AddScoped<IServiceInterface, ServiceImplementation>();
```

#### Test Pattern (from test files in `/NoLock.Social.Components.Tests/`)
```csharp
public class ComponentTests : TestContext
{
    private readonly Mock<IService> _mockService;
    
    public ComponentTests()
    {
        _mockService = new Mock<IService>();
        Services.AddSingleton(_mockService.Object);
    }

    [Fact]
    public void Component_InitialRender_RendersCorrectly()
    {
        // Arrange & Act
        var component = RenderComponent<Component>();
        
        // Assert
        Assert.NotNull(component);
    }
}
```

### 2. External Library Research

#### BlazorPro.BlazorSize Package
- **NuGet Package**: `BlazorPro.BlazorSize` version 9.0.0
- **GitHub Repository**: https://github.com/EdCharbeneau/BlazorSize
- **Documentation**: GitHub Wiki at https://github.com/EdCharbeneau/BlazorSize/wiki
- **Purpose**: JavaScript interop for detecting browser size changes and media queries

**Key API Elements:**
```csharp
@using BlazorPro.BlazorSize
@inject IResizeListener ResizeListener

// Usage pattern
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        ResizeListener.OnResized += OnBrowserResize;
    }
}

private async void OnBrowserResize(object? sender, BrowserWindowSize window)
{
    // window.Width and window.Height available
    // Detect orientation: window.Width > window.Height = landscape
}

public async ValueTask DisposeAsync()
{
    ResizeListener.OnResized -= OnBrowserResize;
}
```

### 3. Mobile Touch Handling Research

#### CSS-First Touch Approach (Recommended)
```css
/* Prevent zoom/pan/scroll on image */
.fullscreen-image {
    touch-action: none;
    user-select: none;
    -webkit-user-select: none;
    -webkit-touch-callout: none;
}

/* Allow vertical pan for swipe-to-close gesture */
.fullscreen-backdrop {
    touch-action: pan-y manipulation;
}
```

#### Native Blazor Touch Events (Minimal Implementation)
```csharp
<div @ontouchstart="HandleTouchStart" @ontouchend="HandleTouchEnd">
    <!-- Content -->
</div>

private void HandleTouchStart(TouchEventArgs e) { /* Simple implementation */ }
private async Task HandleTouchEnd(TouchEventArgs e) { /* Simple swipe detection */ }
```

## Implementation Plan

### Phase 1: Package Installation & Setup (15 minutes)

1. **Install NuGet Package**
   ```xml
   <PackageReference Include="BlazorPro.BlazorSize" Version="9.0.0" />
   ```

2. **Register Service** (add to `/NoLock.Social.Web/Program.cs`)
   ```csharp
   builder.Services.AddScoped<IResizeListener, ResizeListener>();
   ```

### Phase 2: Component Implementation (45 minutes)

3. **Create Component** (`/NoLock.Social.Components/Camera/FullscreenImageViewer.razor`)

**Complete Implementation Blueprint:**
```csharp
@namespace NoLock.Social.Components.Camera
@using BlazorPro.BlazorSize
@using NoLock.Social.Core.Camera.Models
@implements IAsyncDisposable
@inject IResizeListener ResizeListener
@inject ILogger<FullscreenImageViewer>? Logger

@if (IsVisible && CurrentImage != null)
{
    <div class="modal-backdrop fullscreen-backdrop @VisibilityClass" 
         @onclick="HandleBackdropClick"
         @ontouchstart="HandleTouchStart" 
         @ontouchend="HandleTouchEnd"
         @onkeydown="HandleKeyDown"
         tabindex="-1">
         
        <div class="fullscreen-container @OrientationClass" @onclick:stopPropagation="true">
            <img src="@CurrentImage.DataUrl" 
                 class="fullscreen-image" 
                 alt="Fullscreen preview of captured image"
                 @onload="OnImageLoaded"
                 @onerror="OnImageError" />
            
            <button class="btn-close-viewer" 
                    @onclick="CloseAsync" 
                    title="Close (ESC)"
                    aria-label="Close fullscreen image">
                <i class="bi bi-x-lg"></i>
            </button>
        </div>
    </div>
}

@code {
    [Parameter] public EventCallback OnClosed { get; set; }
    
    // State Management
    private CapturedImage? CurrentImage;
    private bool IsVisible;
    private bool IsLandscape;
    private bool ImageLoaded;
    private bool ImageError;
    
    // Touch handling
    private bool _touchStarted;
    private double _touchStartY;
    
    // Computed properties
    private string VisibilityClass => IsVisible ? "show" : "hide";
    private string OrientationClass => IsLandscape ? "landscape" : "portrait";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            ResizeListener.OnResized += OnBrowserResize;
        }
    }

    // Public API
    public async Task ShowAsync(CapturedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        
        CurrentImage = image;
        IsVisible = true;
        ImageLoaded = false;
        ImageError = false;
        
        await InvokeAsync(StateHasChanged);
        
        // Focus for keyboard navigation
        await Task.Delay(100); // Allow render
        // Focus will be handled by CSS tabindex
    }

    public async Task CloseAsync()
    {
        IsVisible = false;
        CurrentImage = null;
        ImageLoaded = false;
        ImageError = false;
        
        await InvokeAsync(StateHasChanged);
        await OnClosed.InvokeAsync();
    }

    // Event Handlers
    private async void OnBrowserResize(object? sender, BrowserWindowSize window)
    {
        try
        {
            var wasLandscape = IsLandscape;
            IsLandscape = window.Width > window.Height;
            
            if (wasLandscape != IsLandscape)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error handling browser resize");
        }
    }

    private async Task HandleBackdropClick()
    {
        await CloseAsync();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            await CloseAsync();
        }
    }

    private void HandleTouchStart(TouchEventArgs e)
    {
        if (e.Touches.Length > 0)
        {
            _touchStarted = true;
            _touchStartY = e.Touches[0].ClientY;
        }
    }

    private async Task HandleTouchEnd(TouchEventArgs e)
    {
        if (!_touchStarted || e.ChangedTouches.Length == 0) return;
        
        try
        {
            var swipeDistance = e.ChangedTouches[0].ClientY - _touchStartY;
            
            // Simple swipe down to close (100px threshold)
            if (swipeDistance > 100)
            {
                await CloseAsync();
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error handling touch end");
        }
        finally
        {
            _touchStarted = false;
        }
    }

    private void OnImageLoaded()
    {
        ImageLoaded = true;
        ImageError = false;
        StateHasChanged();
    }

    private void OnImageError()
    {
        ImageError = true;
        ImageLoaded = false;
        StateHasChanged();
    }

    // Cleanup
    public async ValueTask DisposeAsync()
    {
        try
        {
            ResizeListener.OnResized -= OnBrowserResize;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error disposing FullscreenImageViewer");
        }
    }
}
```

### Phase 3: CSS Implementation (30 minutes)

4. **Create Component Styles** (`/NoLock.Social.Components/Camera/FullscreenImageViewer.razor.css`)

```css
/* Fullscreen backdrop */
.fullscreen-backdrop {
    position: fixed;
    top: 0;
    left: 0;
    width: 100vw;
    height: 100vh;
    height: 100dvh; /* Future CSS standard */
    background: rgba(0, 0, 0, 0.9);
    z-index: 1060; /* Higher than modal backdrop */
    display: flex;
    align-items: center;
    justify-content: center;
    
    /* Glass effect */
    backdrop-filter: blur(4px);
    animation: fadeIn 0.3s ease-out;
    
    /* Touch handling */
    touch-action: pan-y manipulation;
}

/* Progressive enhancement for backdrop-filter */
@supports not (backdrop-filter: blur(4px)) {
    .fullscreen-backdrop {
        background: rgba(0, 0, 0, 0.95);
    }
}

/* iOS Safari support */
@supports (-webkit-touch-callout: none) {
    .fullscreen-backdrop {
        padding-top: env(safe-area-inset-top);
        padding-bottom: env(safe-area-inset-bottom);
        padding-left: env(safe-area-inset-left);
        padding-right: env(safe-area-inset-right);
    }
}

.fullscreen-container {
    position: relative;
    width: 100%;
    height: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 1rem;
}

.fullscreen-image {
    max-width: 100%;
    max-height: 100%;
    object-fit: contain;
    border-radius: 8px;
    box-shadow: 0 20px 60px rgba(0, 0, 0, 0.4);
    transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    
    /* CSS-first touch handling */
    touch-action: none;
    user-select: none;
    -webkit-user-select: none;
    -webkit-touch-callout: none;
}

/* Close button */
.btn-close-viewer {
    position: absolute;
    top: 1rem;
    right: 1rem;
    background: rgba(255, 255, 255, 0.9);
    border: none;
    border-radius: 50%;
    width: 48px;
    height: 48px;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    transition: all 0.2s ease;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
    z-index: 1061;
}

.btn-close-viewer:hover {
    background: white;
    transform: scale(1.05);
}

/* Mobile touch feedback */
@media (hover: none) and (pointer: coarse) {
    .btn-close-viewer:active {
        transform: scale(0.95);
        background: rgba(255, 255, 255, 1);
    }
}

/* Mobile orientation optimization */
@media (orientation: landscape) and (max-width: 1024px) {
    .fullscreen-container.landscape {
        padding: 0.5rem;
    }
    
    .fullscreen-image {
        max-height: 95vh;
        max-width: 98vw;
    }
}

@media (orientation: portrait) and (max-width: 768px) {
    .fullscreen-container.portrait {
        padding: 1rem 0.5rem;
    }
    
    .fullscreen-image {
        max-height: 90vh;
        max-width: 95vw;
    }
}

/* Animations */
@keyframes fadeIn {
    from { opacity: 0; }
    to { opacity: 1; }
}

.fullscreen-backdrop.show {
    animation: fadeIn 0.3s ease-out;
}

.fullscreen-backdrop.hide {
    animation: fadeOut 0.3s ease-out;
}

@keyframes fadeOut {
    from { opacity: 1; }
    to { opacity: 0; }
}

/* High contrast mode support */
@media (prefers-contrast: high) {
    .fullscreen-backdrop {
        background: rgba(0, 0, 0, 1);
    }
    
    .btn-close-viewer {
        background: white;
        border: 2px solid black;
    }
}

/* Reduced motion support */
@media (prefers-reduced-motion: reduce) {
    .fullscreen-backdrop,
    .fullscreen-image,
    .btn-close-viewer {
        transition: none;
        animation: none;
    }
}
```

### Phase 4: Integration (20 minutes)

5. **Update CapturedImagesPreview.razor** (modify existing file)

Add to the top of the file:
```csharp
<FullscreenImageViewer @ref="fullscreenViewer" OnClosed="OnFullscreenClosed" />
```

Modify the existing image element:
```csharp
<img src="@image.DataUrl" 
     class="card-img-top captured-image-thumbnail cursor-pointer" 
     alt="Captured image @(index + 1)"
     @onclick="() => ShowFullscreen(image)" 
     @onkeydown="@((e) => HandleThumbnailKeyDown(e, image))"
     tabindex="0" />
```

Add to @code section:
```csharp
private FullscreenImageViewer? fullscreenViewer;

private async Task ShowFullscreen(CapturedImage image)
{
    if (fullscreenViewer != null)
    {
        await fullscreenViewer.ShowAsync(image);
    }
}

private async Task HandleThumbnailKeyDown(KeyboardEventArgs e, CapturedImage image)
{
    if (e.Key == "Enter" || e.Key == " ")
    {
        await ShowFullscreen(image);
    }
}

private Task OnFullscreenClosed()
{
    // Optional: Handle post-close logic
    return Task.CompletedTask;
}
```

Add CSS class to existing styles:
```css
.cursor-pointer {
    cursor: pointer;
}

.captured-image-thumbnail:focus {
    outline: 2px solid #007bff;
    outline-offset: 2px;
}
```

### Phase 5: Testing Implementation (30 minutes)

6. **Create Unit Tests** (`/NoLock.Social.Components.Tests/Camera/FullscreenImageViewerTests.cs`)

```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Components.Camera;
using NoLock.Social.Core.Camera.Models;
using BlazorPro.BlazorSize;
using Xunit;

namespace NoLock.Social.Components.Tests.Camera
{
    public class FullscreenImageViewerTests : TestContext
    {
        private readonly Mock<IResizeListener> _mockResizeListener;
        private readonly Mock<ILogger<FullscreenImageViewer>> _mockLogger;

        public FullscreenImageViewerTests()
        {
            _mockResizeListener = new Mock<IResizeListener>();
            _mockLogger = new Mock<ILogger<FullscreenImageViewer>>();

            // Register services
            Services.AddSingleton(_mockResizeListener.Object);
            Services.AddSingleton(_mockLogger.Object);
        }

        [Fact]
        public void Component_InitialRender_DoesNotShowModal()
        {
            // Arrange & Act
            var component = RenderComponent<FullscreenImageViewer>();

            // Assert
            Assert.DoesNotContain("fullscreen-backdrop", component.Markup);
        }

        [Fact]
        public async Task ShowAsync_WithValidImage_ShowsModal()
        {
            // Arrange
            var component = RenderComponent<FullscreenImageViewer>();
            var image = new CapturedImage 
            { 
                Id = "test-id",
                ImageUrl = "data:image/jpeg;base64,test-data",
                Width = 800,
                Height = 600
            };

            // Act
            await component.Instance.ShowAsync(image);

            // Assert
            Assert.Contains("fullscreen-backdrop", component.Markup);
            Assert.Contains("test-data", component.Markup);
        }

        [Fact]
        public async Task CloseAsync_WhenVisible_HidesModal()
        {
            // Arrange
            var component = RenderComponent<FullscreenImageViewer>();
            var image = new CapturedImage { ImageUrl = "test.jpg" };
            await component.Instance.ShowAsync(image);

            // Act
            await component.Instance.CloseAsync();

            // Assert
            Assert.DoesNotContain("fullscreen-backdrop", component.Markup);
        }

        [Fact]
        public async Task BackdropClick_WhenVisible_ClosesModal()
        {
            // Arrange
            var component = RenderComponent<FullscreenImageViewer>();
            var image = new CapturedImage { ImageUrl = "test.jpg" };
            await component.Instance.ShowAsync(image);

            // Act
            var backdrop = component.Find(".fullscreen-backdrop");
            await backdrop.ClickAsync();

            // Assert
            Assert.DoesNotContain("fullscreen-backdrop", component.Markup);
        }

        [Fact]
        public async Task ShowAsync_WithNullImage_ThrowsArgumentNullException()
        {
            // Arrange
            var component = RenderComponent<FullscreenImageViewer>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                component.Instance.ShowAsync(null!));
        }

        [Fact]
        public async Task EscapeKey_WhenVisible_ClosesModal()
        {
            // Arrange
            var component = RenderComponent<FullscreenImageViewer>();
            var image = new CapturedImage { ImageUrl = "test.jpg" };
            await component.Instance.ShowAsync(image);

            // Act
            var backdrop = component.Find(".fullscreen-backdrop");
            await backdrop.KeyDownAsync("Escape");

            // Assert
            Assert.DoesNotContain("fullscreen-backdrop", component.Markup);
        }
    }
}
```

7. **Create Integration Tests** (`/NoLock.Social.Components.Tests/Camera/CapturedImagesPreviewIntegrationTests.cs`)

```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NoLock.Social.Components.Camera;
using NoLock.Social.Core.Camera.Models;
using BlazorPro.BlazorSize;
using Xunit;

namespace NoLock.Social.Components.Tests.Camera
{
    public class CapturedImagesPreviewIntegrationTests : TestContext
    {
        private readonly Mock<IResizeListener> _mockResizeListener;

        public CapturedImagesPreviewIntegrationTests()
        {
            _mockResizeListener = new Mock<IResizeListener>();
            Services.AddSingleton(_mockResizeListener.Object);
        }

        [Fact]
        public async Task ThumbnailClick_OpensFullscreenViewer()
        {
            // Arrange
            var images = new List<CapturedImage>
            {
                new() { Id = "1", ImageUrl = "test1.jpg", Width = 800, Height = 600 },
                new() { Id = "2", ImageUrl = "test2.jpg", Width = 1024, Height = 768 }
            };

            var component = RenderComponent<CapturedImagesPreview>(parameters => parameters
                .Add(p => p.CapturedImages, images)
                .Add(p => p.AllowRemove, true));

            // Act
            var thumbnail = component.Find(".captured-image-thumbnail");
            await thumbnail.ClickAsync();

            // Assert
            Assert.Contains("fullscreen-backdrop", component.Markup);
            Assert.Contains("test1.jpg", component.Markup);
        }
    }
}
```

## Validation Gates

**Execute these commands to validate implementation:**

```bash
# 1. Build verification
cd NoLock.Social.Web && dotnet build

# 2. Test verification  
dotnet test

# 3. Runtime verification
cd NoLock.Social.Web && dotnet run

# 4. Linting (if available)
dotnet format --verify-no-changes
```

**Manual Testing Checklist:**
- [ ] Click thumbnail opens fullscreen view
- [ ] ESC key closes viewer
- [ ] Backdrop click closes viewer  
- [ ] Close button works
- [ ] Image displays correctly
- [ ] Responsive behavior on mobile
- [ ] Touch swipe-down closes on mobile
- [ ] Orientation changes work correctly
- [ ] Keyboard navigation works (Tab, Enter, Space, ESC)
- [ ] Error handling for broken images
- [ ] High contrast mode support
- [ ] Reduced motion preference respected

## Error Handling Strategy

1. **Image Loading Errors**: Display error state, log error, allow closing
2. **Resize Listener Errors**: Log error, continue with fallback behavior  
3. **Touch Event Errors**: Log error, don't break interaction
4. **Null Reference Prevention**: ArgumentNullException.ThrowIfNull for parameters
5. **Async Disposal Errors**: Try-catch with logging, don't throw

## URLs & Documentation References

- **BlazorPro.BlazorSize**: https://github.com/EdCharbeneau/BlazorSize
- **NuGet Package**: https://www.nuget.org/packages/BlazorPro.BlazorSize
- **Blazor Component Documentation**: https://docs.microsoft.com/en-us/aspnet/core/blazor/components/
- **CSS Touch-Action**: https://developer.mozilla.org/en-US/docs/Web/CSS/touch-action
- **CSS Backdrop-Filter**: https://developer.mozilla.org/en-US/docs/Web/CSS/backdrop-filter
- **Blazor Touch Events**: https://docs.microsoft.com/en-us/aspnet/core/blazor/components/event-handling

## Implementation Tasks Checklist

- [ ] **Task 1**: Install BlazorPro.BlazorSize NuGet package
- [ ] **Task 2**: Register IResizeListener service in Program.cs
- [ ] **Task 3**: Create FullscreenImageViewer.razor component
- [ ] **Task 4**: Create FullscreenImageViewer.razor.css styles
- [ ] **Task 5**: Update CapturedImagesPreview.razor integration
- [ ] **Task 6**: Create unit tests for FullscreenImageViewer
- [ ] **Task 7**: Create integration tests for CapturedImagesPreview
- [ ] **Task 8**: Execute validation gates (build, test, run)
- [ ] **Task 9**: Manual testing on desktop and mobile
- [ ] **Task 10**: Accessibility testing (keyboard navigation, screen readers)

## Expected Outcomes

After implementation:
- Users can click any captured image thumbnail to view it fullscreen
- Fullscreen viewer works responsively on desktop and mobile
- Touch interactions work naturally (tap to close, swipe down to close)
- Keyboard navigation is fully accessible
- Component follows existing patterns and integrates seamlessly
- All tests pass and code builds without errors
- Performance is optimal (no custom JavaScript, leverages battle-tested package)

## Confidence Score: 9/10

**High confidence for one-pass implementation because:**
- ✅ All existing patterns identified and documented
- ✅ External library researched with specific API usage  
- ✅ Complete code examples provided for all components
- ✅ CSS patterns from existing codebase included
- ✅ Test patterns from existing codebase documented  
- ✅ Clear validation gates defined
- ✅ Integration points specifically identified
- ✅ Error handling strategy defined
- ✅ Follows approved architecture (BlazorPro.BlazorSize approach)
- ✅ Comprehensive mobile and accessibility considerations

**Only risk (-1 point)**: Potential minor integration issues with exact CSS class names or event handling details that may need tiny adjustments during implementation, but these would be easily resolvable.