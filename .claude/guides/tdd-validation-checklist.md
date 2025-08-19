# TDD Validation Checklist

## Quick Reference: Is Your Work Following TDD?

### ✅ Code Development TDD (Principal Engineer)
- [ ] **RED**: Test written BEFORE implementation?
- [ ] **GREEN**: Minimal code to pass test?
- [ ] **REFACTOR**: Code improved with tests passing?
- [ ] Baby steps: Each change < 5 minutes?
- [ ] Git commits: One per TDD cycle?

### ✅ Test Development TDD (QA Automation)
- [ ] **Discovery Phase** completed first?
- [ ] Existing tests checked via `git status`?
- [ ] TRIZ alternatives explored?
- [ ] One test case at a time?
- [ ] Data-driven approach used?

### ✅ Architecture TDD (System & Solutions Architects)
- [ ] **ADR created** BEFORE implementation?
- [ ] Testable architecture decisions?
- [ ] TRIZ methodology applied?
- [ ] Validation criteria defined upfront?
- [ ] Architecture tests written first?

### ✅ Business Requirements TDD (Product Owner)
- [ ] **User story** written BEFORE code?
- [ ] Acceptance criteria defined clearly?
- [ ] BDD scenarios created (Given/When/Then)?
- [ ] Business value validated first?
- [ ] Sprint goals test-driven?

### ✅ Infrastructure TDD (DevOps/SRE)
- [ ] **Pipeline tests** written before changes?
- [ ] Infrastructure as Code tested first?
- [ ] Monitoring defined before deployment?
- [ ] Security tests before implementation?
- [ ] Performance criteria pre-defined?

### ✅ Frontend TDD (Senior Web Developer)
- [ ] **Component test** before component?
- [ ] User interaction tests first?
- [ ] Accessibility tests defined upfront?
- [ ] State management tests before stores?
- [ ] API mocks created before integration?

### ✅ Workflow TDD (Git Flow Automation)
- [ ] **Workflow tests** before automation?
- [ ] Branch protection rules tested?
- [ ] PR validation automated?
- [ ] Commit message validation tested?
- [ ] Merge conflict tests defined?

## 🔴 Red Flags: You're NOT Following TDD If...
- ❌ Writing code without failing tests
- ❌ Writing multiple features at once
- ❌ Tests written after implementation
- ❌ Skipping the refactor phase
- ❌ Large commits spanning hours
- ❌ No test for bug fixes
- ❌ Architecture without ADRs
- ❌ Features without user stories

## 🟢 Green Lights: You ARE Following TDD When...
- ✅ Every feature starts with a failing test
- ✅ Commits are small and frequent
- ✅ Tests drive the design
- ✅ Refactoring is continuous
- ✅ Coverage increases naturally
- ✅ Baby steps are the norm
- ✅ Documentation comes first
- ✅ Tests are the specification

## Quick Audit Questions

### For Any New Feature:
1. Where is the failing test?
2. What's the smallest change to make it pass?
3. How can this be refactored?
4. Is this a baby step (< 5 min)?

### For Any Bug Fix:
1. Where is the test that reproduces it?
2. Did the test fail before the fix?
3. Does the test pass after the fix?
4. Are there related edge cases to test?

### For Any Architecture Change:
1. Where is the ADR?
2. What tests validate this decision?
3. How is this testable?
4. What TRIZ principles apply?

### For Any User Story:
1. Where are the acceptance tests?
2. What BDD scenarios exist?
3. How is success measured?
4. What's the minimum viable test?

## TDD Enforcement by Agent

| Agent | TDD Trigger | Validation |
|-------|------------|------------|
| Principal Engineer | Any code change | Test exists first |
| QA Automation | Any test creation | Discovery phase done |
| System Architect | Any design decision | ADR with tests |
| Solutions Architect | Any integration | Contract tests first |
| Product Owner | Any requirement | BDD scenarios exist |
| DevOps/SRE | Any deployment | Pipeline tests pass |
| Web Developer | Any UI change | Component test first |
| Git Flow | Any workflow change | Automation test exists |

## Daily TDD Standup Questions
1. What test did you write yesterday?
2. What test are you writing today?
3. What's blocking you from writing tests?

## TDD Metrics to Track
- [ ] Test-first commit ratio (target: >80%)
- [ ] Average time per TDD cycle (<15 min)
- [ ] Test coverage trend (increasing)
- [ ] Defect escape rate (decreasing)
- [ ] Refactoring frequency (high)

---
*Remember: TDD is not about testing, it's about design. Let tests drive your work!*