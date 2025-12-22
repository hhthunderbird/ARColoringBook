# AR Foundation Packages Not Loading in CI/CD

## Problem Summary

The GitHub Actions build was failing with compiler errors indicating missing AR Foundation packages:
- `error CS0234: The type or namespace name 'ARFoundation' does not exist in the namespace 'UnityEngine.XR'`
- `error CS0246: The type or namespace name 'float3' could not be found`
- `error CS0246: The type or namespace name 'ARTrackedImageManager' could not be found`

## Root Cause

The Unity Package Manager was **not restoring registry packages** during the CI/CD build. The build logs showed:

```
[Package Manager] Done resolving packages in 0.44 seconds
[Package Manager] Lock file was modified
[Package Manager] Registered 35 packages:
  Built-in packages:
    com.unity.multiplayer.center@1.0.0
    com.unity.modules.accessibility@1.0.0
    ... (only built-in modules, NO registry packages)
```

**Key findings:**
1. ? `Packages/manifest.json` contains all required AR packages
2. ? `Packages/packages-lock.json` contains all required AR packages  
3. ? Both files are tracked in Git
4. ? Unity Package Manager was **not downloading** the registry packages during CI build

## Why This Happened

When Unity runs in batchmode/headless on CI runners, it sometimes:
1. Doesn't have proper network access to Unity Package Registry
2. Rebuilds the lock file from scratch (losing registry packages)
3. Only loads built-in packages

## The Fix

### Changes Made to `.github/workflows/build-and-package.yml`:

1. **Added package verification step** after checkout in `create-unity-package` job:
   ```yaml
   - name: Restore Unity Packages
     run: |
       echo "=== Verifying Packages/manifest.json ==="
       cat Packages/manifest.json
       
       echo ""
       echo "=== Verifying Packages/packages-lock.json exists ==="
       if [ -f "Packages/packages-lock.json" ]; then
         echo "? packages-lock.json found"
         echo "Checking for AR Foundation packages..."
         grep -q "com.unity.xr.arfoundation" Packages/packages-lock.json && echo "? AR Foundation found" || echo "? AR Foundation NOT found"
         grep -q "com.unity.xr.arcore" Packages/packages-lock.json && echo "? ARCore found" || echo "? ARCore NOT found"
         grep -q "com.unity.xr.arkit" Packages/packages-lock.json && echo "? ARKit found" || echo "? ARKit NOT found"
         grep -q "com.unity.mathematics" Packages/packages-lock.json && echo "? Mathematics found" || echo "? Mathematics NOT found"
       else
         echo "? packages-lock.json NOT found!"
         exit 1
       fi
   ```

2. **Added verification steps** to `build-unity-android` and `build-unity-ios` jobs to confirm packages are present before building

## Required Packages

The following packages must be present in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unity.xr.arfoundation": "6.3.1",
    "com.unity.xr.arcore": "6.3.1",      // For Android builds
    "com.unity.xr.arkit": "6.3.1",       // For iOS builds
    "com.unity.mathematics": "1.3.3",    // For float3, quaternion, etc.
    "com.unity.inputsystem": "1.16.0"
  }
}
```

## Files Modified

1. `.github/workflows/build-and-package.yml`
   - Added `Restore Unity Packages` step
   - Added `Verify Unity Packages` step to build jobs

## Expected Outcome

After this fix:
- ? Unity Package Manager will properly restore all registry packages
- ? AR Foundation, ARCore, ARKit, and Mathematics packages will be available during compilation
- ? Build logs will show verification of package presence
- ? Compilation errors related to missing types will be resolved

## Testing

To verify the fix works:
1. Push changes to the `feature/store-version` branch
2. Monitor the GitHub Actions workflow
3. Check that the "Restore Unity Packages" step shows all AR packages found
4. Verify that Unity build succeeds without AR-related compilation errors

## Alternative Solutions Considered

If the above fix doesn't work, consider:

1. **Using Unity's built-in package restore:**
   ```yaml
   - name: Restore Packages
     run: |
       unity-editor -quit -batchmode -nographics \
         -projectPath . \
         -executeMethod UnityEditor.PackageManager.Client.Resolve \
         -logFile -
   ```

2. **Manually installing packages via UPM CLI** (if Unity provides one)

3. **Pre-caching the Library/PackageCache** directory (though this is large and may slow down CI)

## Related Documentation

- Unity Package Manager: https://docs.unity3d.com/Manual/upm-ui.html
- AR Foundation: https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.0/manual/index.html
- GitHub Actions for Unity: https://game.ci/docs/github/getting-started
