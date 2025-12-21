# Workflow Fix Validation Checklist

## Pre-Commit Checks

- [x] Fixed trailing backslash in macOS output filename
- [x] Created iOS CMake toolchain file
- [x] Updated build process to use CMAKE_INSTALL_PREFIX
- [x] Added ninja install step to all platform builds
- [x] Updated copy logic to handle install directories

## Files to Commit

```bash
# Modified files
modified:   .github/workflows/build-and-package.yml

# New files
new file:   FelinaLibrary/cmake/ios.toolchain.cmake
new file:   FelinaLibrary/cmake/README.md
new file:   WORKFLOW_FIXES_COMPLETE.md
```

## Local Testing (Optional but Recommended)

### Test iOS Toolchain
```bash
cd FelinaLibrary
mkdir -p build/test-ios && cd build/test-ios
cmake ../.. -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=../../cmake/ios.toolchain.cmake \
  -DPLATFORM=OS64 \
  -DCMAKE_BUILD_TYPE=Release
# Should configure without errors
```

### Test macOS Build
```bash
cd FelinaLibrary
mkdir -p build/test-macos && cd build/test-macos
cmake ../.. -G Ninja -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_INSTALL_PREFIX=../../install/test-macos
ninja
ninja install
# Check install/test-macos/lib/libFelina.dylib exists
ls -la ../../install/test-macos/lib/
```

## Commit and Push

```bash
# Stage changes
git add .github/workflows/build-and-package.yml
git add FelinaLibrary/cmake/
git add WORKFLOW_FIXES_COMPLETE.md

# Commit with descriptive message
git commit -m "fix(ci): resolve GitHub Actions build failures

- Fix macOS library build path mismatch by using CMAKE_INSTALL_PREFIX
- Remove trailing backslash from macOS output filename in workflow matrix
- Add missing iOS CMake toolchain configuration (ios.toolchain.cmake)
- Update all platform builds to use install directory approach
- Add fallback logic for Windows DLL location (bin/ or lib/)
- Add documentation for cmake toolchain usage

Resolves build errors:
- cp: FelinaLibrary/build/macOS/libFelina.dylib: No such file or directory
- CMake Error: Could not find toolchain file
- cp exit code 64 due to invalid filename"

# Push to remote
git push origin feature/store-version
```

## Post-Push Verification

### 1. Monitor GitHub Actions
- Go to: https://github.com/hhthunderbird/ARColoringBook/actions
- Watch the workflow run on `feature/store-version` branch
- Verify all jobs complete successfully:
  - ? build-ios-library
  - ? build-other-platforms (Windows, macOS, Android)
  - ? create-unity-package
  - ? test-package

### 2. Check Artifacts
After successful workflow run:
- Download `ios-libraries` artifact
- Download `Windows-library` artifact
- Download `macOS-library` artifact
- Download `Android-library` artifact
- Download `unity-package` artifact

Verify contents:
```bash
# iOS libraries should contain:
Assets/Plugins/iOS/Felina.xcframework/
Assets/Plugins/iOS/libFelina.a

# macOS library should contain:
Assets/Plugins/macOS/libFelina.dylib

# Windows library should contain:
Assets/Plugins/x86_64/Felina.dll

# Android library should contain:
Assets/Plugins/Android/libs/arm64-v8a/libFelina.so

# Unity package should contain:
FelinaARColoringBook_v{version}.unitypackage
FelinaARColoringBook_v{version}.unitypackage.sha256
```

### 3. Test Unity Package (Optional)
```bash
# Download the unity package artifact
# Import into a test Unity project
# Verify no import errors
# Check that plugins are properly configured for each platform
```

## Success Criteria

- [ ] All GitHub Actions jobs pass
- [ ] All platform libraries are built and uploaded as artifacts
- [ ] iOS XCFramework is created successfully
- [ ] Unity package is created and includes all libraries
- [ ] No build errors in workflow logs
- [ ] Artifact files have correct sizes (not empty)

## Rollback Plan (If Needed)

If the workflow still fails:
```bash
# Revert changes
git revert HEAD

# Or reset to previous commit
git reset --hard HEAD~1
git push origin feature/store-version --force

# Then investigate and fix issues locally before pushing again
```

## Additional Notes

- The iOS build requires macOS runner (already configured)
- Windows build requires MSVC (already configured with ilammy/msvc-dev-cmd@v1)
- Android build requires cmake and ninja (already configured)
- All builds now use consistent install-prefix approach
- Libraries are isolated in install/{platform} directories

## Contact

If issues persist:
1. Check workflow logs in GitHub Actions
2. Review error messages carefully
3. Ensure all required tools are available in runners
4. Verify CMakeLists.txt compatibility with toolchain files
