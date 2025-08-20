# DRY Violations

## Remaining Issues (Not Yet Fixed)

### 1. Try-Catch-Log Pattern
```csharp
// Found in 86 locations
try {
    // operation
} catch (Exception ex) {
    _logger.LogError(ex, "Operation failed");
    throw;
}
```
**Potential Solution**: Consider Result<T> pattern or aspect-oriented logging

### 2. Database Access Code
**Files with IndexedDB repetition:**
- Multiple services contain identical IndexedDB setup code
- Repeated store name strings: 14 occurrences
- Database version strings: 7 occurrences

### 3. Voice Command Configuration
**Problem**: Command mappings duplicated across files
- Same command strings in tests and production
- Repeated command handler patterns

### 4. OCR Processing Logic
**Duplication in:**
- `CheckProcessor.cs`
- `ReceiptProcessor.cs`
- Similar validation and extraction logic

### 5. Connectivity Checking
**Files with duplicate connectivity logic:**
- `ConnectivityService.cs`
- `SyncService.cs`
- `OfflineQueueService.cs`

### 6. Logging Patterns
**Repeated logging code:**
- Error logging: 86 instances
- Info logging with similar messages
- Performance logging patterns


## ✅ FIXED Issues

### Previously Fixed:
1. **Validation Messages** - Now using `ValidationMessages` class
   - "Session ID cannot be null or empty" (8 occurrences) → `ValidationMessages.SessionIdRequired`
   - "Session '{sessionId}' not found" (6 occurrences) → `ValidationMessages.SessionNotFound`

2. **Null Guard Patterns** - Now using `Guard` utility class
   - Replaced 17+ occurrences of `?? throw new ArgumentNullException()`
   - Now using `Guard.AgainstNull()`, `Guard.AgainstNullOrEmpty()`

3. **Parameter Names** - Correctly using `nameof()`
   - "cancellationToken", "logger", "fieldName", "documentType" are NOT violations
   - These are proper uses of `nameof(parameter)` for compile-time safety
   - Each occurrence refers to its local parameter - this is the correct pattern