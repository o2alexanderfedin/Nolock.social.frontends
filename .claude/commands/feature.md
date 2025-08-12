---
description: Start or finish a Git Flow feature branch
argument-hint: start <name> | finish - e.g., "start user-auth" or "finish"
---

# Git Flow Feature: $ARGUMENTS

I'll manage the Git Flow feature branch for you.

!git rev-parse --abbrev-ref HEAD
!git status --porcelain

Based on the arguments "$ARGUMENTS", I will now:

1. Parse the action (start/finish) and feature name
2. If starting: Create and checkout a new feature branch from develop
3. If finishing: Merge the current feature to develop and push
4. Handle any uncommitted changes appropriately

Let me execute the Git Flow feature workflow.