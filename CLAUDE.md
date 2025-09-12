# NoLock.Social Frontend - AI Hive® Configuration

This document contains configuration and guidelines for AI Hive® AI development assistance on the NoLock.Social Blazor WebAssembly frontend application.

## 🔄 Project Awareness & Context
- **Always read `CLAUDE.md`** (this document) at the start of a new conversation to understand the project's architecture, goals, style, and constraints.
- **Check documentation files** (`docs/proposals/`, `docs/architecture/`) before starting a new task to understand existing patterns and decisions.
- **Use consistent naming conventions, file structure, and architecture patterns** as described throughout this document.
- **Never assume missing context. Ask questions if uncertain.**
- **Never hallucinate libraries, functions, or patterns** – only use known, verified Blazor/C#/.NET packages and patterns.
- **Always confirm file paths and component names** exist before referencing them in code or tests.
- **Never delete or overwrite existing code** unless explicitly instructed to or if part of a clearly defined task.

## 🦥 Productive Laziness Principle
- **Do the minimum necessary to solve the problem** - Don't add features, abstractions, or complexity that wasn't explicitly requested
- **Research existing solutions FIRST** - Before writing any code, search for:
  - Existing NuGet packages that solve the problem
  - Built-in .NET/Blazor features that handle the requirement
  - Community solutions on GitHub, Stack Overflow, or Blazor documentation
  - Existing components or services in the current codebase
- **Use existing solutions first** - Before writing new code, check if there's already a component, service, or pattern in the codebase that does what you need
- **Don't reinvent the wheel** - If a well-maintained package exists that solves 80% of the problem, use it rather than building from scratch
- **Don't refactor working code** unless it's broken, causing problems, or specifically requested
- **Avoid premature optimization** - Write simple, clear code first; optimize only when there's a proven performance issue
- **Don't add "nice to have" features** - Stick to the requirements; resist the urge to add extra functionality "while you're at it"
- **Use the simplest approach that works** - If a basic solution solves the problem, don't make it more complex "for flexibility"
- **Don't add excessive error handling** - Handle obvious error cases, but don't write defensive code for every theoretical edge case unless required
- **If it works, leave it alone** - Working code that meets requirements is better than "perfect" code that introduces new bugs

### Research-First Workflow
1. **Understand the requirement** - What exactly needs to be solved?
2. **Search for existing solutions** - NuGet, GitHub, documentation, existing codebase
3. **Evaluate options** - Is there a package/solution that fits well?
4. **Choose the laziest approach** - Use existing solution > modify existing > build minimal new code
5. **Implement only what's needed** - Don't add extra features or complexity

