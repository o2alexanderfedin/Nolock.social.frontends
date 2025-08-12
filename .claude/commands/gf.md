---
description: Complete Git Flow automation using git flow commands
argument-hint: [action/message] - "feature name", "release 1.0", "hotfix bug", or commit message
---

# Git Flow Automation

I'll execute the complete Git Flow workflow using `git flow` commands based on your current branch and arguments.

## Current Status

!git rev-parse --abbrev-ref HEAD
!git status --porcelain

## Executing Workflow

Let me analyze the current branch and arguments "$ARGUMENTS" to determine the appropriate action:

!CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD); echo "Current branch: $CURRENT_BRANCH"

Based on the branch and your input, I will now:

### On develop branch:
- Parse arguments to detect if starting new feature/release/hotfix
- Or commit and push changes if arguments are a commit message

### On main branch:
- Start hotfix if requested
- Or commit and push changes

### On feature/* branch:
- Commit all changes with provided message
- Push the feature branch
- Use `git flow feature finish` to complete
- Push develop branch

### On release/* branch:
- Commit all changes
- Push the release branch
- Use `git flow release finish` with tag message
- Push main, develop, and tags

### On hotfix/* branch:
- Commit all changes
- Push the hotfix branch
- Use `git flow hotfix finish` with tag message
- Push main, develop, and tags

Executing the appropriate git flow commands now...