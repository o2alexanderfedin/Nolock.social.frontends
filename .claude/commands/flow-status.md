---
description: Show current Git Flow status and active branches
argument-hint: (no arguments needed)
---

# Git Flow Status

Let me check the current Git Flow status for you:

## Current Branch
!git rev-parse --abbrev-ref HEAD

## Working Directory Status
!git status --short

## Active Git Flow Branches
!git branch -a | grep -E "feature/|hotfix/|release/" | sed 's/^[ *]*//'

## Recent Tags (Releases)
!git tag --sort=-version:refname | head -5

## Git Flow Configuration
!git config --get gitflow.branch.master || echo "Git Flow not initialized"
!git config --get gitflow.branch.develop || echo "No develop branch configured"

## Summary
Based on the above information, I can see:
- Your current branch and any uncommitted changes
- All active feature, hotfix, and release branches
- Recent version tags
- Whether Git Flow is properly initialized

Would you like me to help you start or finish any Git Flow branches?