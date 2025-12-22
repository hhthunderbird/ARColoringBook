# Export Script Not Executing - Diagnosis

## Issue Identified

The Unity build runs but the export script is **never executed**:

```
=== Checking for log files ===
Found log: ./Logs/Packages-Update.log
[... package updates ...]

=== Checking for Builds directory ===
No Builds directory  ?
```

**Key observation:** We never see `"=== Starting Package Export ==="` in the logs, which means Unity isn't calling our `ExportPackage.Export()` method.

## Root Cause Analysis

### Why Unity Isn't Calling Our Method:

1. **Script Not Compiled**
   - Unity needs to compile C# scripts before they can be called
   - The `game-ci/unity-builder` action might not be compiling editor scripts
   - Editor scripts require Asset Database to be initialized

2. **Incorrect Build Method Path**
   - Using `buildMethod: ExportPackage.Export`
   - Might need namespace or assembly specification

3. **Build Target Issue**
   - Using `targetPlatform: StandaloneLinux64`
   - This tries to build a game, not export a package
   - Unity might be looking for a scene to build instead of running our method

## Solutions Applied

### 1. Added Verification Step

```yaml
- name: Verify Export Script
  run: |
    if [ -f "Assets/Editor/ExportPackage.cs" ]; then
      echo "? ExportPackage.cs exists"
      head -20 Assets/Editor/ExportPackage.cs
    fi
```

This confirms the script file is created before Unity runs.

### 2. Enhanced Logging

Updated `Check Unity Logs` to specifically search for:
- `"Starting Package Export"` - Did our script run?
- `"Package exported successfully"` - Did export complete?
- `"Asset path does not exist"` - Asset validation errors

### 3. Changed GUID

Changed the meta file GUID from `12345678901234567890123456789012` to `a1b2c3d4e5f6789012345678901234ab` to avoid conflicts.

## Alternative Approaches

### Option 1: Use Unity's Built-in Package Manager (Recommended)

Instead of using `game-ci/unity-builder` with a build method, run Unity directly:

```yaml
- name: Export Unity Package
  run: |
    docker run --rm \
      -v $(pwd):/workspace \
      -e PACKAGE_NAME=${{ env.PACKAGE_NAME }} \
      -e PACKAGE_VERSION=${{ steps.version.outputs.version }} \
      unityci/editor:ubuntu-6000.0.30f1-base-3 \
      unity-editor \
        -quit \
        -batchmode \
        -nographics \
        -projectPath /workspace \
        -executeMethod ExportPackage.Export \
        -logFile -
```

### Option 2: Pre-compile the Script

Add a step to compile scripts before export:

```yaml
- name: Compile Export Script
  uses: game-ci/unity-builder@v4
  with:
    unityVersion: ${{ env.UNITY_VERSION }}
    targetPlatform: StandaloneLinux64
    buildMethod: UnityEditor.AssetDatabase.Refresh
    versioning: None

- name: Export Package
  uses: game-ci/unity-builder@v4
  with:
    buildMethod: ExportPackage.Export
    # ... rest of config
```

### Option 3: Use Different Build Method Format

Try different method call formats:

```yaml
# Option A: With namespace
buildMethod: global::ExportPackage.Export

# Option B: With assembly
buildMethod: Assembly-CSharp-Editor.ExportPackage.Export

# Option C: Full path
buildMethod: Assets.Editor.ExportPackage.Export
```

### Option 4: Export Without Unity Builder

Create the package manually using tar:

```yaml
- name: Create Unity Package (Manual)
  run: |
    # Unity packages are just tar.gz files with specific structure
    cd Assets/ColouringBook
    tar -czf ../../Builds/${PACKAGE_NAME}_v${VERSION}.unitypackage .
```

## Recommended Fix

The most reliable approach is **Option 1** - run Unity directly instead of through unity-builder:

```yaml
- name: Export Unity Package
  run: |
    mkdir -p Builds
    
    docker run --rm \
      --workdir /workspace \
      -v $(pwd):/workspace \
      -e UNITY_EMAIL="${{ secrets.UNITY_EMAIL }}" \
      -e UNITY_PASSWORD="${{ secrets.UNITY_PASSWORD }}" \
      -e UNITY_SERIAL="${{ secrets.UNITY_LICENSE }}" \
      -e PACKAGE_NAME="${{ env.PACKAGE_NAME }}" \
      -e PACKAGE_VERSION="${{ steps.version.outputs.version }}" \
      unityci/editor:ubuntu-6000.0.30f1-base-3 \
      bash -c "
        # Activate Unity
        unity-editor -quit -batchmode -nographics -silent-crashes \
          -logFile - \
          -username \$UNITY_EMAIL \
          -password \$UNITY_PASSWORD \
          -serial \$UNITY_SERIAL

        # Run export
        unity-editor -quit -batchmode -nographics \
          -projectPath /workspace \
          -executeMethod ExportPackage.Export \
          -logFile -
      "
```

## Why game-ci/unity-builder Might Not Work for Package Export

The `game-ci/unity-builder` action is designed for:
- ? Building games
- ? Creating builds for specific platforms
- ? Running tests

It's **not optimized for**:
- ? Exporting Unity packages
- ? Running custom editor scripts
- ? Asset database operations

The action assumes you're building a game and:
1. Looks for scenes to build
2. Runs platform-specific build pipeline
3. May not properly initialize Asset Database for editor operations

## Debugging Checklist

If the script still doesn't run, check:

1. **Is the script created?**
   ```bash
   ls -la Assets/Editor/ExportPackage.cs
   ```

2. **Is Unity finding the method?**
   ```bash
   grep -r "ExportPackage" build/*.log
   ```

3. **Are there compilation errors?**
   ```bash
   grep -i "error" build/*.log | grep -i "ExportPackage"
   ```

4. **Is Unity activating properly?**
   ```bash
   grep -i "license" build/*.log
   ```

5. **What's in the build directory?**
   ```bash
   find build/ -type f
   ```

## Next Steps

1. **Try the recommended fix** (run Unity directly)
2. **Check the verification step** output to confirm script exists
3. **Look for compilation errors** in Unity logs
4. **If still failing**, consider manual tar.gz export (Option 4)

## Commit Updated Changes

```bash
git add .github/workflows/build-and-package.yml
git commit -m "fix(ci): add verification and enhanced logging for export script

- Add verification step to confirm export script exists
- Change meta file GUID to avoid conflicts
- Enhanced logging to search for specific export messages
- List Editor folder contents for debugging
- Check Assets/Editor directory in logs

Next: Consider switching from game-ci/unity-builder to direct Unity execution"

git push origin feature/store-version
```

---

**Status**: Diagnosed issue - export script not being executed by Unity

**Root Cause**: `game-ci/unity-builder` with `buildMethod` may not properly execute editor scripts for package export

**Recommended Solution**: Run Unity directly with `-executeMethod` instead of using unity-builder action
