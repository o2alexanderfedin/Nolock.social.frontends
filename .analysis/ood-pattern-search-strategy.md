# OOD Pattern Search Strategy

## Quick Reference for Pattern Identification

### Creational Patterns Search
- **Factory Method**: Look for `Create*`, `Make*`, virtual creation methods
- **Abstract Factory**: Search for `*Factory` interfaces with multiple creation methods
- **Builder**: Find `*Builder` classes, fluent interfaces with `With*` methods
- **Singleton**: Static instances, `GetInstance()`, private constructors
- **Prototype**: `Clone()`, `Copy()` methods, `ICloneable` implementations

### Structural Patterns Search
- **Adapter**: `*Adapter`, `*Wrapper` classes, interface bridging
- **Decorator**: Classes wrapping same interface, `*Decorator` suffix
- **Facade**: `*Facade`, simplified API classes, service aggregators
- **Proxy**: `*Proxy`, lazy loading, remote object representations
- **Composite**: Tree structures, `Add/Remove` child methods
- **Bridge**: Abstraction and implementation hierarchies

### Behavioral Patterns Search
- **Strategy**: `*Strategy`, algorithm interfaces, policy classes
- **Observer**: `IObserver`, `Subscribe/Unsubscribe`, event handlers
- **Command**: `*Command`, `Execute()` methods, undo/redo
- **Chain of Responsibility**: `Handle()`, `SetNext()`, linked handlers
- **Template Method**: Abstract classes with protected virtual methods
- **Iterator**: `IEnumerator`, `GetEnumerator()`, foreach support
- **State**: `*State` classes, state transitions, context switching
- **Visitor**: `Visit()` methods, `Accept()` in elements
- **Mediator**: `*Mediator`, centralized communication
- **Memento**: Snapshot/restore functionality, state preservation

### Enterprise Patterns Search
- **Repository**: `*Repository`, data access abstraction
- **Unit of Work**: Transaction boundaries, change tracking
- **Dependency Injection**: Constructor parameters, `IServiceCollection`
- **Service Layer**: `*Service` classes, business logic encapsulation
- **Domain Model**: Entity classes, value objects, aggregates

## Search Commands

### Interface Pattern Search
```bash
# Find all interfaces
grep -r "interface I[A-Z]" --include="*.cs"

# Find abstract classes
grep -r "abstract class" --include="*.cs"

# Find base classes
grep -r ": [A-Z]\\w*Base" --include="*.cs"
```

### Common Pattern Suffixes
```bash
# Factory patterns
grep -r "class \\w*Factory" --include="*.cs"

# Service patterns
grep -r "class \\w*Service" --include="*.cs"

# Repository patterns
grep -r "class \\w*Repository" --include="*.cs"

# Builder patterns
grep -r "class \\w*Builder" --include="*.cs"
```

### Dependency Injection
```bash
# Constructor injection
grep -r "public \\w*\\([^)]*I[A-Z]" --include="*.cs"

# Service registration
grep -r "services\\.Add" --include="*.cs"
```

### Event Patterns
```bash
# Event handlers
grep -r "EventHandler" --include="*.cs"

# Event declarations
grep -r "event " --include="*.cs"
```