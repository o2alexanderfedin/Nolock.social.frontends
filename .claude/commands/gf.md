---
description: Complete Git Flow automation - commits, pushes, finishes, or starts branches
argument-hint: [action/message] - "feature name", "release 1.0", "hotfix bug", or commit message
---

# Git Flow Automation

I'll handle the complete Git Flow workflow automatically based on your current branch and the arguments provided.

## Checking current status...

!git rev-parse --abbrev-ref HEAD
!git status --porcelain

## Executing Git Flow Workflow

Based on the current branch and arguments "$ARGUMENTS", I will:

### If on develop branch:
- If arguments start with "feature": Start new feature branch
- If arguments start with "release": Start new release branch  
- If arguments start with "hotfix": Switch to main and start hotfix
- Otherwise: Commit and push any changes

### If on main branch:
- If arguments start with "hotfix": Start new hotfix branch
- Otherwise: Commit and push any changes

### If on feature/* branch:
- Commit all changes (using arguments as message or auto-generate)
- Push to origin
- Finish feature (merge to develop)
- Delete feature branch
- Push develop

### If on release/* branch:
- Commit all changes
- Push to origin
- Finish release (merge to main and develop)
- Create version tag
- Delete release branch
- Push main, develop, and tags

### If on hotfix/* branch:
- Commit all changes
- Push to origin
- Finish hotfix (merge to main and develop)
- Create version tag
- Delete hotfix branch
- Push main, develop, and tags

Let me execute the appropriate workflow now...