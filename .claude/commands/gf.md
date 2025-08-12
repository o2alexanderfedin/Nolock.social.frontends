---
description: Execute Git Flow workflow automatically with MANDATORY release
---

## Run step-by-step:

1. `git status` - check what was changed
2. `git flow feature start <name>` - start feature branch  
3. `git add -A` - stage all changes
4. `git commit -m "<message>"` - commit changes
5. `git flow feature finish -F <name>` - finish feature (non-interactive)
6. `git push origin develop` - push develop

## MANDATORY RELEASE (ALWAYS EXECUTE):
7. Auto-detect version:
   - Get latest tag: `git describe --tags --abbrev=0` or default to 0.0.0
   - Auto-increment patch version (0.1.0 → 0.1.1)
   - Or minor version for features (0.1.0 → 0.2.0)
8. `git flow release start <new-version>` - start release with auto version
9. `git flow release finish -F -m "Release-<new-version>" <new-version>` - finish release
10. `git push origin main` - push main
11. `git push origin develop` - push develop again
12. `git push --tags` - push tags

**IMPORTANT**: Release is MANDATORY! Version is auto-incremented:
- Patch: 0.1.0 → 0.1.1 (for fixes)
- Minor: 0.1.0 → 0.2.0 (for features)
- Major: 0.1.0 → 1.0.0 (for breaking changes)