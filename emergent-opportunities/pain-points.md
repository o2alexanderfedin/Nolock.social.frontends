# Component Pain Points

## Current Issues
- **30+ EventCallbacks** across components (no central hub)
- **30+ StateHasChanged** calls (manual state sync)
- **10+ components** with identical init code
- **5-6 services** injected per component

## Pain Points Identified

### 1. **Repeated Component Initialization Code** ⚠️ Factory Opportunity
**Pain:** Every component has similar initialization boilerplate
```csharp
// Repeated in DocumentCaptureContainer, CameraPreview, etc.
protected override async Task OnInitializedAsync()
{
    // Subscribe to events
    Service.Event += Handler;
    
    // Initialize voice commands
    await InitializeVoiceCommandsAsync();
    
    // Setup timers
    _timer = new Timer(1000);
    
    // Announce initial state
    await AnnouncementService.AnnouncePoliteAsync(...);
}
```
**Emergent Solution:** Component Factory pattern would naturally emerge from refactoring this duplication

### 2. **Complex Conditional Logic in OCR Components** ⚠️ Strategy Opportunity
**Pain:** Switch statements for confidence levels, field types, document types
```csharp
// Multiple switch patterns in OCR components
return level switch
{
    ConfidenceLevel.High => "green",
    ConfidenceLevel.Medium => "yellow",
    ConfidenceLevel.Low => "red",
    _ => "gray"
};
```
**Emergent Solution:** Strategy pattern would naturally replace these switches

### 3. **Event Handling Inconsistency** ⚠️ Observer/Mediator Opportunity
**Pain:** Mixed patterns for event handling
- Direct event subscriptions with += operators
- EventCallback parameters
- Manual cleanup in Dispose()
- No centralized event management

**Examples:**
```csharp
// Direct subscription (IdentityStatusComponent)
SessionStateService.SessionStateChanged += OnSessionStateChanged;

// EventCallback parameter (30+ components)
[Parameter] public EventCallback<bool> OnVerificationComplete { get; set; }

// Manual cleanup required
public void Dispose()
{
    SessionStateService.SessionStateChanged -= OnSessionStateChanged;
    _timer?.Dispose();
}
```
**Emergent Solution:** Event Aggregator or Mediator would reduce coupling

### 4. **Service Dependencies Explosion** ⚠️ Facade Opportunity
**Pain:** Components with 5+ injected services
```csharp
// DocumentCaptureContainer has 6 injected services
@inject ICameraService CameraService
@inject ILogger<DocumentCaptureContainer> Logger
@inject IFocusManagementService FocusManagementService
@inject IAnnouncementService AnnouncementService
@inject IVoiceCommandService VoiceCommandService
```
**Emergent Solution:** Service Facade would naturally group related services

### 5. **Manual State Synchronization** ⚠️ Reactive Pattern Opportunity
**Pain:** Manual StateHasChanged calls everywhere
- InvokeAsync(StateHasChanged) in event handlers
- StateHasChanged after every state mutation
- No automatic change detection

**Emergent Solution:** Reactive state management (Observable pattern) would eliminate manual updates

## Specific Emergent Opportunities

### 1. **Component Factory Pattern** (HIGH PRIORITY)
**Where code changes together:**
- All camera components share initialization logic
- All OCR components share validation setup
- All identity components share session monitoring

**Natural refactoring would create:**
```csharp
public interface IComponentFactory<T> where T : ComponentBase
{
    T CreateWithStandardInitialization(ComponentParameters params);
}
```

### 2. **Confidence Level Strategy** (MEDIUM PRIORITY)
**Current pain in OCR components:**
- Repeated switch statements for confidence display
- Duplicated color/icon logic
- Hard to add new confidence levels

**Natural emergence:**
```csharp
public interface IConfidenceDisplayStrategy
{
    string GetCssClass(ConfidenceLevel level);
    string GetIcon(ConfidenceLevel level);
    string GetMessage(ConfidenceLevel level);
}
```

### 3. **Event Aggregator Pattern** (HIGH PRIORITY)
**Current coupling pain:**
- Components directly depend on specific services for events
- Memory leaks from forgotten unsubscriptions
- Testing difficulty due to event dependencies

**Natural emergence from refactoring:**
```csharp
public interface IEventAggregator
{
    void Subscribe<TEvent>(Action<TEvent> handler);
    void Publish<TEvent>(TEvent eventData);
}
```

### 4. **Camera Service Facade** (MEDIUM PRIORITY)
**Current pain:**
- Components inject 4-6 camera-related services
- Repeated service coordination code
- Complex initialization sequences

**Would naturally emerge as:**
```csharp
public interface ICameraComponentServices
{
    ICameraService Camera { get; }
    IVoiceCommandService VoiceCommands { get; }
    IAnnouncementService Announcements { get; }
    IImageEnhancementService Enhancement { get; }
}
```

### 5. **Reactive State Container** (LOW PRIORITY - Framework Level)
**Current pain:**
- Manual StateHasChanged everywhere
- Race conditions with async state updates
- No centralized state management

**Natural evolution would be:**
- Adopt Fluxor or similar for Blazor
- Or create lightweight Observable wrapper

## Fix These First
1. **Component Factory** - 10+ duplicate inits
2. **Event Aggregator** - Replace 30+ callbacks
3. **Service Facades** - Group 5-6 services into 1