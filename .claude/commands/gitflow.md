---
description: Git Flow workflow management (start/finish features, hotfixes, releases)
argument-hint: <action> [type] [name/version] - e.g., "start feature auth" or "finish release"
---

# Git Flow Command: $ARGUMENTS

I'll help you with Git Flow operations. Let me process your request: $ARGUMENTS

## Analysis

First, let me check the current Git Flow status and branch:

!git rev-parse --abbrev-ref HEAD
!git status --short

Now I'll execute the appropriate Git Flow workflow based on your request.

## Instructions for Git Flow Operations

Based on the arguments provided, I will:

1. **For starting a feature/hotfix/release:**
   - Check current branch status
   - Commit any pending changes if needed
   - Start the appropriate Git Flow branch
   - Switch to the new branch

2. **For finishing a feature/hotfix/release:**
   - Ensure we're on the correct branch type
   - Commit any uncommitted changes
   - Push the current branch
   - Complete the Git Flow merge process
   - Push all affected branches and tags
   - Clean up the completed branch

3. **For status requests:**
   - Show current branch and type
   - List active features, hotfixes, and releases
   - Display recent tags
   - Show uncommitted changes

The workflow will automatically:
- Initialize Git Flow if not already configured
- Handle commits with appropriate conventional commit messages
- Push all changes to origin
- Create tags for releases and hotfixes
- Merge to appropriate branches (develop for features, main+develop for hotfixes/releases)

Please confirm the specific action you want me to take with the Git Flow workflow.