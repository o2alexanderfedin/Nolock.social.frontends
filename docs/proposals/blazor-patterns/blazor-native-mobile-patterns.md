# Blazor Native Mobile Patterns

## Executive Summary
This proposal demonstrates how to implement mobile-native features in Blazor with zero or minimal JavaScript, leveraging C#/.NET capabilities for gesture detection, haptic feedback, and other mobile interactions.

## Haptic Feedback Implementation

### Minimal JavaScript Module (3 lines)
```javascript
// haptic.js - Thin wrapper for native API
export const trigger = (type) => navigator.vibrate?.(type === 'light' ? 10 : type === 'medium' ? 25 : 50);
export const pattern = (p) => navigator.vibrate?.(p);
export const stop = () => navigator.vibrate?.(0);
```

### C# Service with Full Logic
```csharp
public interface IHapticService
{
    ValueTask TriggerAsync(HapticFeedbackType type);
    ValueTask PlayPatternAsync(int[] pattern);
    ValueTask StopAsync();
}

public class HapticService : IHapticService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public HapticService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        if (_module != null) return _module;
        
        await _initLock.WaitAsync();
        try
        {
            _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/haptic.js");
            return _module;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask TriggerAsync(HapticFeedbackType type)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("trigger", type.ToString().ToLower());
    }

    public async ValueTask PlayPatternAsync(int[] pattern)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("pattern", pattern);
    }

    public async ValueTask StopAsync()
    {
        if (_module != null)
            await _module.InvokeVoidAsync("stop");
    }

    public async ValueTask DisposeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_module != null)
            {
                await _module.DisposeAsync();
                _module = null;
            }
        }
        finally
        {
            _initLock.Release();
            _initLock.Dispose();
        }
    }
}
```

## Blazor-Native Gesture Detection

### Pure C# Gesture Recognition (Zero JavaScript)

