# Critical Lost Features to Restore

## 1. Git Flow Automation & Commands

### Lost from code.md:
- **Automatic git flow detection** with branch naming conventions
- **Guided commit flow** with pre-commit hook handling
- **Pull request creation automation** with structured format
- **Git commands expertise**:
  ```bash
  # Lost examples:
  - git log command and git diff [base-branch]...HEAD
  - gh pr create with HEREDOC formatting
  - Automatic PR body generation with test plans
  - Branch tracking and remote push automation
  ```

### Lost from architect.md:
- **Version control patterns** for architecture decisions
- **Git-based documentation tracking**

## 2. Testing Framework Expertise (30+ Frameworks)

### Lost from code.md:
- **Comprehensive framework knowledge**:
  - Jest, Mocha, Jasmine, Karma (JavaScript)
  - pytest, unittest, nose2 (Python)
  - JUnit, TestNG, Spock (Java/JVM)
  - RSpec, Minitest (Ruby)
  - Go test, Testify (Go)
  - PHPUnit, Codeception (PHP)
  - NUnit, xUnit, MSTest (C#/.NET)
  - And 15+ more frameworks

### Lost from architect.md:
- **Test architecture patterns**:
  - Test pyramid strategies
  - Contract testing approaches
  - Performance testing frameworks
  - Security testing integration

## 3. TRIZ Methodology & Discovery Phase

### Lost from code.md:
- **MANDATORY INITIAL DISCOVERY PHASE**:
  1. Verify Current State (System Completeness)
  2. Find Existing Solutions (Use of Resources)
  3. Seek Simplification (Ideal Final Result)
  4. Identify Contradictions (Contradiction Resolution)
  5. Evolution Check (System Evolution)

- **TRIZ Engineering Patterns**:
  - Segmentation
  - Asymmetry
  - Dynamics
  - Preliminary Action
  - Cushioning
  - Inversion
  - Nesting
  - Prior Counteraction

### Lost from architect.md:
- **TRIZ application to architecture**:
  - Contradiction matrices for design decisions
  - Evolution trends for system planning
  - Resource utilization patterns

## 4. Baby-Steps Development Methodology

### Lost from code.md:
- **Strict enforcement** (2-5 minutes per task)
- **Explicit handoff format**: "Completed: [what]. State: [current]. Next: [suggestion]"
- **Git status discovery** after each step
- **Micro-task examples**:
  - Create one file/interface (2 min)
  - Add one method signature (1 min)
  - Write one unit test (3 min)
  - Implement one small function (5 min)

### Lost from architect.md:
- **Baby-steps in architecture**:
  - Incremental design documentation
  - Phased migration strategies
  - Step-by-step validation

## 5. Tool-Specific Implementation Details

### Lost from code.md:
- **Bash tool specifics**:
  - Background process management
  - Timeout handling (600000ms max)
  - Directory verification before operations
  - Proper quoting for paths with spaces

- **Testing tool patterns**:
  - Data-driven test examples
  - Parameterized test templates
  - Test refactoring triggers

### Lost from architect.md:
- **Architecture tool usage**:
  - Mermaid diagram generation
  - PlantUML integration
  - C4 model templates

## 6. Code Review & Quality Standards

### Lost from code.md:
- **Explicit REJECT criteria**:
  - Multiple test methods with identical logic
  - Copy-pasted test code
  - Non-DRY implementations

- **Explicit APPROVE criteria**:
  - Single parameterized tests
  - Clear test data with descriptive names
  - Proper abstraction levels

### Lost from architect.md:
- **Architecture review criteria**:
  - CAP theorem compliance
  - SOLID principle validation
  - Performance requirement verification

## 7. Principal Engineer Persona Elements

### Lost from code.md:
- **15+ years experience** positioning
- **Agile Leadership** competencies
- **SCRUM/Kanban** methodology expertise
- **Systematic Debugging** with scientific method

### Lost from architect.md:
- **Strategic thinking** emphasis
- **Cross-functional collaboration** patterns
- **Technology evaluation** frameworks

## 8. Specific Command Examples

### Lost Git Commands:
```bash
# Commit with HEREDOC (lost from code.md)
git commit -m "$(cat <<'EOF'
   Commit message here.

   🤖 Generated with [Claude Code](https://claude.ai/code)

   Co-Authored-By: Claude <noreply@anthropic.com>
   EOF
   )"

# PR creation with body (lost from code.md)
gh pr create --title "the pr title" --body "$(cat <<'EOF'
## Summary
<1-3 bullet points>

## Test plan
[Checklist of TODOs for testing the pull request...]

🤖 Generated with [Claude Code](https://claude.ai/code)
EOF
)"
```

### Lost Test Examples:
```csharp
// Data-driven approach (lost from code.md)
[Theory]
[InlineData(5, 5, 10, "positive numbers")]
[InlineData(-5, -5, -10, "negative numbers")]
void Calculate_ProducesCorrectResult(int a, int b, int expected, string scenario)
{
    var result = Calculate(a, b);
    Assert.Equal(expected, result, $"Failed: {scenario}");
}
```

## 9. Behavioral Guidelines

### Lost from code.md:
- **Research emphasis**: "research thoroughly using web search"
- **Trade-off communication**: "present options with trade-offs"
- **Hypothesis sharing**: "share your hypothesis and investigation process"

### Lost from architect.md:
- **Decision documentation**: ADR templates
- **Risk assessment**: probability/impact matrices
- **Stakeholder communication**: technical/non-technical translations

## 10. Environment & Platform Expertise

### Lost from both:
- **Cloud platform specifics**:
  - AWS service selection
  - Azure integration patterns
  - GCP best practices
  - Kubernetes orchestration

- **Framework-specific knowledge**:
  - React optimization patterns
  - Spring Boot configurations
  - Django best practices
  - Express.js middleware patterns

## Restoration Checklist

- [ ] Restore TRIZ methodology section completely
- [ ] Restore all 30+ testing frameworks
- [ ] Restore git flow automation logic
- [ ] Restore baby-steps enforcement with timing
- [ ] Restore explicit code review criteria
- [ ] Restore tool-specific implementation details
- [ ] Restore command examples with proper formatting
- [ ] Restore Principal Engineer positioning
- [ ] Restore behavioral guidelines
- [ ] Restore architecture patterns and tools

## Verification Steps

1. Check for "MANDATORY INITIAL DISCOVERY PHASE" heading
2. Verify TRIZ patterns are listed
3. Confirm testing frameworks count (30+)
4. Validate git command examples with HEREDOC
5. Ensure baby-steps timing (2-5 minutes) is specified
6. Check for REJECT/APPROVE criteria in reviews
7. Verify Principal Engineer with "15+ years" is mentioned
8. Confirm specific tool examples are present