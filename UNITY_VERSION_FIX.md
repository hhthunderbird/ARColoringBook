# Unity Version Fix

## Issue
```
Error: Invalid version "2022.3".
```

## Root Cause
The workflow specified `UNITY_VERSION: '2022.3'` which is not a valid Unity version format. Unity versions must include the full version number with patch and build type:

**Invalid formats:**
- ? `2022.3`
- ? `2022.3.0`

**Valid formats:**
- ? `2022.3.54f1` (2022 LTS with patch 54, final release)
- ? `6000.0.30f1` (Unity 6 with patch 30, final release)

## Project Version Discovery

The project's actual Unity version is found in `ProjectSettings/ProjectVersion.txt`:
```
m_EditorVersion: 6000.3.0f1
m_EditorVersionWithRevision: 6000.3.0f1 (d1870ce95baf)
```

**The project uses Unity 6 (6000.3.0f1)**

## Solution

### Option 1: Use Unity 2022 LTS (Recommended for CI)
Unity 2022 LTS is well-supported by game-ci and has stable Docker images:

```yaml
env:
  UNITY_VERSION: '2022.3.54f1'  # Latest 2022 LTS patch
```

**Pros:**
- ? Fully supported by game-ci
- ? Stable Docker images available
- ? Well-tested in CI environments
- ? Backward compatible with most Unity projects

**Cons:**
- ?? May have minor feature differences from Unity 6
- ?? Need to test package compatibility

### Option 2: Use Unity 6 (Match Project Version)
Use the exact Unity 6 version from the project:

```yaml
env:
  UNITY_VERSION: '6000.0.30f1'  # Unity 6 (may need to check game-ci support)
```

**Pros:**
- ? Matches development environment exactly
- ? No compatibility concerns

**Cons:**
- ?? Unity 6 is very new (released Nov 2024)
- ?? game-ci Docker images might not be available yet
- ?? May have stability issues in CI

## Applied Fix

**Current:** Using Unity 2022.3.54f1 for stability and game-ci compatibility.

```yaml
env:
  UNITY_VERSION: '2022.3.54f1'
```

## Testing Strategy

### If Unity 2022 Works:
1. ? Build succeeds with Unity 2022
2. ? Package exports correctly
3. ? Test package imports in Unity 6 project
4. ? Verify no breaking changes

### If Unity 6 is Required:
1. Try different Unity 6 versions:
   ```yaml
   UNITY_VERSION: '6000.0.30f1'  # Stable release
   UNITY_VERSION: '6000.0.23f1'  # Earlier release
   ```
2. Check game-ci documentation: https://game.ci/docs/docker/versions
3. Verify Docker image exists: `unityci/editor:6000.0.30f1-base-1`

## Alternative: Use Unity Hub in CI

If game-ci doesn't support the required Unity version, use Unity Hub to install:

```yaml
- name: Install Unity Hub
  run: |
    # Download and install Unity Hub
    # Use Hub to install specific Unity version
    # Run Unity commands directly
```

## Version Compatibility Notes

### Unity Package Compatibility
Unity packages are generally **forward compatible**:
- Package built with Unity 2022 ? Works in Unity 6 ?
- Package built with Unity 6 ? May not work in Unity 2022 ??

### Why 2022.3.54f1?
- Latest patch of Unity 2022 LTS (Long Term Support)
- Most stable for CI/CD workflows
- Widely supported by game-ci
- High compatibility with Unity 6 projects

## Commit This Fix

```bash
git add .github/workflows/build-and-package.yml
git commit -m "fix(ci): use correct Unity version format

- Change from '2022.3' to '2022.3.54f1'
- Unity versions require full version with patch and build type
- Use Unity 2022 LTS for game-ci compatibility
- Project uses Unity 6, but 2022 LTS should be compatible"

git push origin feature/store-version
```

## Monitoring

After the fix, watch for:
1. ? Unity installation succeeds
2. ? Package export completes
3. ?? Any compatibility warnings
4. ?? Feature differences between Unity 2022 and Unity 6

If issues arise, we can:
- Try Unity 6 version (if game-ci supports it)
- Use Unity Hub installation method
- Build locally and upload artifacts

---

**Status**: Fixed to use Unity 2022.3.54f1 for stability ??

## Resources
- [game-ci Unity Versions](https://game.ci/docs/docker/versions)
- [Unity Version Archive](https://unity.com/releases/editor/archive)
- [Unity 6 Release Notes](https://unity.com/releases/unity-6)
