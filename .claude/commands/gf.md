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
7. `git flow release start <version>` - start release (auto-increment version)
8. `git flow release finish -F -m "Release-<version>" <version>` - finish release
9. `git push origin main` - push main
10. `git push origin develop` - push develop again
11. `git push --tags` - push tags

**IMPORTANT**: Release steps 7-11 are MANDATORY and must ALWAYS be executed after any feature completion to ensure code reaches production!