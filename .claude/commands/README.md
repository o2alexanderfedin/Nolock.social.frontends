# Git Flow Slash Commands

Custom slash commands for Git Flow workflow management in Claude Code.

## Available Commands

### `/gitflow <action> [type] [name]`
General Git Flow management command.

**Examples:**
- `/gitflow start feature user-auth` - Start a new feature
- `/gitflow finish feature` - Finish current feature
- `/gitflow start release 1.2.0` - Start a release
- `/gitflow status` - Show Git Flow status

### `/feature <action> [name]`
Manage feature branches.

**Examples:**
- `/feature start authentication` - Start feature/authentication branch
- `/feature finish` - Finish current feature and merge to develop

### `/release <action> [version]`
Manage release branches.

**Examples:**
- `/release start 1.2.0` - Start release/1.2.0 branch
- `/release finish` - Finish release, merge to main and develop, create tag

### `/hotfix <action> [name]`
Manage hotfix branches for production issues.

**Examples:**
- `/hotfix start critical-bug` - Start hotfix/critical-bug from main
- `/hotfix finish` - Finish hotfix, merge to main and develop, create tag

### `/flow-status`
Display current Git Flow status.

Shows:
- Current branch
- Uncommitted changes
- Active feature/hotfix/release branches
- Recent version tags
- Git Flow configuration

## Git Flow Workflow

### Features
- Created from: `develop`
- Merged to: `develop`
- Purpose: New functionality

### Releases
- Created from: `develop`
- Merged to: `main` and `develop`
- Purpose: Prepare new production release
- Creates version tag

### Hotfixes
- Created from: `main`
- Merged to: `main` and `develop`
- Purpose: Emergency production fixes
- Creates version tag

## Automatic Actions

All commands automatically:
1. Initialize Git Flow if needed
2. Commit uncommitted changes with appropriate messages
3. Push branches to origin
4. Create tags for releases and hotfixes
5. Clean up completed branches

## Configuration

Git Flow uses these branch names:
- Production: `main`
- Development: `develop`
- Features: `feature/*`
- Releases: `release/*`
- Hotfixes: `hotfix/*`

## Solo Developer Mode

These commands are optimized for solo development:
- No pull requests required
- Automatic pushing to origin
- Direct merging without review
- Simplified commit messages