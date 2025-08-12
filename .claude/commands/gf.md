---
description: Execute Git Flow workflow automatically
---

## Run step-by-step:

1. `git status` - check what was changed
2. `git flow feature start <name>` - start feature branch
3. `git add -A` - stage all changes
4. `git commit -m "<message>"` - commit changes
5. `git flow feature finish -F <name>` - finish feature (non-interactive)
6. `git push origin develop` - push develop
7. `git flow release start <version>` - start release
8. `git flow release finish -F -m "Release-<version>" <version>` - finish release (non-interactive)
9. `git push origin main` - push main
10. `git push origin develop` - push develop again
11. `git push --tags` - push tags