```csharp
public class GestureRecognizer
{
    private readonly Dictionary<long, TouchPoint> _activeTouches = new();
    private readonly Stopwatch _stopwatch = new();
    private DateTime? _lastTapTime;
    private Point? _lastTapPosition;
    private const double DoubleTapThreshold = 300; // ms
    private const double DoubleTapRadius = 20; // pixels

    public event Action<SwipeDirection>? OnSwipe;
    public event Action<double>? OnPinch;
    public event Action? OnDoubleTap;

    public void HandleTouchStart(TouchEventArgs e)
    {
        _stopwatch.Restart();
        
        foreach (var touch in e.Touches)
        {
            _activeTouches[touch.Identifier] = new TouchPoint
            {
                StartX = touch.ClientX,
                StartY = touch.ClientY,
                CurrentX = touch.ClientX,
                CurrentY = touch.ClientY,
                StartTime = DateTime.UtcNow
            };
        }

        // Check for double tap
        if (e.Touches.Length == 1)
        {
            var touch = e.Touches[0];
            var now = DateTime.UtcNow;
            
            if (_lastTapTime.HasValue && _lastTapPosition.HasValue)
            {
                var timeDiff = (now - _lastTapTime.Value).TotalMilliseconds;
                var distance = Math.Sqrt(
                    Math.Pow(touch.ClientX - _lastTapPosition.Value.X, 2) +
                    Math.Pow(touch.ClientY - _lastTapPosition.Value.Y, 2));

                if (timeDiff < DoubleTapThreshold && distance < DoubleTapRadius)
                {
                    OnDoubleTap?.Invoke();
                    _lastTapTime = null;
                    _lastTapPosition = null;
                    return;
                }
            }

            _lastTapTime = now;
            _lastTapPosition = new Point(touch.ClientX, touch.ClientY);
        }
    }

    public void HandleTouchMove(TouchEventArgs e)
    {
        // Update touch positions
        foreach (var touch in e.Touches)
        {
            if (_activeTouches.ContainsKey(touch.Identifier))
            {
                _activeTouches[touch.Identifier].CurrentX = touch.ClientX;
                _activeTouches[touch.Identifier].CurrentY = touch.ClientY;
            }
        }

        // Detect pinch gesture
        if (_activeTouches.Count == 2)
        {
            var touches = _activeTouches.Values.ToArray();
            
            // Calculate initial distance
            var initialDistance = CalculateDistance(
                touches[0].StartX, touches[0].StartY,
                touches[1].StartX, touches[1].StartY);
            
            // Calculate current distance
            var currentDistance = CalculateDistance(
                touches[0].CurrentX, touches[0].CurrentY,
                touches[1].CurrentX, touches[1].CurrentY);
            
            var scale = currentDistance / initialDistance;
            OnPinch?.Invoke(scale);
        }
    }

    public void HandleTouchEnd(TouchEventArgs e)
    {
        foreach (var touch in e.ChangedTouches)
        {
            if (_activeTouches.TryGetValue(touch.Identifier, out var touchPoint))
            {
                // Calculate swipe
                var deltaX = touchPoint.CurrentX - touchPoint.StartX;
                var deltaY = touchPoint.CurrentY - touchPoint.StartY;
                var duration = (DateTime.UtcNow - touchPoint.StartTime).TotalMilliseconds;
                
                // Velocity-based swipe detection
                var velocityX = Math.Abs(deltaX / duration);
                var velocityY = Math.Abs(deltaY / duration);
                const double minVelocity = 0.3; // pixels per ms
                const double minDistance = 50; // pixels

                if (Math.Abs(deltaX) > minDistance && velocityX > minVelocity)
                {
                    OnSwipe?.Invoke(deltaX > 0 ? SwipeDirection.Right : SwipeDirection.Left);
                }
                else if (Math.Abs(deltaY) > minDistance && velocityY > minVelocity)
                {
                    OnSwipe?.Invoke(deltaY > 0 ? SwipeDirection.Down : SwipeDirection.Up);
                }

                _activeTouches.Remove(touch.Identifier);
            }
        }
    }

    private double CalculateDistance(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
    }

    private class TouchPoint
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double CurrentX { get; set; }
        public double CurrentY { get; set; }
        public DateTime StartTime { get; set; }
    }

    private record struct Point(double X, double Y);
}

public enum SwipeDirection
{
    Up, Down, Left, Right
}
```

### Blazor Component Integration

```razor
@implements IDisposable

<div @ontouchstart="HandleTouchStart"
     @ontouchmove="HandleTouchMove"
     @ontouchend="HandleTouchEnd"
     @ontouchstart:preventDefault="true"
     @ontouchmove:preventDefault="true"
     class="gesture-area">
    @ChildContent
</div>

@code {
    private readonly GestureRecognizer _gestureRecognizer = new();
    
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback<SwipeDirection> OnSwipe { get; set; }
    [Parameter] public EventCallback<double> OnPinch { get; set; }
    [Parameter] public EventCallback OnDoubleTap { get; set; }

    protected override void OnInitialized()
    {
        _gestureRecognizer.OnSwipe += HandleSwipe;
        _gestureRecognizer.OnPinch += HandlePinch;
        _gestureRecognizer.OnDoubleTap += HandleDoubleTap;
    }

    private void HandleTouchStart(TouchEventArgs e) => 
        _gestureRecognizer.HandleTouchStart(e);
    
    private void HandleTouchMove(TouchEventArgs e) => 
        _gestureRecognizer.HandleTouchMove(e);
    
    private void HandleTouchEnd(TouchEventArgs e) => 
        _gestureRecognizer.HandleTouchEnd(e);

    private void HandleSwipe(SwipeDirection direction) => 
        InvokeAsync(() => OnSwipe.InvokeAsync(direction));
    
    private void HandlePinch(double scale) => 
        InvokeAsync(() => OnPinch.InvokeAsync(scale));
    
    private void HandleDoubleTap() => 
        InvokeAsync(() => OnDoubleTap.InvokeAsync());

    public void Dispose()
    {
        _gestureRecognizer.OnSwipe -= HandleSwipe;
        _gestureRecognizer.OnPinch -= HandlePinch;
        _gestureRecognizer.OnDoubleTap -= HandleDoubleTap;
    }
}
```

