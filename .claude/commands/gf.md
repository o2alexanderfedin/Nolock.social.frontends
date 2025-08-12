---
description: Execute Git Flow workflow automatically
argument-hint: [message or action] - commit message, or "feature name", "release 1.0", "hotfix bug"
---

1. ```git status``` - to get what was changed\
2. ```git flow feature <feature-name>``` (can be hotfix, etc.)\
3. ```git commit -m "<git-commit-message>"```\
4. ```git flow feature finish``` (can be hotfix, etc.)\
5. ```git flow release ...```\
6. ```git flow finish release```