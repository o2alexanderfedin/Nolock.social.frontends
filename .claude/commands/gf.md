---
description: Execute Git Flow workflow automatically
---

## Run step-by-step:

1. ```git status``` - to get what was changed\
2. ```git flow feature start <feature-name>``` (can be hotfix, etc.)\
3. ```git add -A```
4. ```git commit -m "<git-commit-message>"```\
5. ```git flow feature finish``` (can be hotfix, etc.)\
6. ```git flow start release ...```\
7. ```git flow finish release```