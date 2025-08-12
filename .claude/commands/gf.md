---
description: Complete Git Flow workflow - commit, push, and finish/release in one command
argument-hint: [commit message] - Auto-detects branch type and completes the flow
---

# Git Flow Complete Workflow

I'll handle the complete Git Flow workflow: commit, push, and finish/release based on your current branch.

## Current Status
!git rev-parse --abbrev-ref HEAD
!git status --short

## Analyzing Branch and Executing Workflow

Let me detect your current branch type and execute the appropriate workflow:

1. **If on a feature branch**: 
   - Commit all changes
   - Push to origin
   - Finish feature (merge to develop)
   - Push develop

2. **If on a hotfix branch**:
   - Commit all changes  
   - Push to origin
   - Finish hotfix (merge to main and develop)
   - Create tag
   - Push main, develop, and tags

3. **If on a release branch**:
   - Commit all changes
   - Push to origin
   - Finish release (merge to main and develop)
   - Create version tag
   - Push main, develop, and tags

4. **If on develop**:
   - Commit all changes
   - Push to origin
   - Optionally start a new release if requested

5. **If on main**:
   - Commit all changes
   - Push to origin

The commit message will be: $ARGUMENTS (or auto-generated based on changes if not provided)

Let me execute this workflow now.