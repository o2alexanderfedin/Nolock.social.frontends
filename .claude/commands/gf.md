---
description: Complete Git Flow automation - commits, pushes, and finishes everything in one shot
argument-hint: [action/message] - "feature name", "release 1.0", "hotfix bug", or commit message
---

# Git Flow Complete Automation

I'll execute the complete Git Flow workflow in one shot based on your current branch.

## Current Status

!git rev-parse --abbrev-ref HEAD
!git status --porcelain

## Executing Complete Workflow

Let me analyze the current branch and execute the full workflow:

!CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD) && echo "Current branch: $CURRENT_BRANCH"

Based on the branch "$CURRENT_BRANCH" and arguments "$ARGUMENTS", I'll now execute:

### If on develop branch with arguments:
- **"feature <name>"**: Run `git flow feature start <name>`
- **"release <version>"**: Run `git flow release start <version>`  
- **"hotfix <name>"**: Run `git checkout main && git flow hotfix start <name>`
- **Otherwise**: Commit with message and push

### If on main branch with arguments:
- **"hotfix <name>"**: Run `git flow hotfix start <name>`
- **Otherwise**: Commit and push

### If on feature/* branch:
!if [[ "$CURRENT_BRANCH" == feature/* ]]; then
  FEATURE_NAME=${CURRENT_BRANCH#feature/}
  echo "Finishing feature: $FEATURE_NAME"
  
  # Commit any changes
  if [[ -n $(git status --porcelain) ]]; then
    git add -A
    git commit -m "${ARGUMENTS:-feat: complete $FEATURE_NAME}"
  fi
  
  # Push feature branch
  git push -u origin "$CURRENT_BRANCH"
  
  # Finish feature (non-interactive, no-edit)
  git flow feature finish -F "$FEATURE_NAME"
  
  # Push develop
  git push origin develop
fi

### If on release/* branch:
!if [[ "$CURRENT_BRANCH" == release/* ]]; then
  RELEASE_VERSION=${CURRENT_BRANCH#release/}
  echo "Finishing release: $RELEASE_VERSION"
  
  # Commit any changes
  if [[ -n $(git status --porcelain) ]]; then
    git add -A
    git commit -m "${ARGUMENTS:-chore: release $RELEASE_VERSION}"
  fi
  
  # Push release branch
  git push -u origin "$CURRENT_BRANCH"
  
  # Finish release (non-interactive with message)
  git flow release finish -F -m "Release $RELEASE_VERSION" "$RELEASE_VERSION"
  
  # Push everything
  git push origin main
  git push origin develop
  git push --tags
fi

### If on hotfix/* branch:
!if [[ "$CURRENT_BRANCH" == hotfix/* ]]; then
  HOTFIX_NAME=${CURRENT_BRANCH#hotfix/}
  echo "Finishing hotfix: $HOTFIX_NAME"
  
  # Commit any changes
  if [[ -n $(git status --porcelain) ]]; then
    git add -A
    git commit -m "${ARGUMENTS:-fix: $HOTFIX_NAME}"
  fi
  
  # Push hotfix branch
  git push -u origin "$CURRENT_BRANCH"
  
  # Finish hotfix (non-interactive with message)
  git flow hotfix finish -F -m "Hotfix $HOTFIX_NAME" "$HOTFIX_NAME"
  
  # Push everything
  git push origin main
  git push origin develop
  git push --tags
fi

### If on develop/main and just need to commit:
!if [[ "$CURRENT_BRANCH" == "develop" ]] || [[ "$CURRENT_BRANCH" == "main" ]]; then
  # Parse arguments for branch creation
  if [[ "$ARGUMENTS" =~ ^feature[[:space:]]+(.*) ]]; then
    FEATURE_NAME="${BASH_REMATCH[1]}"
    echo "Starting feature: $FEATURE_NAME"
    git flow feature start "$FEATURE_NAME"
  elif [[ "$ARGUMENTS" =~ ^release[[:space:]]+(.*) ]]; then
    RELEASE_VERSION="${BASH_REMATCH[1]}"
    echo "Starting release: $RELEASE_VERSION"
    git flow release start "$RELEASE_VERSION"
  elif [[ "$ARGUMENTS" =~ ^hotfix[[:space:]]+(.*) ]]; then
    HOTFIX_NAME="${BASH_REMATCH[1]}"
    echo "Starting hotfix: $HOTFIX_NAME"
    [[ "$CURRENT_BRANCH" != "main" ]] && git checkout main
    git flow hotfix start "$HOTFIX_NAME"
  else
    # Just commit and push if there are changes
    if [[ -n $(git status --porcelain) ]]; then
      git add -A
      git commit -m "${ARGUMENTS:-chore: update $CURRENT_BRANCH}"
      git push origin "$CURRENT_BRANCH"
    else
      echo "No changes to commit on $CURRENT_BRANCH"
    fi
  fi
fi

Done! The workflow has been completed automatically.