> **🔗 Related Principles**: Productive Laziness directly implements **DRY** (Don't Repeat Yourself - use existing solutions), **KISS** (Keep It Simple - simplest working solution), **YAGNI** (You Aren't Gonna Need It - minimum required features), and **TRIZ** (What if this didn't need to exist - leverage existing solutions).

## AI Agent Configuration

AI Hive® utilizes specialized AI agents, each with their own identity and expertise defined in `.claude/agents/`. Each agent maintains their individual name, personality, and specialized knowledge while working as part of the AI Hive® ecosystem by O2.services.

## Project Overview

**Company**: O2.services  
**AI Assistant**: AI Hive® by O2.services  
**Technology Stack**: Blazor WebAssembly, C#, ASP.NET Core, Bootstrap CSS, Material Design, JavaScript ES6 Modules  
**Target Platform**: Web browsers (Chrome, Firefox, Safari, Edge)  
**Mobile Support**: iOS Safari, Android Chrome with responsive design  
**Architecture**: Component-based with SOLID principles, KISS/DRY/YAGNI methodology  
**UI/UX Framework**: Material Design principles with Bootstrap integration  
**Core Principles**: Always follow "Vertical Slice", SOLID, KISS, DRY, YAGNI, TRIZ (see 🦥 **Productive Laziness Principle** for practical implementation)  

## Development Commands

### Build & Test Commands
```bash
# Build the application
cd NoLock.Social.Web && dotnet build

# Run in development mode
cd NoLock.Social.Web && dotnet run

# Run unit tests
dotnet test

# Lint and format (if applicable)
dotnet format
```

### Quality Gates
Before marking any task as complete, AI Hive® must verify:
- ✅ `dotnet build` completes without errors
- ✅ `dotnet test` passes all tests
- ✅ Application runs without runtime errors
- ✅ Manual testing confirms feature works
- ✅ Code follows existing patterns and conventions

## Project Structure

```
NoLock.Social.Components/     # Reusable Blazor components
├── Camera/                   # Camera-related components
├── Identity/                 # Authentication components
└── wwwroot/                  # Static web assets

NoLock.Social.Core/           # Business logic and services
├── Camera/                   # Camera domain models and services
├── Identity/                 # Authentication services
└── Interfaces/               # Service abstractions

NoLock.Social.Web/            # Main Blazor WebAssembly application
├── Pages/                    # Page components
├── Shared/                   # Shared UI components
└── wwwroot/                  # Web assets and JavaScript modules

docs/                         # Architecture documentation
├── proposals/                # Technical proposals and reviews
├── architecture/             # System architecture docs
└── scrum/                    # Project management docs
```

## 🧱 Code Structure & Modularity
- **Never create a file longer than 500 lines of code.** If a file approaches this limit, refactor by splitting it into modules or helper files.
- **Organize code into clearly separated modules**, grouped by feature or responsibility.
  For Blazor components this looks like:
    - `ComponentName.razor` - Main component markup and logic
    - `ComponentName.razor.cs` - Code-behind for complex logic (if needed)
    - `ComponentName.razor.css` - Component-specific styles
    - Supporting service classes in appropriate namespace folders
- **Use clear, consistent imports** and namespace organization.
- **Follow the existing project structure** as outlined in the Project Structure section.

## Coding Standards & Conventions

### C# Guidelines
- Use **explicit typing** and avoid `var` unless type is obvious
- Follow **async/await** patterns consistently
- Implement **IAsyncDisposable** for components with JavaScript interop
- Use **nullable reference types** and handle null cases explicitly
- Apply **SOLID principles** in component and service design (balanced with 🦥 **Productive Laziness** - don't over-abstract)
- **Use type hints and strong typing** - treat all code as strongly typed
- **Follow PEP8 equivalent for C#** - use consistent naming conventions (PascalCase for public members, camelCase for private/local)
- Write **XML documentation comments for every public method** using standard format:
  ```csharp
  /// <summary>
  /// Brief summary of what this method does.
  /// </summary>
  /// <param name="parameter">Description of parameter.</param>
  /// <returns>Description of return value.</returns>
  public async Task<Result> ExampleMethod(string parameter)
  ```

### JavaScript Interop Patterns
- Use **ES6 modules** instead of global window objects
- Always implement proper **async disposal** for JavaScript resources
- Follow **module-based architecture** for better encapsulation
- Cache **IJSObjectReference** instances and dispose properly

### Component Architecture
- Apply **single responsibility principle** - one clear purpose per component
- Use **component-local state** unless global state is required
- Implement **proper parameter binding** with EventCallback patterns
- Follow **existing modal/popup patterns** for consistency

### CSS & Styling
- Use **Bootstrap utilities** where possible
- Follow **Material Design principles** for component design and interactions
- Apply **Material Design color palettes**, typography, and spacing systems
- Implement **Material Design elevation** and shadow patterns
- Use **Material Design animation curves** and timing functions
- Follow **existing CSS patterns** (glass effects, animations)
- Implement **mobile-first responsive design**
- Support **iOS Safari viewport** issues with dynamic height calculations

## Mobile Development Guidelines

### iOS Safari Considerations
- Use `calc(var(--vh, 1vh) * 100)` for full height instead of `100vh`
- Handle **address bar hiding/showing** with viewport meta adjustments
- Implement **touch-action** CSS properties for gesture handling
- Test **orientation changes** and viewport updates

### Android Chrome Considerations
- Optimize for **memory constraints** on mobile devices
- Implement **progressive image loading** for large images
- Handle **keyboard appearance** affecting viewport height
- Test **different screen densities** and sizes

### Touch Interaction Patterns
- Implement **essential touch gestures** (tap, swipe)
- Follow **Material Design touch target guidelines** (minimum 48dp/48px)
- Apply **Material Design ripple effects** for touch feedback
- Use **Material Design gesture patterns** (swipe-to-dismiss, pull-to-refresh)
- Use **passive event listeners** for better scrolling performance
- Avoid **complex gesture recognition** unless specifically required

## Architecture Decision Records (ADRs)

### JavaScript Interop
- **Decision**: Use ES6 modules over global window objects
- **Reason**: Better encapsulation, CSP compliance, performance
- **Trade-off**: Slightly more setup complexity

### State Management
- **Decision**: Component-local state for single-component features
- **Reason**: KISS principle, avoid premature optimization (aligned with 🦥 **Productive Laziness**)
- **Trade-off**: Harder to extend to multi-component coordination

### Mobile Support
- **Decision**: Mobile-first responsive design with progressive enhancement
- **Reason**: Primary usage expected on mobile devices
- **Trade-off**: Additional testing complexity across device types

### UI/UX Design System
- **Decision**: Material Design principles with Bootstrap utility integration
- **Reason**: Consistent, accessible design language with proven mobile usability
- **Trade-off**: Larger CSS bundle size, requires Material Design expertise

## Development Workflow

### Feature Development
1. **Plan**: Create architectural proposal in `docs/proposals/`
2. **Review**: Get architectural review and address concerns
3. **Implement**: Follow baby-steps methodology with quality gates
4. **Test**: Manual testing on desktop and mobile browsers
5. **Document**: Update relevant documentation and ADRs

## 🧪 Testing & Reliability
- **Always create unit tests for new features** (components, services, methods, etc.) using xUnit and bUnit for Blazor components.
- **After updating any logic**, check whether existing unit tests need to be updated. If so, do it.
- **Tests should live in corresponding `.Tests` projects** mirroring the main project structure.
  - Include at least:
    - 1 test for expected use
    - 1 edge case
    - 1 failure case
- **Use the existing test patterns** found in the codebase (Mock services, TestContext from bUnit, etc.)

### Test Credentials
- **Username**: `alexanderfedin`
- **Password**: `Vilisaped1!`
- **Usage**: Always use these credentials for manual testing, authentication flows, and integration testing

### Quality Assurance
- **Unit Testing**: Test components and services in isolation using xUnit and bUnit
- **Integration Testing**: Test component interactions and service integrations
- **Manual Testing**: Verify user experience on target devices (desktop and mobile)
- **Performance Testing**: Check memory usage and rendering performance

### Code Review Guidelines
- **Architecture**: Follows SOLID principles and project patterns (see 🦥 **Productive Laziness** to avoid over-engineering)
- **Performance**: No obvious performance bottlenecks
- **Security**: No hardcoded secrets or XSS vulnerabilities
- **Maintainability**: Clear naming, appropriate abstractions
- **Mobile**: Works correctly on iOS Safari and Android Chrome

### Agent Identity Guidelines
- Each AI agent should identify themselves using their designated name from `.claude/agents/`
- Agents maintain their individual expertise and personality as defined in their agent files
- **principal-engineer** handles complex technical implementation and architectural decisions
- **system-architect-blazor** specializes in Blazor component architecture and patterns
- **qa-automation-engineer** focuses on testing strategies and quality assurance
- Other specialized agents should be used according to their defined expertise areas

## Common Patterns in Codebase

### Modal/Popup Pattern
```csharp
@if (ShowModal)
{
    <div class="modal-backdrop" @onclick="HandleBackdropClick">
        <div class="modal-dialog" @onclick:stopPropagation="true">
            <!-- Modal content -->
        </div>
    </div>
}
```

### JavaScript Module Loading
```csharp
private IJSObjectReference? _jsModule;

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/modules/component-module.js");
    }
}

public async ValueTask DisposeAsync()
{
    if (_jsModule != null)
        await _jsModule.DisposeAsync();
}
```

### Component Parameter Pattern
```csharp
[Parameter] public EventCallback<DataType> OnEvent { get; set; }
[Parameter] public RenderFragment? ChildContent { get; set; }
[Parameter] public string? CssClass { get; set; }
```

## Troubleshooting Common Issues

### Build Issues
- **Error**: "Package not found" → Check NuGet package references
- **Error**: "Nullable reference types" → Add null checks or use nullable operators
- **Error**: "JavaScript interop" → Verify module paths and disposal patterns

### Runtime Issues
- **Problem**: Component not rendering → Check StateHasChanged() calls
- **Problem**: JavaScript errors → Verify module loading and disposal
- **Problem**: Mobile layout issues → Check viewport meta tags and CSS

### Performance Issues
- **Problem**: Slow initial load → Check bundle size and lazy loading
- **Problem**: Memory leaks → Verify IAsyncDisposable implementation
- **Problem**: Poor mobile performance → Check image optimization and DOM complexity

## Security Considerations

### Content Security Policy (CSP)
- Use **ES6 modules** instead of inline scripts
- Avoid **dynamic script generation** or eval()
- Implement **proper nonce handling** if required
- Test with strict CSP policies

### Input Validation
- **Validate all user inputs** on both client and server
- **Sanitize HTML content** to prevent XSS
- **Use parameterized queries** for data access
- **Implement proper authentication** and authorization

## ✅ Task Completion & Progress Tracking
- **Use TodoWrite tool** to track complex tasks and show progress to users
- **Mark completed tasks immediately** after finishing them using TodoWrite
- Add new sub-tasks or TODOs discovered during development to the todo list under appropriate categories
- **Keep the todo list updated** throughout the development process to maintain visibility

## 📚 Documentation & Explainability
- **Update documentation files** (`docs/proposals/`, README files) when new features are added, dependencies change, or setup steps are modified
- **Comment non-obvious code** and ensure everything is understandable to a mid-level developer
- When writing complex logic, **add inline comments** explaining the why, not just the what
- **Use `// Reason:` comments** for explaining design decisions in code

## Documentation Standards

### Code Documentation
- **XML comments** for public APIs and complex methods
- **Inline comments** for business logic explanations
- **Architecture Decision Records** for significant design choices
- **README updates** for new features or breaking changes

### Technical Proposals
- Follow template in `docs/proposals/`
- Include **problem statement**, **proposed solution**, **alternatives considered**
- Address **SOLID principles**, **performance implications**, **mobile considerations** (guided by 🦥 **Productive Laziness** - only add complexity that's truly needed)
- Get **architectural review** before implementation

---

**Last Updated**: 2025-09-06  
**Project Version**: 1.0  
**Company**: O2.services  
**AI Hive® Compatibility**: ✅ Verified