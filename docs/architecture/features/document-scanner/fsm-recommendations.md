# Finite State Machine (FSM) Library Recommendations for OCR Architecture

## Identified State Machines in Current Architecture

### 1. Scanner State Machine
- **States**: Idle, RequestingPermission, CameraActive, ImageCaptured, Processing, ResultsReady, Error
- **Use Case**: Managing the document scanning workflow

### 2. OCR Processing State Machine  
- **States**: Submitted, Polling, CheckStatus, Completed, Failed, Timeout
- **Use Case**: Managing async OCR API polling workflow

### 3. Document Processing Pipeline
- **States**: Queued, Processing, Validated, Stored, Signed, Complete, Failed
- **Use Case**: Document processing workflow

## Recommended NuGet Packages for Blazor WebAssembly

### 1. **Stateless** (RECOMMENDED)
```xml
<PackageReference Include="Stateless" Version="5.15.0" />
```

**Pros:**
- ✅ Lightweight (perfect for Blazor WASM)
- ✅ No dependencies
- ✅ Fluent API for configuration
- ✅ Supports async transitions
- ✅ Built-in state machine visualization (DOT graph export)
- ✅ Thread-safe
- ✅ Well-maintained (6.8k GitHub stars)

**Example Usage:**
```csharp
public class ScannerStateMachine
{
    private readonly StateMachine<ScannerState, ScannerTrigger> _machine;
    
    public ScannerStateMachine()
    {
        _machine = new StateMachine<ScannerState, ScannerTrigger>(ScannerState.Idle);
        
        _machine.Configure(ScannerState.Idle)
            .Permit(ScannerTrigger.StartScanning, ScannerState.RequestingPermission)
            .OnEntry(() => Console.WriteLine("Scanner ready"));
            
        _machine.Configure(ScannerState.RequestingPermission)
            .PermitIf(ScannerTrigger.PermissionGranted, ScannerState.CameraActive, 
                      () => HasCameraPermission())
            .Permit(ScannerTrigger.PermissionDenied, ScannerState.Error);
            
        _machine.Configure(ScannerState.Processing)
            .OnEntryAsync(async () => await SubmitToOcrApi())
            .Permit(ScannerTrigger.ProcessingComplete, ScannerState.ResultsReady)
            .Permit(ScannerTrigger.ProcessingFailed, ScannerState.Error);
    }
}
```

### 2. **Appccelerate.StateMachine**
```xml
<PackageReference Include="Appccelerate.StateMachine" Version="5.1.0" />
```

**Pros:**
- ✅ Hierarchical state machines
- ✅ Async support
- ✅ Built-in persistence
- ✅ Good for complex workflows

**Cons:**
- ❌ Heavier than Stateless
- ❌ More complex API

### 3. **NStateManager**
```xml
<PackageReference Include="NStateManager" Version="4.0.1" />
```

**Pros:**
- ✅ Simple and lightweight
- ✅ Good for basic state machines
- ✅ Minimal dependencies

**Cons:**
- ❌ Less features than Stateless
- ❌ Smaller community

## Integration Points in OCR Architecture

### 1. Replace Scanner State Enum with Stateless FSM
```csharp
// BEFORE (Current approach)
public enum ScannerState { Idle, RequestingPermission, ... }

// AFTER (With Stateless)
public class DocumentScannerStateMachine
{
    private readonly StateMachine<State, Trigger> _fsm;
    private readonly ILogger<DocumentScannerStateMachine> _logger;
    private readonly IOcrService _ocrService;
    
    public DocumentScannerStateMachine(IOcrService ocrService, ILogger<...> logger)
    {
        _fsm = new StateMachine<State, Trigger>(State.Idle);
        ConfigureStateMachine();
    }
    
    private void ConfigureStateMachine()
    {
        _fsm.Configure(State.Idle)
            .Permit(Trigger.StartCapture, State.RequestingPermission)
            .OnEntry(() => _logger.LogInformation("Scanner ready"));
            
        _fsm.Configure(State.Processing)
            .OnEntryAsync(async () => await ProcessWithOcrAsync())
            .Permit(Trigger.Success, State.ResultsReady)
            .Permit(Trigger.Failure, State.Error)
            .OnExit(() => _logger.LogInformation("Processing complete"));
    }
    
    public async Task<bool> FireAsync(Trigger trigger)
    {
        if (_fsm.CanFire(trigger))
        {
            await _fsm.FireAsync(trigger);
            return true;
        }
        return false;
    }
    
    public string ExportToDotGraph() => UmlDotGraph.Format(_fsm.GetInfo());
}
```

### 2. OCR Polling State Machine
```csharp
public class OcrPollingStateMachine
{
    private readonly StateMachine<PollingState, PollingTrigger> _fsm;
    private readonly StateMachine<PollingState, PollingTrigger>.TriggerWithParameters<int> _retryTrigger;
    private int _retryCount = 0;
    
    public OcrPollingStateMachine()
    {
        _fsm = new StateMachine<PollingState, PollingTrigger>(PollingState.Submitted);
        _retryTrigger = _fsm.SetTriggerParameters<int>(PollingTrigger.Retry);
        
        _fsm.Configure(PollingState.Polling)
            .PermitReentry(PollingTrigger.ContinuePolling)
            .Permit(PollingTrigger.Success, PollingState.Completed)
            .Permit(PollingTrigger.Failure, PollingState.Failed)
            .PermitIf(_retryTrigger, PollingState.Polling, 
                      count => count < 10, "Max retries not exceeded")
            .OnEntryFromAsync(_retryTrigger, async count =>
            {
                _retryCount = count;
                await Task.Delay(GetBackoffDelay(count));
            });
    }
    
    private TimeSpan GetBackoffDelay(int attempt) => attempt switch
    {
        <= 2 => TimeSpan.FromSeconds(5),
        <= 4 => TimeSpan.FromSeconds(10),
        <= 6 => TimeSpan.FromSeconds(15),
        _ => TimeSpan.FromSeconds(30)
    };
}
```

### 3. Benefits of Using Stateless

1. **KISS**: Simple, declarative state machine configuration
2. **DRY**: Reusable state machine patterns
3. **YAGNI**: Use only the features you need
4. **SOLID**: Clean separation of state logic from business logic
5. **TRIZ**: Use proven FSM library instead of custom implementation

### 4. Migration Strategy

1. **Phase 1**: Add Stateless NuGet package
2. **Phase 2**: Create FSM wrappers for existing state enums
3. **Phase 3**: Gradually migrate state logic to FSM
4. **Phase 4**: Remove custom state management code
5. **Phase 5**: Export state diagrams for documentation

## Recommendation

**Use Stateless** for all state machines in the OCR architecture because:
- Lightweight for Blazor WASM (no heavy dependencies)
- Async support for OCR API polling
- Can export state diagrams (replace manual Mermaid diagrams)
- Well-maintained and battle-tested
- Follows KISS principle

## Code to Remove from Architecture Document

With a proper FSM library, remove:
- Custom state management implementations
- Manual state transition logic
- State validation code
- Custom workflow orchestration

Replace with:
- Simple FSM configuration
- State machine service registration in DI
- Reference to Stateless documentation