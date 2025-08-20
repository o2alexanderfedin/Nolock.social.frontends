# Single Responsibility Principle (SRP) Violations

## Critical Violations (Immediate Refactoring Required)

### 1. CameraService.cs (1035 lines)
**Location:** `NoLock.Social.Core/Camera/Services/CameraService.cs`
**Responsibilities Identified:** 7+ distinct responsibilities
- Camera hardware management
- Permission handling
- Session management
- Offline storage coordination
- Queue processing
- State management

**Recommended Refactoring:**
- Extract `CameraPermissionManager` for permission logic
- Extract `CameraSessionManager` for session lifecycle
- Extract `CameraStreamManager` for stream operations
- Use mediator pattern for component coordination

### 2. DocumentProcessingQueue.cs (636 lines)
**Location:** `NoLock.Social.Core/OCR/Services/DocumentProcessingQueue.cs`
**Responsibilities Identified:** 5+ distinct responsibilities
- Queue management
- Retry logic
- Persistence
- Processing coordination
- Status tracking

**Recommended Refactoring:**
- Extract `QueuePersistenceManager`
- Extract `RetryPolicyManager`
- Extract `ProcessingCoordinator`
- Implement Chain of Responsibility for processing pipeline

### 3. DocumentProcessorRegistry.cs (596 lines)
**Location:** `NoLock.Social.Core/OCR/Services/DocumentProcessorRegistry.cs`
**Responsibilities Identified:** 4+ distinct responsibilities
- Processor registration
- Type detection
- Processing dispatch
- Plugin management

**Recommended Refactoring:**
- Extract `ProcessorFactory`
- Extract `DocumentTypeResolver`
- Extract `PluginManager`
- Use Strategy pattern for processor selection

## Moderate Violations (Scheduled Refactoring)

### 4. FieldValidationService.cs (573 lines)
**Multiple validation types in single class**
- Should be split into domain-specific validators
- Use Composite pattern for complex validations

### 5. ReactiveSessionStateService.cs (573 lines)
**Mixed concerns of state management and reactivity**
- Extract reactive notification system
- Separate state persistence from state changes


## Key Violations

### God Objects:
1. **CameraService** (1035 lines) - Does camera, permissions, sessions, storage, queue, enhancements
2. **DocumentProcessingQueue** (636 lines) - Queue + retry + persistence + coordination + status
3. **DocumentProcessorRegistry** (596 lines) - Registration + detection + dispatch + plugins

## Metrics
- **Files >300 lines:** 19
- **Largest:** CameraService.cs (1035 lines)
- **DI Violations:** 59 (direct `new` calls)

## Action Items
1. **CameraService** - Split into CameraManager, PermissionService, SessionService
2. **DocumentProcessingQueue** - Extract RetryManager and PersistenceManager
3. **Test Files** - Use parameterized tests (many 600+ line test files)