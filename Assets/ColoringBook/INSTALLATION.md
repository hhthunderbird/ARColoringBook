# Felina AR Coloring Book - Installation Guide

**Get up and running in 10 minutes with step-by-step instructions for your Unity version.**

## ?? Table of Contents

- [Who Should Read This](#-who-should-read-this)
- [Prerequisites](#-prerequisites)
- [Quick Start](#-quick-start-for-experienced-users)
- [Detailed Installation](#-detailed-installation)
  - [Unity 2019.4](#for-unity-20194)
  - [Unity 2020.3 LTS](#for-unity-20203-lts)
  - [Unity 2021.3 LTS](#for-unity-20213-lts)
  - [Unity 2022.3 LTS](#for-unity-20223-lts-recommended)
  - [Unity 2023.x / Unity 6.x](#for-unity-60-2023x)
- [Post-Installation](#-post-installation-verification)
- [Platform Configuration](#-platform-configuration)
- [Troubleshooting](#-troubleshooting-installation-issues)
- [Next Steps](#-next-steps)

---

## ?? Who Should Read This?

**This guide is for you if:**
- ? You're installing Felina AR Coloring Book for the first time
- ? You're switching Unity versions
- ? You're experiencing installation issues
- ? You want to understand version requirements

**Skip to Quick Start if:**
- ? You're experienced with Unity packages
- ? You know your Unity and AR Foundation versions
- ? You just need a checklist

---

## ? Prerequisites

### Before You Begin

**Check these requirements first:**

1. **Unity Installation**
   - Unity 2019.4 or later installed
   - Check your version: `Unity Hub > Installs` or `Help > About Unity`
   
2. **Platform Modules**
   - iOS Build Support (if targeting iOS)
   - Android Build Support (if targeting Android)
   - Add via Unity Hub: `Installs > [Your Version] > Gear Icon > Add Modules`

3. **Development Tools** (for building to devices)
   - **iOS**: macOS with Xcode 11+ installed
   - **Android**: Android SDK installed (via Unity Hub or standalone)
   - **Windows**: Visual Studio 2019+ with C++ tools (for native plugins)

4. **Target Device** (for testing)
   - ARKit-compatible iOS device (iPhone 6S+)
   - ARCore-compatible Android device ([check list](https://developers.google.com/ar/devices))

### Knowledge Requirements

**Helpful to know** (but not required):
- Basic Unity Editor navigation
- Understanding of GameObjects and Components
- Familiarity with Package Manager

**Don't worry if you're new!** This guide explains everything step-by-step.

---

## ? Quick Start (For Experienced Users)

**If you know what you're doing, here's the checklist:**

### 1-Minute Installation

```bash
# 1. Import Package
Window > Package Manager > + > Add package from disk
> Select: Assets/ColouringBook/package.json

# 2. Install AR Foundation
Window > Package Manager > Unity Registry > Search "AR Foundation"
> Install version matching your Unity (see table below)

# 3. Install Platform Plugin
> ARKit XR Plugin (iOS) - same version as AR Foundation
> ARCore XR Plugin (Android) - same version as AR Foundation

# 4. Enable XR
Edit > Project Settings > XR Plug-in Management
> iOS tab: Enable ARKit
> Android tab: Enable ARCore

# 5. Validate
Felina > Validate Package Setup
```

### Version Quick Reference

| Unity Version | AR Foundation | ARKit/ARCore Plugin |
|---------------|---------------|---------------------|
| 2019.4 | 2.1.18 | 2.1.18 (ARKit) / 2.1.23 (ARCore) |
| 2020.3 LTS | 4.1.13 | 4.1.13 |
| 2021.3 LTS | 4.2.10 | 4.2.10 |
| **2022.3 LTS** | **5.1.5** | **5.1.5** ? |
| 2023.x / 6.x | 6.0.0+ | 6.0.0+ |

? **Done?** Jump to [Post-Installation Verification](#-post-installation-verification)

---

## ?? Detailed Installation

**Choose your Unity version below for step-by-step instructions:**

## Multi-Version Unity Support (2019.4 - 6.3+)

This package is designed to work across multiple Unity versions with automatic version detection. Follow the steps below for your Unity version.

### Step 1: Install Core Dependencies

**All Unity Versions:**
1. Open **Window > Package Manager**
2. Click **+** ? **Add package by name**
3. Add: `com.unity.mathematics` (any version compatible with your Unity)

### Step 2: Install AR Foundation (Version-Specific)

#### For Unity 2019.4:
```
- com.unity.xr.arfoundation: 2.1.18
- com.unity.xr.arkit: 2.1.18 (iOS)
- com.unity.xr.arcore: 2.1.23 (Android)
```

#### For Unity 2020.3 LTS:
```
- com.unity.xr.arfoundation: 4.1.13
- com.unity.xr.arkit: 4.1.13 (iOS)
- com.unity.xr.arcore: 4.1.13 (Android)
```

#### For Unity 2021.3 LTS:
```
- com.unity.xr.arfoundation: 4.2.10
- com.unity.xr.arkit: 4.2.10 (iOS)
- com.unity.xr.arcore: 4.2.10 (Android)
```

#### For Unity 2022.3 LTS:
```
- com.unity.xr.arfoundation: 5.1.5
- com.unity.xr.arkit: 5.1.5 (iOS)
- com.unity.xr.arcore: 5.1.5 (Android)
```

#### For Unity 6.0+ (2023.x+):
```
- com.unity.xr.arfoundation: 6.3.1
- com.unity.xr.arkit: 6.3.1 (iOS)
- com.unity.xr.arcore: 6.3.1 (Android)
```

### Step 3: Import Felina AR Coloring Book Package

**Option A: Via Package Manager (Recommended)**
1. Open **Window > Package Manager**
2. Click **+** ? **Add package from disk**
3. Navigate to `Assets/ColouringBook/package.json`
4. Click **Open**

**Option B: Manual Import**
1. Copy the entire `Assets/ColouringBook` folder to your project
2. Wait for Unity to import and compile

---

## Automated Setup (Recommended)

After importing, run the setup validator:
1. Go to **Felina > Validate Package Setup** (menu will be available after import)
2. The tool will:
   - ? Detect your Unity version
   - ? Check installed AR Foundation version
   - ? Verify all dependencies
   - ? Show installation instructions if anything is missing

---

## Version-Specific Setup

### Unity 2019.4 Specifics

**Required Packages:**
- AR Foundation 2.1.x
- ARKit XR Plugin 2.1.x (iOS)
- ARCore XR Plugin 2.1.x (Android)
- Unity Mathematics 1.2.1+

**Platform Setup:**
- **iOS**: Install Xcode 11+, set iOS deployment target to 11.0+
- **Android**: Install Android SDK, set minimum API level to 24

**Known Limitations:**
- `FindFirstObjectByType` not available (uses `FindObjectOfType`)
- `Transform.GetPositionAndRotation` not available (uses manual get)
- `NativeReference<T>` not available (uses `NativeArray<T>`)
- Old AR Foundation event system (`trackedImagesChanged`)

### Unity 2020.3 Specifics

**Required Packages:**
- AR Foundation 4.1.x
- ARKit XR Plugin 4.1.x (iOS)
- ARCore XR Plugin 4.1.x (Android)
- Unity Mathematics 1.2.1+

**Platform Setup:**
- **iOS**: Xcode 12+, iOS 11.0+
- **Android**: API Level 24+

**Features Available:**
- New AR Foundation event system (`trackablesChanged`)
- Improved image tracking stability
- Better reference image library management

### Unity 2021.3+ Specifics

**Required Packages:**
- AR Foundation 4.2.x / 5.x
- ARKit XR Plugin (matching version)
- ARCore XR Plugin (matching version)
- Unity Mathematics 1.2.1+

**Platform Setup:**
- **iOS**: Xcode 13+, iOS 11.0+
- **Android**: API Level 24+

**Features Available:**
- `NativeReference<T>` support
- `Transform.SetLocalPositionAndRotation`
- Improved Burst compilation

### Unity 6.0+ (2023.x+) Specifics

**Required Packages:**
- AR Foundation 6.x
- ARKit XR Plugin 6.x (iOS)
- ARCore XR Plugin 6.x (Android)
- Unity Mathematics 1.3.3+

**Platform Setup:**
- **iOS**: Xcode 14+, iOS 12.0+
- **Android**: API Level 24+

**Features Available:**
- `FindFirstObjectByType`
- Latest AR Foundation APIs
- XR Origin (replaces ARSessionOrigin)
- Improved performance and stability

---

## Handling Sample Scripts from AR Foundation

If you see errors about sample scripts (from `AR Foundation Samples` repository):

### Option 1: Delete Sample Scripts (Recommended if not using them)
```
Delete these folders if present:
- Assets/Samples/
- Assets/Scripts/Runtime/AR Foundation Samples/
```

### Option 2: Install AR Foundation Samples Package
1. Open **Window > Package Manager**
2. Find **AR Foundation**
3. Expand **Samples** section
4. Import the samples you need
5. This will place them in `Assets/Samples/AR Foundation/[version]/`

### Option 3: Update Sample Scripts
If samples are already imported from an older version:
1. Delete the old samples: `Assets/Samples/AR Foundation/`
2. Re-import from Package Manager for your current AR Foundation version

---

## Common Issues & Solutions

### Issue: "ARFoundation types not found"
**Solution:** Install AR Foundation package for your Unity version (see Step 2 above)

### Issue: "Sample scripts have errors"
**Solution:** Either delete sample folders or re-import matching AR Foundation version samples

### Issue: "NativeReference not found" (Unity 2019.4-2021.1)
**Solution:** This is expected - the code automatically uses `NativeArray` fallback

### Issue: "trackablesChanged not found" (Unity 2019.4)
**Solution:** This is expected - the code automatically uses `trackedImagesChanged`

### Issue: "XR Origin not found" (Unity 2019.4-2022.x)
**Solution:** This is expected - use `ARSessionOrigin` instead (code handles this)

### Issue: Package compiles but AR doesn't work
**Solution:**
1. Check **Edit > Project Settings > XR Plug-in Management**
2. Enable ARKit (iOS) or ARCore (Android)
3. Configure build settings for mobile platform

---

## Verification Checklist

After installation, verify:

- [ ] Package Manager shows AR Foundation installed
- [ ] Platform-specific plugin (ARKit/ARCore) installed
- [ ] Unity Mathematics package installed
- [ ] No compilation errors in Console
- [ ] XR Plug-in Management configured for target platform
- [ ] Felina menu appears in Unity menu bar

---

## Building for Devices

### iOS
1. Set platform to iOS: **File > Build Settings > iOS**
2. Player Settings:
   - Set minimum iOS version to 11.0+
   - Add camera permission to Info.plist
3. Build and run on device (AR doesn't work in simulator)

### Android
1. Set platform to Android: **File > Build Settings > Android**
2. Player Settings:
   - Set minimum API level to 24
   - Add camera permission to AndroidManifest.xml
3. Build and run on ARCore-compatible device

---

## Getting Help

If you encounter issues:

1. **Check Unity Version Compatibility**
   - Run **Felina > Validate Package Setup**
   - Verify AR Foundation version matches your Unity version

2. **Check Console for Specific Errors**
   - Look for Felina-prefixed log messages
   - Check for missing dependency errors

3. **Verify Platform Setup**
   - Ensure XR Plug-in Management is configured
   - Check build settings for mobile platform

4. **Documentation**
   - See `AR_SETUP_UNITY_2019.md` for Unity 2019.4 specifics
   - See `CONTROLLER_DEVICE_MONITOR.md` for XR Origin setup

---

## Support

- **GitHub**: https://github.com/hhthunderbird/ARColoringBook
- **Documentation**: See `Assets/ColouringBook/README.md`
- **Issues**: GitHub Issues page

---

**Package Version**: 1.0.0  
**Unity Support**: 2019.4.41 - 6.3+  
**AR Foundation**: 2.x - 6.x (auto-detected)
