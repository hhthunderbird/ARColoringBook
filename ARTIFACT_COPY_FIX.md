# Artifact Copy Path Fix

## Issue
```
cp: cannot stat 'artifacts/Windows-library/Assets/Plugins/x86_64/*': No such file or directory
```

## Root Cause
The artifact upload/download doesn't preserve the full directory structure. When we upload:

```yaml
- name: Upload Artifact
  uses: actions/upload-artifact@v4
  with:
    name: Windows-library
    path: Assets/Plugins/x86_64/
```

The artifact stores **only** the contents of `Assets/Plugins/x86_64/`, not the full path. When downloaded:

```
artifacts/
??? Windows-library/
    ??? Felina.dll  ? File is directly here, not in nested Assets/Plugins/x86_64/
```

## Solution
Copy directly from the artifact root, not from a nested path:

```bash
# WRONG ?
cp -R artifacts/Windows-library/Assets/Plugins/x86_64/* Assets/Plugins/x86_64/

# CORRECT ?
cp -R artifacts/Windows-library/* Assets/Plugins/x86_64/
```

## Fixed Code

```yaml
- name: Organize Plugin Files
  run: |
    echo "=== Artifact directory structure ==="
    find artifacts -type f || true
    
    # Copy iOS libraries
    if [ -d "artifacts/ios-libraries" ]; then
      mkdir -p Assets/Plugins/iOS
      cp -R artifacts/ios-libraries/* Assets/Plugins/iOS/
      echo "? Copied iOS libraries"
    fi
    
    # Copy Windows library (directly from artifact root)
    if [ -d "artifacts/Windows-library" ]; then
      mkdir -p Assets/Plugins/x86_64
      cp -R artifacts/Windows-library/* Assets/Plugins/x86_64/
      echo "? Copied Windows libraries"
    fi
    
    # Copy macOS library
    if [ -d "artifacts/macOS-library" ]; then
      mkdir -p Assets/Plugins/macOS
      cp -R artifacts/macOS-library/* Assets/Plugins/macOS/
      echo "? Copied macOS libraries"
    fi
    
    # Copy Android library
    if [ -d "artifacts/Android-library" ]; then
      mkdir -p Assets/Plugins/Android/libs/arm64-v8a
      cp -R artifacts/Android-library/* Assets/Plugins/Android/libs/arm64-v8a/
      echo "? Copied Android libraries"
    fi
```

## Key Changes
1. ? Added debug output: `find artifacts -type f`
2. ? Fixed copy paths: Copy from artifact root, not nested path
3. ? Applied same fix to all platforms (Windows, macOS, Android)
4. ? iOS was already correct

## Expected Output
```
=== Artifact directory structure ===
artifacts/ios-libraries/Felina.xcframework/Info.plist
artifacts/ios-libraries/Felina.xcframework/ios-arm64/libFelina.a
artifacts/ios-libraries/Felina.xcframework/ios-arm64-simulator/libFelina.a
artifacts/ios-libraries/Felina.xcframework.meta
artifacts/Windows-library/Felina.dll
artifacts/macOS-library/libFelina.dylib
artifacts/Android-library/libFelina.so

? Copied iOS libraries
? Copied Windows libraries
? Copied macOS libraries
? Copied Android libraries
? All libraries organized in Assets/Plugins/

=== Plugin directory structure ===
Assets/Plugins/iOS/Felina.xcframework/Info.plist
Assets/Plugins/iOS/Felina.xcframework/ios-arm64/libFelina.a
Assets/Plugins/iOS/Felina.xcframework/ios-arm64-simulator/libFelina.a
Assets/Plugins/iOS/Felina.xcframework.meta
Assets/Plugins/x86_64/Felina.dll
Assets/Plugins/macOS/libFelina.dylib
Assets/Plugins/Android/libs/arm64-v8a/libFelina.so
```

## Commit
```bash
git add .github/workflows/build-and-package.yml UNITY_BUILD_FIX.md
git commit -m "fix(ci): correct artifact copy paths in organize step

- Artifacts don't preserve full directory structure
- Copy directly from artifact root, not nested paths
- Add debug output to show artifact structure
- Fix applies to Windows, macOS, and Android libraries"

git push origin feature/store-version
```

---
**Status**: Ready to fix! This should resolve the artifact copy error. ??
