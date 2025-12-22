# Unity Android and iOS Builds Added

## ? New Jobs Added

I've added **two new jobs** to build Unity applications for Android and iOS platforms:

### 1. **build-unity-android** - Android APK Build
### 2. **build-unity-ios** - iOS Xcode Project Build

---

## ?? What Was Already Built

### **Before (Native Libraries Only):**

| Platform | What Was Built | Output | Job |
|----------|---------------|--------|-----|
| **iOS** | Native C++ Library | `libFelina.a` (XCFramework) | `build-ios-library` |
| **Android** | Native C++ Library | `libFelina.so` | `build-other-platforms` |
| **Windows** | Native C++ Library | `Felina.dll` | `build-other-platforms` |
| **macOS** | Native C++ Library | `libFelina.dylib` | `build-other-platforms` |

These are **native plugins** used within Unity projects.

---

## ?? What's Now Being Built

### **After (Native Libraries + Unity Apps):**

| Platform | What's Built | Output | Job |
|----------|-------------|--------|-----|
| **iOS** | Native C++ Library | `libFelina.a` (XCFramework) | `build-ios-library` |
| **iOS** | **Unity iOS App** ? | **Xcode Project** | **`build-unity-ios`** ?? NEW |
| **Android** | Native C++ Library | `libFelina.so` | `build-other-platforms` |
| **Android** | **Unity Android App** ? | **APK File** | **`build-unity-android`** ?? NEW |
| **Windows** | Native C++ Library | `Felina.dll` | `build-other-platforms` |
| **macOS** | Native C++ Library | `libFelina.dylib` | `build-other-platforms` |

---

## ?? New Job Details

### **build-unity-android**

**Purpose:** Build a complete Android APK from the Unity project

**Steps:**
1. ? Free disk space (~30 GB)
2. ? Checkout repository with LFS
3. ? Download all platform native libraries
4. ? Organize plugins into correct directories
5. ? Cache Unity Library folder
6. ? Build Android APK using game-ci/unity-builder
7. ? Upload APK as artifact

**Output:**
- `android-apk` artifact containing `.apk` file
- Can be installed directly on Android devices
- Signed with keystore (if secrets provided)

**Requirements:**
To sign the APK, you need these GitHub secrets:
- `ANDROID_KEYSTORE_BASE64` - Base64 encoded keystore file
- `ANDROID_KEYSTORE_NAME` - Keystore filename
- `ANDROID_KEYSTORE_PASS` - Keystore password
- `ANDROID_KEYALIAS_NAME` - Key alias name
- `ANDROID_KEYALIAS_PASS` - Key alias password

**Without keystore:** APK will still build but won't be signed for release.

---

### **build-unity-ios**

**Purpose:** Build Unity iOS Xcode project

**Steps:**
1. ? Checkout repository with LFS
2. ? Download all platform native libraries
3. ? Organize plugins into correct directories
4. ? Cache Unity Library folder
5. ? Build iOS Xcode project using game-ci/unity-builder
6. ? Upload Xcode project as artifact

**Output:**
- `ios-xcode-project` artifact containing Xcode project
- **Note:** This creates the Xcode project, not a final IPA
- You need to open in Xcode to:
  - Configure signing & capabilities
  - Build the final IPA
  - Submit to App Store

**Why Xcode Project and not IPA?**
- iOS apps require code signing with Apple certificates
- Can't sign in CI without certificates
- Xcode project allows you to sign locally

---

## ?? Workflow Flow

```
???????????????????????????????????????????????
? 1. Build Native Libraries                  ?
?    - iOS XCFramework (arm64)                ?
?    - Android .so (arm64-v8a)                ?
?    - Windows DLL (x86_64)                   ?
?    - macOS dylib                            ?
???????????????????????????????????????????????
                   ?
                   ?
???????????????????????????????????????????????
? 2. Create Unity Package                     ?
?    - Export .unitypackage                   ?
?    - Include all native libraries           ?
???????????????????????????????????????????????
                   ?
                   ?
        ???????????????????????
        ?                     ?
        ?                     ?
????????????????????  ????????????????????
? 3. Build Android ?  ? 4. Build iOS     ?
?    - Unity APK   ?  ?    - Xcode Proj  ?
?    - Signed      ?  ?    - Plugins     ?
????????????????????  ????????????????????
```

---

## ?? Artifacts Produced

After a successful workflow run, you'll have:

1. **ios-libraries** - iOS native XCFramework
2. **Windows-library** - Windows native DLL
3. **macOS-library** - macOS native dylib
4. **Android-library** - Android native .so
5. **unity-package** - Unity package (.unitypackage)
6. **test-results** - Package import test logs
7. **android-apk** ? - **Android APK** (new!)
8. **ios-xcode-project** ? - **iOS Xcode project** (new!)

---

## ?? Configuration Required

### For Android Builds:

To release a signed APK, add these secrets in GitHub:
1. Go to repository Settings ? Secrets and variables ? Actions
2. Add these secrets:

```
ANDROID_KEYSTORE_BASE64
ANDROID_KEYSTORE_NAME
ANDROID_KEYSTORE_PASS
ANDROID_KEYALIAS_NAME
ANDROID_KEYALIAS_PASS
```

**To create keystore:**
```bash
keytool -genkey -v -keystore release.keystore \
  -alias release \
  -keyalg RSA \
  -keysize 2048 \
  -validity 10000

# Convert to base64
cat release.keystore | base64 > keystore.base64
```

### For iOS Builds:

No additional secrets needed for Xcode project generation.

**To create IPA:**
1. Download `ios-xcode-project` artifact
2. Extract and open in Xcode
3. Configure signing with your Apple Developer account
4. Build and archive
5. Export IPA

---

## ?? Build Targets Summary

| Target | Platform | Type | Output | Signed? |
|--------|----------|------|--------|---------|
| Native Libs | iOS | C++ | XCFramework | N/A |
| Native Libs | Android | C++ | .so | N/A |
| Unity App | **Android** ? | **APK** | **APK** | **If keystore provided** |
| Unity App | **iOS** ? | **Xcode** | **Xcode Project** | **Locally in Xcode** |
| Package | All | Unity | .unitypackage | N/A |

---

## ?? Commit These Changes

```bash
git add .github/workflows/build-and-package.yml
git commit -m "feat(ci): add Unity Android and iOS builds

Add two new jobs to build complete Unity applications:

1. build-unity-android
   - Builds Android APK
   - Includes all native plugins
   - Supports keystore signing
   - Uploads APK artifact

2. build-unity-ios
   - Builds iOS Xcode project
   - Includes all native plugins  
   - Uploads Xcode project for local signing

Both jobs:
- Run after create-unity-package
- Download all platform libraries
- Use Library caching for speed
- Free disk space for Unity 6

This completes the CI/CD pipeline with native libraries,
Unity package export, and platform-specific builds."

git push origin feature/store-version
```

---

## ? Summary

**What you asked for:** ? Done!
- ? **Android build added** - Unity APK
- ? **iOS build added** - Unity Xcode project

**What was already there:**
- ? iOS native library (C++)
- ? Android native library (C++)
- ? Windows native library
- ? macOS native library
- ? Unity package export

**Total build outputs now:**
- 4 native libraries (iOS, Android, Windows, macOS)
- 1 Unity package (.unitypackage)
- 1 Android APK
- 1 iOS Xcode project

**The workflow is now complete for building and distributing to both Android and iOS!** ????
