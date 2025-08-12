---
description: Start or finish a Git Flow hotfix
argument-hint: start <name> | finish - e.g., "start critical-bug" or "finish"
---

# Git Flow Hotfix: $ARGUMENTS

I'll manage the Git Flow hotfix for you.

!git rev-parse --abbrev-ref HEAD
!git status --porcelain
!git tag --sort=-version:refname | head -1

Based on the arguments "$ARGUMENTS", I will:

1. Parse the action (start/finish) and hotfix name
2. If starting: Create hotfix branch from main
3. If finishing: Merge to both main and develop, create tag, push everything
4. Ensure the fix is applied to both production and development branches

Let me execute the Git Flow hotfix workflow.