# Build Fix Priority List

## Analysis Summary - Updated After .NET 9 Migration
- **Total Errors**: 27 (NEW - blocking compilation)
- **Total Warnings**: 106  
- **Build Status**: FAILED
- **Framework**: .NET 9.0 (upgraded from 8.0)

## CRITICAL BUILD ERRORS (Must Fix to Compile)

### Priority 1: Missing Using Directive (1 error) - QUICK FIX
**File**: `NoLock.Social.Core.Tests/Cryptography/VerificationServiceTests.cs`
- **Line 313**: Missing `using System.Text;` for StringBuilder
- **Fix**: Add using statement at top of file
- **Time**: 1 minute

### Priority 2: CS0854 - Expression Tree Optional Parameters (26 errors)
**File**: `NoLock.Social.Core.Tests/Storage/TypedContentAddressableStorageTests.cs`
- **Pattern**: All Moq `Setup()` calls with optional parameters
- **Root Cause**: .NET 9 breaking change - stricter expression tree rules
- **Lines**: 36, 44, 62, 73, 82, 89, 108, 115, 133, 140, 158, 170, 179, 186, 204, 211, 218, 224, 246
- **Fix Options**:
  1. Explicitly specify all parameters in Setup() calls
  2. Use `It.IsAny<T>()` for optional parameters  
  3. Update Moq to latest version
- **Time**: 10-15 minutes

## Previous Issues (Now Lower Priority - Warnings Only)

### High Priority Warnings (Runtime Risk)
1. **CS8625 - Null to Non-Nullable Conversions (10+ warnings)**
   - Files: OCRServiceOptionsTests.cs, ConfidenceScoreServiceTests.cs
   - Impact: Potential NullReferenceException at runtime
   
2. **CS8618 - Uninitialized Non-Nullable Properties**
   - File: PollingServiceTests.cs (Message property)
   - Impact: Properties may be null despite non-nullable declaration

### Low Priority (Cleanup)
3. **CS0219 - Unused Variables (2 warnings)**
   - Files: SessionStateServiceTests.cs, SigningServiceTests.cs
   
4. **xUnit1026 - Unused Test Parameter (1 warning)**
   - File: ConfidenceScoreServiceTests.cs

## Root Cause Analysis

### .NET 9 Migration Breaking Changes
1. **Expression Trees**: .NET 9 enforces stricter rules for optional parameters in expression trees
2. **Moq Compatibility**: Current Moq version may not be fully compatible with .NET 9
3. **Missing Namespaces**: System.Text not automatically included in some contexts

## Fix Sequence (Baby Steps)
1. **Step 1** (1 min): Add `using System.Text;` to VerificationServiceTests.cs
2. **Step 2** (15 min): Fix CS0854 errors in TypedContentAddressableStorageTests.cs
3. **Step 3** (2 min): Rebuild and verify compilation succeeds
4. **Step 4**: Run tests to check for runtime issues
5. **Step 5**: Address warnings if time permits