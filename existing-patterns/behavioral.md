# Existing Patterns in Core

## Working Patterns

### Factory Pattern ✅
**Location:** `OCR/Services/RetryableOperation.cs`
- Creates retryable operations with DI
- Solves: Consistent retry logic

**Usage Example:**
```csharp
// OCRServiceWithRetry.cs uses factory to create operations
var operation = _operationFactory.CreateAsyncOperation<OCRResult>(
    operation: () => SubmitAsync(),
    operationName: "OCR submission"
);
```

### Observer Pattern ✅
**Location:** ReactiveSessionStateService, LoginAdapterService
- Uses Rx.NET observables
- Solves: Reactive UI updates

**Key Observable Streams Found:**
1. **Session State Management:**
   - `IObservable<SessionStateChangedEventArgs> SessionStateChanges`
   - `IObservable<SessionState> StateStream`
   - `IObservable<TimeSpan> RemainingTimeStream`
   - `IObservable<TimeSpan> TimeoutWarningStream`

2. **Login State Management:**
   - `IObservable<LoginStateChange> LoginStateChanges`

3. **Document Processing Queue:**
   - Traditional events (DocumentQueued, ProcessingStatusChanged, ProcessingCompleted)
   - Mixed pattern: Events + Observable pattern

**Reactive Extensions Usage:**
- Custom operators in `Cryptography/Extensions/ReactiveExtensions.cs`
- `WhenUnlocked()` - Waits for session unlock
- `BufferUntilUnlocked()` - Buffers events until session ready
- `RetryWithBackoff()` - Exponential backoff retry
- `ThrottleProgress()` - Progress event throttling
- `CombineProgress()` - Multi-source progress aggregation

### Event Pattern ✅
**Locations:** OCR, Session services
- Standard .NET events
- Mixed with observables (inconsistent)

**Event Usage:**
1. **WakeLockService:**
   - `VisibilityChanged` - Page visibility monitoring
   - `WakeLockStatusChanged` - Wake lock state changes

2. **DocumentProcessingQueue:**
   - `DocumentQueued`, `ProcessingStatusChanged`, `ProcessingCompleted`
   - `QueueStateChanged` - Queue state notifications

3. **OCRRetryQueueProcessor:**
   - `ProcessingStarted`, `ProcessingCompleted`
   - `RequestSucceeded`, `RequestFailed`

## Not Found (Good - YAGNI)
- **Repository**: Direct service access simpler
- **Singleton**: DI container handles lifecycle

## Emerging Patterns

### 3.1 Reactive Programming Pattern
**Evolution Stage:** Mature adoption
- Heavy use of System.Reactive for async event streams
- Custom reactive operators for domain-specific needs
- Mixed with traditional events for backward compatibility

### 3.2 Retry/Resilience Pattern
**Evolution Stage:** Problem-specific implementation
- RetryableOperationFactory for consistent retry logic
- Exponential backoff strategies
- Offline queue processing for network failures

### 3.3 Progressive Enhancement Pattern
**Evolution Stage:** Emerging
- Wake lock service for preventing device sleep
- Visibility monitoring for page state
- Offline-first capabilities with retry queues

## Problems
- **Mixed Events**: Some use EventHandler, others IObservable
- **One Factory**: Other services need factories too

## Actions
- **Keep**: RetryableOperationFactory, Rx.NET usage
- **Fix**: Standardize on observables (not mixed events)
- **Add**: Strategy for document types, Circuit breaker for OCR