### Advanced Gesture Patterns with Pattern Matching

```csharp
public class AdvancedGestureRecognizer : GestureRecognizer
{
    // Pattern matching for complex gestures
    public GesturePattern RecognizePattern(List<TouchPoint> points)
    {
        return points switch
        {
            // Circle detection using angle progression
            var p when IsCircularMotion(p) => GesturePattern.Circle,
            
            // Long press detection
            var p when p.Count == 1 && IsLongPress(p[0]) => GesturePattern.LongPress,
            
            // Three-finger swipe
            var p when p.Count == 3 && IsMultiFingerSwipe(p) => GesturePattern.ThreeFingerSwipe,
            
            // Default to unknown
            _ => GesturePattern.Unknown
        };
    }

    private bool IsCircularMotion(List<TouchPoint> points)
    {
        if (points.Count < 8) return false;
        
        var angles = new List<double>();
        for (int i = 1; i < points.Count; i++)
        {
            var angle = Math.Atan2(
                points[i].Y - points[i-1].Y,
                points[i].X - points[i-1].X);
            angles.Add(angle);
        }
        
        // Check if angles progress consistently (circular motion)
        var totalRotation = angles.Sum();
        return Math.Abs(totalRotation) > Math.PI * 1.5; // At least 3/4 circle
    }

    private bool IsLongPress(TouchPoint point)
    {
        var duration = (DateTime.UtcNow - point.StartTime).TotalMilliseconds;
        var movement = Math.Sqrt(
            Math.Pow(point.CurrentX - point.StartX, 2) +
            Math.Pow(point.CurrentY - point.StartY, 2));
        
        return duration > 500 && movement < 10; // 500ms with minimal movement
    }

    private bool IsMultiFingerSwipe(List<TouchPoint> points)
    {
        // Check if all fingers moved in same direction
        var directions = points.Select(p => new
        {
            DeltaX = p.CurrentX - p.StartX,
            DeltaY = p.CurrentY - p.StartY
        }).ToList();
        
        // All fingers should move in similar direction
        var avgDeltaX = directions.Average(d => d.DeltaX);
        var avgDeltaY = directions.Average(d => d.DeltaY);
        
        return directions.All(d =>
            Math.Sign(d.DeltaX) == Math.Sign(avgDeltaX) &&
            Math.Sign(d.DeltaY) == Math.Sign(avgDeltaY));
    }
}

public enum GesturePattern
{
    Unknown,
    Circle,
    LongPress,
    ThreeFingerSwipe
}
```

## Key Benefits

1. **Zero JavaScript Dependencies**: All gesture logic in C#
2. **Type Safety**: Full IntelliSense and compile-time checking
3. **Testability**: Pure C# logic can be unit tested
4. **Performance**: No JS interop overhead for gesture calculations
5. **Maintainability**: Single language, consistent patterns
6. **Debugging**: Full C# debugging experience

## Testing Strategy

### C# Unit Testing with xUnit and bUnit

Since our approach is purely Blazor-native, we can leverage standard C# testing tools without JavaScript test runners:

#### Testing Gesture Recognizer Logic

```csharp
[Fact]
public void GestureRecognizer_DetectsLongPress()
{
    // Arrange
    var recognizer = new GestureRecognizer();
    var longPressDetected = false;
    recognizer.OnLongPress = () => longPressDetected = true;
    
    // Act - Simulate touch start
    recognizer.HandleTouchStart(100, 200);
    
    // Simulate 500ms delay (mock timer callback)
    recognizer.TriggerLongPressTimer();
    
    // Assert
    Assert.True(longPressDetected);
    Assert.True(recognizer.IsLongPressing);
}

[Theory]
[InlineData(100, 200, 150, 250, true)]  // Within threshold
[InlineData(100, 200, 200, 300, false)] // Outside threshold
public void GestureRecognizer_DetectsMovementThreshold(
    double startX, double startY, 
    double endX, double endY, 
    bool shouldBeLongPress)
{
    // Arrange
    var recognizer = new GestureRecognizer();
    
    // Act
    recognizer.HandleTouchStart(startX, startY);
    recognizer.HandleTouchMove(endX, endY);
    
    // Assert
    Assert.Equal(shouldBeLongPress, recognizer.IsWithinMoveThreshold());
}
```

