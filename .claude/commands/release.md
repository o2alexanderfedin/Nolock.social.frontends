---
description: Start or finish a Git Flow release
argument-hint: start <version> | finish - e.g., "start 1.2.0" or "finish"
---

# Git Flow Release: $ARGUMENTS

I'll manage the Git Flow release for you.

!git rev-parse --abbrev-ref HEAD
!git status --porcelain
!git tag --sort=-version:refname | head -3

Based on the arguments "$ARGUMENTS", I will:

1. Parse the action (start/finish) and version
2. If starting: Create release branch from develop, update version files
3. If finishing: Merge to main and develop, create version tag, push everything
4. Handle version bumping in package.json or other version files

Let me execute the Git Flow release workflow.