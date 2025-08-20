---
description: Comprehensive code quality analysis to identify anti-patterns, code smells, and improvement opportunities
argument-hint: [optional: specific area to focus on]
allowed-tools: [Grep, Glob, Read, LS, Bash]
---

# Code Quality Analysis

## Intent
Perform a comprehensive analysis of your codebase to identify anti-patterns, code smells, technical debt, and improvement opportunities - exactly like the analysis that originally found the 86 try-catch-log patterns.

## What This Does For You
- **Identifies anti-patterns** - Finds repetitive code patterns that should be refactored
- **Detects code smells** - Locates problematic code structures
- **Measures technical debt** - Quantifies TODO/FIXME markers and deprecated code
- **Evaluates complexity** - Identifies overly complex methods and classes
- **Suggests improvements** - Provides actionable recommendations with priority levels
- **Tracks progress** - Compares against previous analyses to show improvement

## Usage Examples
```
/analyze
/analyze Focus on error handling patterns
/analyze Check the OCR services for complexity
/analyze Review security and authentication code
```

## Analysis Categories

### 1. Anti-Patterns Detection
- **God Objects**: Classes with too many responsibilities (>15 public methods)
- **Long Methods**: Methods exceeding 50 lines
- **Deep Nesting**: Code nested more than 4 levels deep
- **Primitive Obsession**: Overuse of primitives instead of value objects
- **Feature Envy**: Methods that use another class's data excessively

### 2. Code Smells
- **Magic Numbers**: Hard-coded numeric literals without named constants
- **Duplicate Code**: Similar code blocks that should be extracted
- **Dead Code**: Unreachable or commented-out code
- **Large Classes**: Classes with too many lines (>500)
- **Long Parameter Lists**: Methods with more than 4 parameters
- **Circular Dependencies**: Classes that depend on each other

### 3. SOLID Violations
- **Single Responsibility**: Classes doing too many things
- **Open/Closed**: Hard-coded switches instead of polymorphism
- **Liskov Substitution**: Inheritance hierarchies that break substitutability
- **Interface Segregation**: Fat interfaces with too many methods
- **Dependency Inversion**: Direct dependencies instead of abstractions

### 4. Async/Await Issues
- **async void**: Fire-and-forget methods that can't be awaited
- **.Result/.Wait()**: Blocking async calls that can cause deadlocks
- **Missing ConfigureAwait**: Context capture in library code
- **Sync over async**: Synchronous wrappers over async methods

### 5. Security Concerns
- **Hardcoded Secrets**: API keys or passwords in code
- **SQL Injection**: String concatenation in queries
- **Missing Input Validation**: Unvalidated user input
- **Insecure Randomness**: Using Random instead of cryptographic RNG

### 6. Performance Issues
- **N+1 Queries**: Database queries in loops
- **Memory Leaks**: Missing disposal of IDisposable
- **Inefficient Collections**: Wrong collection types for use case
- **Excessive Allocations**: Creating too many temporary objects

### 7. Testing Gaps
- **Missing Tests**: Code without corresponding tests
- **Test Smells**: Tests with multiple assertions, no arrange-act-assert
- **Fragile Tests**: Tests dependent on execution order
- **Slow Tests**: Tests that take too long to run

### 8. Documentation Debt
- **Missing XML Comments**: Public APIs without documentation
- **Outdated Comments**: Comments that don't match code
- **TODO/FIXME/HACK**: Technical debt markers
- **Unclear Naming**: Methods/variables that need comments to understand

## Analysis Process

I will:

1. **Scan the entire codebase** systematically
2. **Identify patterns** using advanced pattern matching
3. **Count occurrences** of each issue type
4. **Analyze severity** based on impact and frequency
5. **Generate examples** showing actual code locations
6. **Provide solutions** with specific refactoring suggestions
7. **Create priority list** based on impact vs effort
8. **Generate report** with actionable recommendations

## Output Format

### Summary Dashboard
```
═══════════════════════════════════════════
    CODE QUALITY ANALYSIS RESULTS
═══════════════════════════════════════════
  
  Critical Issues:    [count]
  Major Issues:       [count]
  Minor Issues:       [count]
  
  Code Health Score:  [A-F grade]
  Technical Debt:     [hours estimate]
═══════════════════════════════════════════
```

### Detailed Findings
For each issue type:
- **Pattern Name**: What was found
- **Severity**: Critical/Major/Minor
- **Count**: Number of occurrences
- **Locations**: Specific files and line numbers (top 5)
- **Example**: Code snippet showing the issue
- **Impact**: Why this matters
- **Solution**: How to fix it
- **Effort**: Estimated time to fix

### Priority Matrix
```
High Impact + Low Effort = DO FIRST
High Impact + High Effort = PLAN
Low Impact + Low Effort = QUICK WINS
Low Impact + High Effort = SKIP/DEFER
```

### Trend Analysis
If previous analysis exists:
- **Improvements**: Issues that were fixed
- **Regressions**: New issues introduced
- **Persistent**: Issues that remain
- **Progress**: Overall trend direction

## Example Output

```
CRITICAL: Try-Catch-Log Anti-Pattern
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Found: 86 occurrences
Severity: HIGH
Technical Debt: ~8 hours

Top Locations:
• CameraService.cs (8 patterns)
• SessionStateService.cs (6 patterns)
• OCRService.cs (5 patterns)

Example:
  try {
      await SomeOperation();
  } catch (Exception ex) {
      _logger.LogError(ex, "Failed");
      throw;
  }

Solution: Implement Result<T> pattern with ExecuteWithLogging
Effort: 5-10 minutes per pattern
```

## Interactive Mode

After analysis, I can:
- **Deep dive** into specific issues
- **Generate fix scripts** for automated refactoring
- **Create tasks** for issue resolution
- **Estimate timeline** for addressing debt
- **Compare** with industry standards

## Success Metrics

A healthy codebase typically has:
- **< 5%** duplicate code
- **< 10** TODO/FIXME markers
- **0** try-catch-log patterns
- **< 3** levels of nesting average
- **< 50** lines per method
- **< 500** lines per class
- **> 80%** test coverage

## The Value

This analysis helps you:
- **Prevent bugs** before they happen
- **Improve maintainability** systematically
- **Reduce technical debt** with clear priorities
- **Ensure consistency** across the codebase
- **Track progress** over time
- **Make informed decisions** about refactoring

Remember: This is the same comprehensive analysis that discovered the 86 try-catch-log patterns we just fixed. Regular analysis helps maintain code quality and catch issues early.

---

$ARGUMENTS