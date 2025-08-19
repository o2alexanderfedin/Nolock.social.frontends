# Programming by Intent - Validation Checklist

## Core Intent Validation Questions

### Primary Validation
- [ ] **Does it express WHAT to accomplish rather than HOW?**
  - Focus on the goal, not the method
  - Describe the desired outcome, not the steps
  
- [ ] **Can a non-technical person understand the goal?**
  - Would a business stakeholder grasp the intent?
  - Is the language accessible to all team members?
  
- [ ] **Is it free from tool-specific instructions?**
  - No mentions of specific frameworks or libraries
  - No references to particular commands or utilities
  
- [ ] **Does it focus on outcomes rather than processes?**
  - Describes the end state, not the journey
  - Emphasizes value delivered, not tasks performed

## Red Flags to Avoid

### Process-Oriented Anti-Patterns
- ❌ **Step-by-step procedures**: "First do X, then Y, finally Z"
- ❌ **Tool or framework names**: "Use React", "Run webpack", "Configure Docker"
- ❌ **Command-line instructions**: "Execute npm install", "Run git commit"
- ❌ **Timing specifications**: "Wait 5 seconds", "Run every hour"
- ❌ **Format prescriptions**: "Create a JSON file", "Use YAML format"

### Implementation Details
- ❌ **Algorithm specifications**: "Use binary search", "Implement recursion"
- ❌ **Data structure mandates**: "Store in HashMap", "Use linked list"
- ❌ **Performance directives**: "Optimize for O(n)", "Cache results"
- ❌ **Architecture prescriptions**: "Create microservice", "Use REST API"

## Green Flags to Embrace

### Intent-Focused Patterns
- ✅ **Clear goal statements**: "Enable user authentication"
- ✅ **Outcome descriptions**: "Users can securely access their accounts"
- ✅ **Value propositions**: "Reduce page load time for better user experience"
- ✅ **Natural language**: "Make the search feature more helpful"
- ✅ **Trust in system capabilities**: "Ensure data consistency across the system"

### Business-Aligned Language
- ✅ **User stories**: "As a user, I want to..."
- ✅ **Problem statements**: "Customers need a way to..."
- ✅ **Success criteria**: "The system should enable..."
- ✅ **Value delivery**: "This will allow users to..."

## Transformation Examples

### Example 1: API Development
**❌ Before (Implementation-focused)**
```
Create a REST API endpoint using Express.js at /api/users with GET and POST methods. 
Implement pagination with limit and offset parameters. Use MongoDB for data storage.
```

**✅ After (Intent-focused)**
```
Enable retrieval and creation of user information through the system. Support browsing 
through large sets of users efficiently. Ensure user data is persistently stored.
```

### Example 2: Performance Optimization
**❌ Before (Process-focused)**
```
Implement Redis caching with 1-hour TTL. Use connection pooling with max 10 connections. 
Add indexes on frequently queried columns. Implement lazy loading for images.
```

**✅ After (Outcome-focused)**
```
Make the application respond quickly even under heavy load. Ensure users experience 
fast page loads and smooth interactions. Minimize waiting time for data retrieval.
```

### Example 3: Testing Strategy
**❌ Before (Tool-focused)**
```
Write Jest unit tests with 80% coverage. Use Cypress for E2E tests. Run tests in CI/CD 
pipeline using GitHub Actions. Generate coverage reports with Istanbul.
```

**✅ After (Goal-focused)**
```
Ensure the system behaves correctly and reliably. Catch issues before they affect users. 
Build confidence that changes don't break existing functionality.
```

### Example 4: Security Implementation
**❌ Before (Method-focused)**
```
Implement JWT tokens with RS256 algorithm. Store refresh tokens in HttpOnly cookies. 
Use bcrypt with 10 salt rounds for password hashing. Enable CORS with whitelist.
```

**✅ After (Intent-focused)**
```
Protect user accounts from unauthorized access. Ensure sensitive data remains 
confidential. Allow only legitimate requests to access the system.
```

## Review Process

### Auditing Existing Prompts

1. **Identify Implementation Language**
   - Scan for tool names, commands, or technical jargon
   - Look for step-by-step instructions
   - Find prescriptive "how-to" statements

2. **Extract Core Intent**
   - Ask: "What problem does this solve?"
   - Ask: "What value does this deliver?"
   - Ask: "What should be different after this?"

3. **Rewrite for Intent**
   - Remove all implementation details
   - Focus on the desired outcome
   - Use business-friendly language

### Validating New Prompts

1. **Initial Draft Review**
   - Write your first version naturally
   - Don't worry about perfection initially

2. **Intent Extraction**
   - Highlight every "how" statement
   - Circle every tool or technology mention
   - Underline process descriptions

3. **Transformation**
   - Replace "how" with "what"
   - Replace tools with capabilities
   - Replace processes with outcomes

4. **Final Validation**
   - Would a new team member understand the goal?
   - Could multiple valid implementations achieve this?
   - Does it leave room for innovation?

### Maintaining Intent Focus

1. **Regular Reviews**
   - Schedule periodic audits of documentation
   - Review prompts before major releases
   - Update based on lessons learned

2. **Team Alignment**
   - Share this checklist with all contributors
   - Discuss intent vs implementation in reviews
   - Celebrate good intent-based examples

3. **Continuous Improvement**
   - Collect examples of successful transformations
   - Document common pitfalls specific to your domain
   - Evolve the checklist based on experience

## Quick Reference Card

### Before Writing
Ask yourself:
- What problem am I solving?
- What value am I delivering?
- What should be possible afterward?

### While Writing
Remember:
- Express goals, not methods
- Describe outcomes, not processes
- Trust the system's intelligence

### After Writing
Check:
- Can someone else achieve this differently?
- Is it free from implementation bias?
- Does it focus on value delivery?

## Intent Declaration Template

When creating new prompts or documentation, use this template:

```
INTENT: [What should be accomplished]
VALUE: [Why this matters to users/business]
SUCCESS: [How we'll know it's achieved]
```

Avoid:
```
STEPS: [How to do it]
TOOLS: [What to use]
PROCESS: [Specific workflow]
```

---

*Remember: Great intent-based programming trusts in intelligence—both human and artificial—to find the best path to the goal.*