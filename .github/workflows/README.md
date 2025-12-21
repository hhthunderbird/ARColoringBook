# GitHub Actions CI/CD

This directory contains GitHub Actions workflows for automated building and packaging.

## Workflows

### ?? build-and-package.yml

Complete CI/CD pipeline that:
1. **Builds iOS native library** (.a for device and simulator)
2. **Builds other platform libraries** (Windows .dll, macOS .dylib, Android .so)
3. **Creates Unity package** (.unitypackage)
4. **Tests package import** in fresh Unity project
5. **Creates GitHub release** (on version tags)

## Triggers

```yaml
# Automatic triggers
- Push to main or feature/store-version branches
- Pull requests to main
- Git tags starting with 'v' (e.g., v1.0.0)

# Manual trigger
- Workflow dispatch from Actions tab
```

## Secrets Required

Configure these in GitHub Settings > Secrets and variables > Actions:

| Secret | Description | Required For |
|--------|-------------|--------------|
| `UNITY_LICENSE` | Unity license file content | Unity package export |
| `UNITY_EMAIL` | Unity account email | Unity activation |
| `UNITY_PASSWORD` | Unity account password | Unity activation |

### Getting Unity License

```bash
# Method 1: From Unity Hub
# 1. Open Unity Hub
# 2. Settings > Licenses
# 3. Click on your license > Show in Finder/Explorer
# 4. Copy file content to UNITY_LICENSE secret

# Method 2: Generate for CI
# Follow: https://game.ci/docs/github/activation
```

## Usage

### Automatic Build (on push)

```bash
git add .
git commit -m "feat: Add new feature"
git push origin main
# Workflow runs automatically
```

### Create Release

```bash
# Tag version
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0

# Workflow creates:
# - Builds for all platforms
# - Unity package
# - GitHub release with assets
```

### Manual Build

1. Go to GitHub > Actions
2. Select "Build iOS Library and Create Unity Package"
3. Click "Run workflow"
4. Enter version (e.g., 1.0.1)
5. Click "Run workflow"

## Artifacts

Each workflow run produces:

### Per-Platform Artifacts (30 days retention)
- `ios-libraries` - iOS .a + .xcframework
- `Windows-library` - Windows .dll
- `macOS-library` - macOS .dylib
- `Android-library` - Android .so

### Package Artifacts (90 days retention)
- `unity-package` - .unitypackage + SHA256 checksum

### Test Results (7 days retention)
- `test-results` - Unity import logs

## Download Artifacts

```bash
# Via GitHub CLI
gh run download <run-id>

# Via web
# Go to Actions > Workflow run > Artifacts section
```

## Local Testing

### Test iOS Build Locally

```bash
# Prerequisites
# - macOS with Xcode installed
# - CMake and Ninja installed (brew install cmake ninja)

cd FelinaLibrary
mkdir -p build/ios
cd build/ios

# Configure
cmake ../.. \
  -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=../../cmake/ios.toolchain.cmake \
  -DPLATFORM=OS64 \
  -DCMAKE_BUILD_TYPE=Release

# Build
ninja
```

### Test Unity Package Export

```bash
# Open Unity project
# Window > Package Manager
# + > Add package from disk
# Select Assets/ColouringBook/package.json

# Export package
# Right-click Assets/ColouringBook
# Export Package...
# Save as .unitypackage
```

## Workflow Jobs

### 1. build-ios-library
- **Platform**: macOS runner
- **Steps**:
  1. Setup Xcode
  2. Install CMake/Ninja
  3. Build iOS device library (arm64)
  4. Build iOS simulator library (arm64 + x86_64)
  5. Create XCFramework
  6. Create fat library (fallback)
  7. Copy to Assets/Plugins/iOS/
  8. Generate .meta files
  9. Commit changes (if on main)

### 2. build-other-platforms
- **Strategy**: Matrix build
- **Platforms**: Windows, macOS, Android
- **Steps**:
  1. Setup platform-specific tools
  2. Build native library
  3. Copy to Assets/Plugins/{Platform}/
  4. Upload artifacts

### 3. create-unity-package
- **Dependencies**: build-ios-library, build-other-platforms
- **Steps**:
  1. Download all platform libraries
  2. Organize in Plugins folder
  3. Update version numbers
  4. Install Unity
  5. Export .unitypackage
  6. Create SHA256 checksum
  7. Upload package artifact
  8. Create GitHub release (if tagged)

### 4. test-package
- **Dependencies**: create-unity-package
- **Steps**:
  1. Create empty Unity project
  2. Download package
  3. Import package
  4. Check for errors
  5. Upload test logs

## Troubleshooting

### Unity License Issues

```bash
# Error: "License is not valid"
# Solution: Regenerate Unity license for CI
# Follow: https://game.ci/docs/github/activation
```

### iOS Build Fails

```bash
# Error: "xcode-select: error: tool 'xcodebuild' requires Xcode"
# Solution: Ensure using macos-latest runner
```

### CMake Toolchain Not Found

```bash
# Error: "Cannot find ios.toolchain.cmake"
# Solution: Ensure cmake/ folder is committed
git add cmake/ios.toolchain.cmake
git commit -m "Add iOS CMake toolchain"
```

### Package Export Fails

```bash
# Error: "Could not find UnityEditor"
# Solution: Check Unity version matches UNITY_VERSION in workflow
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_VERSION` | 2022.3 | Unity version to use |
| `PACKAGE_NAME` | FelinaARColoringBook | Package file name |
| `IOS_LIBRARY_NAME` | Felina | iOS library name |

## Customization

### Change Unity Version

```yaml
env:
  UNITY_VERSION: '2023.1'  # Change to your version
```

### Add New Platform

```yaml
- os: ubuntu-latest
  platform: Linux
  output: libFelina.so
  plugin_path: Assets/Plugins/Linux
```

### Skip Tests

```yaml
# Comment out test-package job
# test-package:
#   name: Test Unity Package Import
#   ...
```

## Performance

**Typical Workflow Duration**:
- iOS build: ~5-10 minutes
- Other platforms: ~3-5 minutes each
- Unity package: ~5-10 minutes
- Tests: ~3-5 minutes
- **Total**: ~20-30 minutes

**Optimization**:
- Build caching enabled (saves ~30%)
- Parallel matrix builds
- Artifact compression

## Support

Issues with GitHub Actions:
- Check workflow logs
- File issue: https://github.com/hhthunderbird/ARColoringBook/issues
- Email: support@felina.dev

---

Last Updated: 2024-01-15
