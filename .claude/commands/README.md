# Git Flow Slash Command

A single, comprehensive Git Flow command that handles the complete workflow using `git flow` commands: commit, push, and finish/release automatically.

**Prerequisites:** Requires `git flow` to be installed and initialized.

## Command: `/gf`

### Usage
```
/gf [commit message]
```

### What it does

The `/gf` command automatically uses `git flow` commands to:

1. **Detects your current branch type** (feature, hotfix, release, develop, or main)
2. **Commits all changes** with your message (or auto-generates one)
3. **Pushes to origin**
4. **Executes proper git flow commands** based on branch type:

#### On Feature Branch (`feature/*`)
- Commits and pushes changes
- Runs `git flow feature finish <name>`
- Pushes develop branch

#### On Hotfix Branch (`hotfix/*`)
- Commits and pushes changes
- Runs `git flow hotfix finish <name>`
- Creates version tag
- Pushes main, develop, and tags

#### On Release Branch (`release/*`)
- Commits and pushes changes
- Runs `git flow release finish <version>`
- Creates version tag
- Pushes main, develop, and tags

#### On Develop Branch
- Commits and pushes changes
- Ready for next feature or release

#### On Main Branch
- Commits and pushes changes
- Production branch updated

### Examples

```bash
# On develop: start a new feature
/gf feature user-auth

# On feature branch: finish with commit message
/gf feat: add user authentication

# On develop: start a release
/gf release 1.2.0

# On release/hotfix: finish with tag message
/gf Release version 1.2.0
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