#### Testing Blazor Components with bUnit

```csharp
[Fact]
public void ImageComponent_HandlesLongPressGesture()
{
    // Arrange
    using var ctx = new TestContext();
    var longPressTriggered = false;
    
    var component = ctx.RenderComponent<ImageGalleryItem>(parameters => parameters
        .Add(p => p.ImageUrl, "test.jpg")
        .Add(p => p.OnLongPress, EventCallback.Factory.Create(this, 
            () => longPressTriggered = true)));
    
    // Act - Simulate pointer events
    var imageElement = component.Find("img");
    imageElement.PointerDown(new PointerEventArgs 
    { 
        ClientX = 100, 
        ClientY = 200,
        PointerId = 1,
        PointerType = "touch"
    });
    
    // Simulate timer completion
    component.InvokeAsync(() => component.Instance.CompleteLongPress());
    
    // Assert
    Assert.True(longPressTriggered);
}

[Fact]
public void ImageComponent_CancelsLongPressOnMove()
{
    // Arrange
    using var ctx = new TestContext();
    var component = ctx.RenderComponent<ImageGalleryItem>();
    
    // Act
    var imageElement = component.Find("img");
    imageElement.PointerDown(new PointerEventArgs { ClientX = 100, ClientY = 200 });
    imageElement.PointerMove(new PointerEventArgs { ClientX = 200, ClientY = 300 });
    
    // Assert
    Assert.False(component.Instance.IsLongPressing);
}
```

#### Mocking Minimal JavaScript Interop

For the minimal haptic feedback JS interop:

```csharp
[Fact]
public async Task HapticService_CallsJavaScriptWhenAvailable()
{
    // Arrange
    var jsRuntime = new Mock<IJSRuntime>();
    jsRuntime.Setup(x => x.InvokeAsync<bool>(
        "navigator.vibrate", 
        It.IsAny<object[]>()))
        .ReturnsAsync(true);
    
    var hapticService = new HapticFeedbackService(jsRuntime.Object);
    
    // Act
    await hapticService.TriggerHapticAsync(10);
    
    // Assert
    jsRuntime.Verify(x => x.InvokeAsync<bool>(
        "navigator.vibrate",
        It.Is<object[]>(args => (int)args[0] == 10)),
        Times.Once);
}

[Fact]
public async Task HapticService_HandlesJavaScriptErrors()
{
    // Arrange
    var jsRuntime = new Mock<IJSRuntime>();
    jsRuntime.Setup(x => x.InvokeAsync<bool>(
        It.IsAny<string>(), 
        It.IsAny<object[]>()))
        .ThrowsAsync(new JSException("Not supported"));
    
    var hapticService = new HapticFeedbackService(jsRuntime.Object);
    
    // Act & Assert - Should not throw
    await hapticService.TriggerHapticAsync(10);
}
```

### Test Coverage Areas

1. **Pure C# Logic Testing** (90% of tests)
   - Gesture state machines
   - Timer management
   - Touch coordinate calculations
   - Event callback chains

2. **Component Integration Testing** (9% of tests)
   - Blazor event handling
   - Component parameter binding
   - State management
   - EventCallback propagation

3. **Minimal JS Interop Testing** (1% of tests)
   - Haptic feedback invocation
   - Error handling for missing APIs
   - Fallback behavior

### Benefits of C# Testing Approach

- **No JavaScript test runners needed** - Just xUnit
- **Full debugging support** - Breakpoints in tests
- **Strong typing** - Compile-time test verification
- **Fast execution** - No browser automation
- **CI/CD friendly** - Standard .NET test pipeline

## Next Steps

- Implement accelerometer access patterns
- Add camera integration patterns
- Create file system access patterns
- Document WebAssembly-specific optimizations