# Unity Build Fix - Semantic Versioning Error

## Issue Identified

### Error:
```
Error: Branch is dirty. Refusing to base semantic version on uncommitted changes
```

### Root Cause:
The `game-ci/unity-builder@v4` action was using semantic versioning by default, which requires a clean git working directory. However, the workflow:
1. Downloads build artifacts
2. Copies libraries to `Assets/Plugins/`
3. Modifies `package.json` and `metadata.json`

All these changes happen **before** the Unity build step, making the branch "dirty" with uncommitted changes.

## Solutions Applied

### Fix #1: Disable Semantic Versioning
Since we're already managing versions manually in the "Get Version" step, we don't need semantic versioning:

```yaml
# OLD
- name: Install Unity
  uses: game-ci/unity-builder@v4
  with:
    unityVersion: ${{ env.UNITY_VERSION }}
    targetPlatform: WebGL
    buildMethod: UnityEditor.BuildPipeline.BuildPlayer

# NEW
- name: Install Unity
  uses: game-ci/unity-builder@v4
  with:
    unityVersion: ${{ env.UNITY_VERSION }}
    targetPlatform: WebGL
    buildMethod: UnityEditor.BuildPipeline.BuildPlayer
    versioning: None  # ? Added this
```

### Fix #2: Improve Artifact Organization
The previous step was using a wildcard copy that might create files in wrong locations:

```bash
# OLD - Could copy artifacts incorrectly
for dir in artifacts/*/; do
  if [ "$dir" != "artifacts/ios-libraries/" ]; then
    cp -R "$dir"* Assets/Plugins/
  fi
done

# NEW - Explicit platform-specific copying
if [ -d "artifacts/Windows-library" ]; then
  mkdir -p Assets/Plugins/x86_64
  cp -R artifacts/Windows-library/Assets/Plugins/x86_64/* Assets/Plugins/x86_64/
fi
# (Same pattern for macOS and Android)
```

## Benefits

1. ? **No More Semantic Versioning Errors**: Unity builder won't complain about dirty branch
2. ? **Better Version Control**: We explicitly control the version via workflow inputs or git tags
3. ? **Cleaner File Organization**: Libraries go to correct platform directories
4. ? **Easier Debugging**: Explicit copy steps show exactly what's happening

## Version Strategy

Our workflow already handles versioning properly:

```yaml
- name: Get Version
  id: version
  run: |
    if [ "${{ github.event_name }}" = "workflow_dispatch" ] && [ -n "${{ github.event.inputs.version }}" ]; then
      VERSION="${{ github.event.inputs.version }}"  # Manual dispatch
    elif [[ $GITHUB_REF == refs/tags/v* ]]; then
      VERSION=${GITHUB_REF#refs/tags/v}  # From git tag
    else
      VERSION=$(date +%Y.%m.%d)-$(git rev-parse --short HEAD)  # Auto-generated
    fi
```

This is then used to update `package.json` and name the Unity package file.

## Files Modified

```
Modified:
  .github/workflows/build-and-package.yml
    - Added versioning: None to Unity builder
    - Improved artifact organization logic
```

## Expected Result

Unity build step should now proceed without errors:

```
? Downloaded artifacts
? Organized plugin files
? Got version
? Updated package version
? Unity build started (no semantic versioning error)
? Unity package exported
? Artifacts uploaded
```

## Commit This Fix

```bash
git add .github/workflows/build-and-package.yml
git commit -m "fix(ci): disable semantic versioning and improve artifact organization

- Set versioning: None in unity-builder to avoid dirty branch error
- Improve artifact organization with explicit platform paths
- Prevent random files from being copied to wrong locations
- We manage versioning manually via workflow inputs/tags"

git push origin feature/store-version
```

---

**Status**: Ready to commit! This should resolve the semantic versioning error. ??
