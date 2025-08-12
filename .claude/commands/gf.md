---
description: Simple Git Flow workflow - executes commands step by step
argument-hint: [message or action] - commit message, or "feature name", "release 1.0", "hotfix bug"
---

# Git Flow Step-by-Step Execution

I'll execute simple Git Flow commands step by step.

## Step 1: Check status
!git status

## Step 2: Get current branch
!git rev-parse --abbrev-ref HEAD

## Step 3: Execute workflow based on branch

!CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD) && echo "Working on: $CURRENT_BRANCH"

### For feature branches:
!if [[ $CURRENT_BRANCH == feature/* ]]; then
  FEATURE_NAME=${CURRENT_BRANCH#feature/}
  echo "=== Feature: $FEATURE_NAME ==="
  
  echo "Step 1: Add all changes"
  git add -A
  
  echo "Step 2: Commit changes"
  git commit -m "${ARGUMENTS:-feat: complete $FEATURE_NAME}"
  
  echo "Step 3: Push feature branch"
  git push -u origin $CURRENT_BRANCH
  
  echo "Step 4: Finish feature"
  git flow feature finish -F $FEATURE_NAME
  
  echo "Step 5: Push develop"
  git push origin develop
fi

### For release branches:
!if [[ $CURRENT_BRANCH == release/* ]]; then
  RELEASE_VERSION=${CURRENT_BRANCH#release/}
  echo "=== Release: $RELEASE_VERSION ==="
  
  echo "Step 1: Add all changes"
  git add -A
  
  echo "Step 2: Commit changes"
  git commit -m "${ARGUMENTS:-chore: release $RELEASE_VERSION}"
  
  echo "Step 3: Push release branch"
  git push -u origin $CURRENT_BRANCH
  
  echo "Step 4: Finish release"
  git flow release finish -F -m "Release $RELEASE_VERSION" $RELEASE_VERSION
  
  echo "Step 5: Push main"
  git push origin main
  
  echo "Step 6: Push develop"
  git push origin develop
  
  echo "Step 7: Push tags"
  git push --tags
fi

### For hotfix branches:
!if [[ $CURRENT_BRANCH == hotfix/* ]]; then
  HOTFIX_NAME=${CURRENT_BRANCH#hotfix/}
  echo "=== Hotfix: $HOTFIX_NAME ==="
  
  echo "Step 1: Add all changes"
  git add -A
  
  echo "Step 2: Commit changes"
  git commit -m "${ARGUMENTS:-fix: $HOTFIX_NAME}"
  
  echo "Step 3: Push hotfix branch"
  git push -u origin $CURRENT_BRANCH
  
  echo "Step 4: Finish hotfix"
  git flow hotfix finish -F -m "Hotfix $HOTFIX_NAME" $HOTFIX_NAME
  
  echo "Step 5: Push main"
  git push origin main
  
  echo "Step 6: Push develop"
  git push origin develop
  
  echo "Step 7: Push tags"
  git push --tags
fi

### For develop branch:
!if [[ $CURRENT_BRANCH == develop ]]; then
  echo "=== On develop branch ==="
  
  # Start new branch if requested
  if [[ "$ARGUMENTS" =~ ^feature[[:space:]]+(.*) ]]; then
    FEATURE_NAME="${BASH_REMATCH[1]}"
    echo "Starting feature: $FEATURE_NAME"
    git flow feature start $FEATURE_NAME
  elif [[ "$ARGUMENTS" =~ ^release[[:space:]]+(.*) ]]; then
    RELEASE_VERSION="${BASH_REMATCH[1]}"
    echo "Starting release: $RELEASE_VERSION"
    git flow release start $RELEASE_VERSION
  elif [[ "$ARGUMENTS" =~ ^hotfix[[:space:]]+(.*) ]]; then
    HOTFIX_NAME="${BASH_REMATCH[1]}"
    echo "Switching to main for hotfix"
    git checkout main
    echo "Starting hotfix: $HOTFIX_NAME"
    git flow hotfix start $HOTFIX_NAME
  else
    # Just commit and push if changes exist
    if [[ -n $(git status --porcelain) ]]; then
      echo "Step 1: Add all changes"
      git add -A
      echo "Step 2: Commit changes"
      git commit -m "${ARGUMENTS:-chore: update develop}"
      echo "Step 3: Push develop"
      git push origin develop
    else
      echo "No changes to commit"
    fi
  fi
fi

### For main branch:
!if [[ $CURRENT_BRANCH == main ]]; then
  echo "=== On main branch ==="
  
  if [[ "$ARGUMENTS" =~ ^hotfix[[:space:]]+(.*) ]]; then
    HOTFIX_NAME="${BASH_REMATCH[1]}"
    echo "Starting hotfix: $HOTFIX_NAME"
    git flow hotfix start $HOTFIX_NAME
  else
    # Just commit and push if changes exist
    if [[ -n $(git status --porcelain) ]]; then
      echo "Step 1: Add all changes"
      git add -A
      echo "Step 2: Commit changes"
      git commit -m "${ARGUMENTS:-chore: update main}"
      echo "Step 3: Push main"
      git push origin main
    else
      echo "No changes to commit"
    fi
  fi
fi

Done!