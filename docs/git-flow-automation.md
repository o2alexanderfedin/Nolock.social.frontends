# Git Flow Automation Command

## Overview
A custom Claude Code slash command `/gf` that fully automates the git flow process for solo developers. It intelligently analyzes changes, creates appropriate branches, commits with meaningful messages, manages releases, and pushes everything.

## Installation
The command is already installed in `.claude/commands/gf.md`

## Usage

### Basic Usage
Simply type in Claude Code:
```
/gf
```
This will:
1. Analyze all current changes
2. Create appropriate git flow branch (feature/bugfix/hotfix)
3. Commit with auto-generated message
4. Complete the flow
5. Create and tag a new release
6. Push everything to origin

### With Custom Message
```
/gf implement user authentication
```
The provided text will be incorporated into the commit message.

## What It Does

### 1. Change Analysis
- Reviews all modified and new files
- Determines the type of change (feature, fix, refactor, etc.)
- Generates appropriate branch name

### 2. Branch Management
- **Features**: New functionality → `feature/branch-name`
- **Bugfixes**: Non-critical fixes → `bugfix/branch-name`  
- **Hotfixes**: Critical production fixes → `hotfix/branch-name`

### 3. Smart Commits
Uses conventional commit format:
- `feat:` New features
- `fix:` Bug fixes
- `refactor:` Code restructuring
- `docs:` Documentation updates
- `test:` Test additions/changes
- `chore:` Maintenance tasks

### 4. Intelligent Versioning
Automatically bumps version based on change type:
- **Major** (1.0.0 → 2.0.0): Breaking changes
- **Minor** (1.0.0 → 1.1.0): New features
- **Patch** (1.0.0 → 1.0.1): Fixes and minor changes

### 5. Complete Automation
- No pull requests (solo development)
- Non-interactive mode (no prompts)
- Handles all git flow commands
- Pushes to all necessary branches
- Creates and pushes tags

## Example Workflow

```bash
# You make changes to files...
# Then simply run:
/gf

# Output:
🚀 Git Flow Automation Complete!

Changes detected: 5 files (3 added, 2 modified)
Type: Feature (new functionality detected)
Branch: feature/add-encryption-layer
Commit: "feat: add encryption layer for sensitive data storage"
Version: 0.5.0 → 0.6.0 (minor bump for feature)

✅ All branches and tags pushed successfully!
```

## Behind The Scenes

The command uses the `git-flow-automation` agent which:
1. Has deep knowledge of git flow best practices
2. Understands semantic versioning
3. Can analyze code changes to determine type
4. Generates meaningful branch and commit names
5. Handles all error cases gracefully

## Requirements

- Git Flow installed (`git flow version`)
- Repository initialized with git flow (`git flow init`)
- Remote repository configured
- No uncommitted changes from previous work

## Troubleshooting

If the command fails:
1. Check git status manually: `git status`
2. Ensure git flow is initialized: `git flow config`
3. Verify remote is set: `git remote -v`
4. Check for merge conflicts
5. Run `/gf` again or manually complete the process

## Benefits

- **Consistency**: Every commit follows the same format
- **Speed**: Complete workflow in seconds
- **No Mistakes**: No forgotten steps or wrong commands
- **Smart**: Understands your changes and acts accordingly
- **Solo-Optimized**: No PR overhead for individual developers

## Notes

- Designed for solo development (no pull requests)
- Always creates a release (continuous delivery)
- Non-interactive (no prompts or confirmations)
- Handles everything from commit to push
- Safe: Reviews changes before acting