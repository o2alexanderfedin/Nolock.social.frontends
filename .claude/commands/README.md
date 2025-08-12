# Git Flow Slash Command

A single, comprehensive Git Flow command that handles the complete workflow: commit, push, and finish/release automatically.

## Command: `/gf`

### Usage
```
/gf [commit message]
```

### What it does

The `/gf` command automatically:

1. **Detects your current branch type** (feature, hotfix, release, develop, or main)
2. **Commits all changes** with your message (or auto-generates one)
3. **Pushes to origin**
4. **Completes the Git Flow workflow** based on branch type:

#### On Feature Branch (`feature/*`)
- Commits and pushes changes
- Finishes feature (merges to develop)
- Pushes develop branch
- Deletes feature branch

#### On Hotfix Branch (`hotfix/*`)
- Commits and pushes changes
- Finishes hotfix (merges to main AND develop)
- Creates version tag
- Pushes main, develop, and tags
- Deletes hotfix branch

#### On Release Branch (`release/*`)
- Commits and pushes changes
- Finishes release (merges to main AND develop)
- Creates version tag
- Pushes main, develop, and tags
- Deletes release branch

#### On Develop Branch
- Commits and pushes changes
- Ready for next feature or release

#### On Main Branch
- Commits and pushes changes
- Production branch updated

### Examples

```bash
# Finish current feature with auto-generated message
/gf

# Finish current feature with custom message
/gf feat: add user authentication

# Complete release with version message
/gf chore: release version 1.2.0

# Finish hotfix with descriptive message
/gf fix: resolve critical payment bug
```

### Solo Developer Optimized

- **No Pull Requests** - Direct merging
- **Automatic Push** - All branches pushed to origin
- **Smart Detection** - Knows what to do based on current branch
- **One Command** - Complete workflow in a single step

### Git Flow Branch Structure

```
main (production)
├── hotfix/* (emergency fixes)
└── release/* (version releases)

develop (next release)
└── feature/* (new features)
```

### Workflow Philosophy

This command embodies the "commit and ship" philosophy:
- Make changes
- Run `/gf` with a message
- Everything else happens automatically
- No manual branch management needed