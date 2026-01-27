# Felina AR Coloring Book

**Transform physical coloring books into immersive AR experiences with professional-grade image tracking and texture capture.**

[![Unity Version](https://img.shields.io/badge/Unity-2019.4%2B--6.3%2B-blue)](https://unity.com)
[![AR Foundation](https://img.shields.io/badge/AR%20Foundation-4.1.x%2B--6.x-green)](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@latest)
[![Platform](https://img.shields.io/badge/Platform-iOS%20%7C%20Android-orange)](https://unity.com)
[![License](https://img.shields.io/badge/License-Commercial-red)](LICENSE.md)

## ?? What Is This?

Felina AR Coloring Book is a **production-ready Unity package** that enables developers to create interactive augmented reality applications where physical images come to life. Perfect for:

- ?? **Interactive Children's Books** - Bring drawings to 3D life
- ?? **Educational Apps** - Scan colored artwork and display AR content
- ?? **AR Games** - Use physical cards as game controllers
- ?? **Marketing Campaigns** - Interactive promotional materials
- ?? **Museum Exhibits** - Augmented historical displays

## ? Key Features

### ?? Advanced AR Image Tracking
- **Multi-version Support**: Works with Unity 2019.4 through Unity 6.3+
- **Automatic Version Detection**: Seamlessly adapts to your Unity version
- **Robust Tracking**: Handles real-world lighting and movement
- **Quality Scoring**: Intelligent capture based on image quality metrics
- **Device Stability**: Built-in shake detection for optimal capture timing

### ??? Real-time Texture Capture
- **GPU-Accelerated Processing**: Native C++ homography unwarp for performance
- **Configurable Resolution**: 256px to 2048px output textures
- **Auto-Lock System**: Stops processing after achieving quality threshold
- **Smart Caching**: Reuses captured textures to save resources
- **Frame Optimization**: Intelligent camera feed updates

### ?? Easy Integration
- **Drag-and-Drop Setup**: No coding required for basic scenarios
- **Event-Driven Architecture**: Subscribe to texture capture events
- **Custom Inspector UI**: Visual configuration in Unity Editor
- **Prefab Spawning**: Automatically instantiate 3D content on tracked images
- **Material Property Blocks**: Zero-allocation texture updates

### ?? Production-Ready Performance
- **Mobile Optimized**: Tested on iPhone 6S+ and ARCore devices
- **Configurable Quality**: Balance between quality and performance
- **Memory Efficient**: Native arrays and proper cleanup
- **Stability Detection**: Prevents blurry captures
- **Multi-Platform**: Single codebase for iOS and Android

## ?? System Requirements

### Minimum Unity Versions
- **Unity 2019.4 LTS** (with AR Foundation 4.1.x) - **Minimum Required**
- **Unity 2020.3 LTS** (with AR Foundation 4.1.x)
- **Unity 2021.3 LTS** (with AR Foundation 4.2.x)
- **Unity 2022.3 LTS** (with AR Foundation 5.x) - **Recommended**
- **Unity 2023.x / Unity 6.x** (with AR Foundation 6.x)

### Required Packages
| Package | Version | Purpose |
|---------|---------|----------|
| AR Foundation | 4.1.x - 6.x | Core AR framework (version-matched) |
| ARKit XR Plugin | 4.1.x - 6.x | iOS support (version-matched) |
| ARCore XR Plugin | 4.1.x - 6.x | Android support (version-matched) |
| Unity Mathematics | 1.2.1+ | Math utilities |

### Target Devices
#### iOS
- **Minimum**: iPhone 6S, iPad (5th gen), iOS 11.0+
- **Recommended**: iPhone 11+, iOS 14.0+
- **Requirements**: ARKit-capable device with Metal support

#### Android
- **Minimum**: ARCore-supported device, Android 7.0 (API 24)+
- **Recommended**: Android 9.0+ with Vulkan support
- **Requirements**: See [ARCore supported devices](https://developers.google.com/ar/devices)

### Development Environment
- **Windows**: Windows 10+ with Visual Studio 2019+
- **macOS**: macOS 10.15+ with Xcode 11+
- **Build Tools**: Android SDK, iOS deployment tools

## ?? Installation

### Option 1: Unity Asset Store (Recommended)
1. **Purchase** from Unity Asset Store
2. **Open** Unity Package Manager (`Window > Package Manager`)
3. Select **"My Assets"** tab
4. Find **"Felina AR Coloring Book"**
5. Click **"Import"**
6. Wait for import to complete
7. Run **`Felina > Validate Package Setup`** to verify installation

### Option 2: Package Manager (Local)
1. Open Unity Package Manager (`Window > Package Manager`)
2. Click **"+"** ? **"Add package from disk"**
3. Navigate to `Assets/ColouringBook/package.json`
4. Click **"Open"**
5. Run **`Felina > Validate Package Setup`**

### Option 3: Manual Import
1. Drag `ColouringBook` folder into your `Assets/` directory
2. Wait for Unity to import and compile
3. Run **`Felina > Validate Package Setup`**

### Post-Installation Setup

**Automatic Validation** (Recommended):
```
Felina > Validate Package Setup
```
This tool will:
- ? Detect your Unity version
- ? Check AR Foundation installation
- ? Verify platform plugins
- ? Provide version-specific recommendations
- ? Show fix instructions for any issues

**Manual Setup**:
1. Install AR Foundation for your Unity version (see [INSTALLATION.md](INSTALLATION.md))
2. Install platform-specific plugins (ARKit for iOS, ARCore for Android)
3. Enable XR Plug-in Management:
   - `Edit > Project Settings > XR Plug-in Management`
   - Enable **ARKit** (iOS) or **ARCore** (Android)
4. Configure platform settings (see [INSTALLATION.md](INSTALLATION.md))

> ?? **First Time User?** Check out [INSTALLATION.md](INSTALLATION.md) for detailed step-by-step instructions for your Unity version.

## ?? Quick Start (5 Minutes)

### Step 1: Setup AR Scene

**Automatic Setup**:
```
Create > AR Foundation > AR Scene Setup
```
This creates:
- ? AR Session (manages AR lifecycle)
- ? AR Session Origin / XR Origin (AR camera parent)
- ? AR Camera (your AR view)
- ? AR Tracked Image Manager (detects images)

**Manual Setup**:
1. Create **AR Session** GameObject
2. Create **AR Session Origin** (Unity 2022 and earlier) or **XR Origin** (Unity 2023+)
3. Assign your **XR Reference Image Library** to AR Tracked Image Manager

> ?? **Reference Image Library**: Create via `Create > XR > Reference Image Library`, then add your target images

### Step 2: Configure Scanner Manager

1. Create empty GameObject named **"Scanner"**
2. Add **`ARScannerManager`** component
3. Configure settings:
   - **Output Resolution**: `1024` (balance between quality and performance)
   - **Capture Threshold**: `0.85` (higher = better quality, slower capture)
   - **Auto Lock**: `Enabled` (stops processing after capture)
   - **Unwarp Material**: Assign `Materials/HomographyUnwarp` (included)

**What this does**:
- Monitors tracked images for quality
- Captures and unwarps the camera feed
- Fires `OnTextureCaptured` event when done

### Step 3: Spawn AR Content

**Option A: Using ARContentSpawner** (Easiest):
1. Add **`ARContentSpawner`** to AR Tracked Image Manager GameObject
2. In Inspector, assign prefabs to each reference image:
   - Select image from dropdown
   - Drag prefab to "Prefab to Spawn" field
3. Prefabs spawn automatically when images are detected

**Option B: Using ARPaintableObject** (For existing objects):
1. Add **`ARPaintableObject`** to any GameObject with a Renderer
2. Set **Reference Image Name** (must match library)
3. Set **Material Index** (which material to update)
4. Set **Texture Property Name**: `_MainTex` or `_BaseMap`

**What this does**:
- Instantiates 3D content on tracked images
- Updates materials with captured textures
- Automatically shows/hides based on tracking

### Step 4: Test in Play Mode

1. **Print** your reference images (ensure good contrast)
2. Click **Play** in Unity Editor
3. **Point webcam** at printed image (if available)
4. **On Device**: Build and deploy to iOS/Android device

**Expected Results**:
- Image detected ? Prefab spawns
- Device stable ? Quality score increases
- Threshold reached ? Texture captured
- Material updated with captured image

> ?? **Tip**: Use `Debug.Log` statements in ARScannerManager to see quality scores in real-time

### Complete Example Scene

Check out **`Scenes/ARFoundationSample.unity`** for a working example with:
- ? Pre-configured scanner
- ? Sample reference images
- ? Example 3D content
- ? Material setup

### Next Steps

- ?? Read [Architecture Overview](#-architecture-overview) to understand the system
- ?? Explore [Configuration Options](#-configuration) for fine-tuning
- ?? Check [Troubleshooting](#-troubleshooting) if you encounter issues
- ?? See [Platform-Specific Notes](#-platform-specific-notes) for iOS/Android details

## ??? Architecture Overview

### System Flow

```
??????????????????????
?  AR Foundation      ?
?  (Unity Package)    ?
?                    ?
?  - Image Tracking   ?
?  - Camera Feed      ?
???????????????????????
        ?
        ?
???????????????????????
? ARFoundationBridge  ?
? (IARBridge impl)   ?
?                    ?
? - Version Adapter  ?
? - Event Wrapper    ?
???????????????????????
        ?
        ?
???????????????????????
?  ARScannerManager  ?
?  (Core Logic)      ?
?                    ?
?  1. Quality Check  ?
?  2. Stability Test ?
?  3. Homography     ?
?  4. Unwarp         ?
?  5. Fire Event     ?
???????????????????????
        ?
        ?????????????????????????????
        ?                           ?
        ?                           ?
?????????????????   ??????????????????????
? ARContentSpawner ?   ? ARPaintableObject ?
?                 ?   ?                   ?
? Spawns Prefabs  ?   ? Updates Materials ?
????????????????????   ??????????????????????
```

### Core Components Explained

#### 1. **IARBridge** (Interface)
- **Purpose**: Abstracts AR platform differences
- **Why**: Allows switching between ARFoundation versions seamlessly
- **You interact with**: Rarely directly - used internally

#### 2. **ARFoundationBridge** (Implementation)
- **Purpose**: Adapts AR Foundation events to our system
- **What it does**:
  - Listens to ARTrackedImageManager
  - Provides camera feed access
  - Converts tracking data to `ScanTarget` structs
- **You interact with**: Set as "AR Bridge Component" in ARScannerManager

#### 3. **ARScannerManager** (Core Engine)
- **Purpose**: Main scanning and capture logic
- **What it does**:
  1. Receives tracked image notifications
  2. Calculates quality score (viewing angle, distance, stability)
  3. Checks device stability (prevents blurry captures)
  4. Computes homography matrix (perspective correction)
  5. Unwarps camera feed using GPU shader
  6. Fires `OnTextureCaptured` event with result
- **You interact with**: Configure settings, subscribe to events

#### 4. **ARContentSpawner** (Convenience Component)
- **Purpose**: Automatically spawn prefabs on tracked images
- **What it does**:
  - Maps reference images to prefabs
  - Instantiates prefabs when images detected
  - Shows/hides based on tracking state
  - Applies captured textures to spawned objects
- **You interact with**: Assign prefabs in custom inspector

#### 5. **ARPaintableObject** (Material Updater)
- **Purpose**: Updates existing object materials with captured textures
- **What it does**:
  - Listens for texture capture events
  - Applies textures using MaterialPropertyBlock (efficient)
  - Supports multiple material slots
  - Works with Standard, URP, and custom shaders
- **You interact with**: Add to existing GameObjects

### Event Flow

```csharp
// 1. AR Foundation detects image
ARTrackedImageManager ? trackablesChanged event

// 2. Bridge forwards to scanner
ARFoundationBridge ? OnTargetAdded(ScanTarget)

// 3. Scanner processes asynchronously
ARScannerManager ? Quality check loop
                  ? Capture when stable & high quality
                  ? OnTextureCaptured(name, texture, score)

// 4. Consumers react
ARContentSpawner ? Updates spawned object material
ARPaintableObject ? Updates assigned object material
Your Custom Code ? Do anything with captured texture
```

### Extension Points

**Create Custom Consumers**:
```csharp
public class MyCustomHandler : MonoBehaviour
{
    void Start()
    {
        // Subscribe to capture events
        ARScannerManager.Instance.OnTextureCaptured += OnTextureReady;
    }
    
    void OnTextureReady(string imageName, RenderTexture texture, float quality)
    {
        // Your custom logic here
        Debug.Log($"Captured {imageName} with quality {quality}");
        
        // Examples:
        // - Save to file
        // - Send to server
        // - Apply filters
        // - Generate thumbnails
    }
}
```

**Create Custom AR Bridge**:
```csharp
public class MyCustomBridge : MonoBehaviour, IARBridge
{
    public event Action<ScanTarget> OnTargetAdded;
    
    // Implement interface methods
    public Camera GetARCamera() { /* ... */ }
    public RenderTexture GetCameraFeedRT() { /* ... */ }
    // ...
}
```

## ?? Configuration

### ARScannerManager Settings

> **Note:** As of v1.0.1, solver parameters (inlier threshold, max iterations, refine with LM) are now configured in the `Settings` asset (ScriptableObject) and not directly on the ARScannerManager component.

#### Output Resolution
**What it does**: Size of the captured texture

| Resolution | Use Case | Performance |
|------------|----------|-------------|
| **256px** | Low-end devices, simple textures | ?? Excellent |
| **512px** | Mobile games, stylized art | ?? Very Good |
| **1024px** | Standard quality (default) | ?? Good |
| **2048px** | High-detail textures, tablets | ?? Moderate |

```csharp
// Set in Inspector or code:
_outputResolution = 1024;
```

#### Capture Threshold (0.0 - 1.0)
**What it does**: Minimum quality score required for auto-lock

| Value | Behavior | Best For |
|-------|----------|----------|
| **0.5-0.6** | Captures quickly, lower quality | Fast prototyping |
| **0.7-0.8** | Balanced capture time & quality | General use |
| **0.85-0.95** | Very strict, high quality (default) | Production apps |
| **0.95-1.0** | Extremely strict, perfect shots only | Photography apps |

```csharp
// Set in Settings asset:
Settings.Instance.CAPTURE_THRESHOLD = 0.85f;
```

#### Solver Parameters (NEW)
**What they do**: Control the homography solver's behavior

| Parameter | Description | Default |
|-----------|-------------|---------|
| Inlier Threshold | RANSAC inlier threshold for solver | 4.0 |
| Max Iterations | Maximum RANSAC iterations | 200 |
| Refine With LM | Use Levenberg-Marquardt refinement | true |

```csharp
// Set in Settings asset:
Settings.Instance.INLIER_THRESHOLD = 4.0f;
Settings.Instance.MAX_ITERATIONS = 200;
Settings.Instance.REFINE_WITH_LM = true;
```

#### Auto Lock
**What it does**: Stops processing after threshold reached

- ? **Enabled** (default): Saves CPU/battery, best for single-capture scenarios
- ? **Disabled**: Continuous capture, updates texture as quality improves

```csharp
_autoLock = true; // Lock after first good capture
```

#### Stability Thresholds
**What it does**: Determines when device is "stable" enough to capture

```csharp
// Maximum movement speed (units per second)
Settings.Instance.MAX_MOVE_SPEED = 0.05f;  // Lower = stricter

// Maximum rotation speed (degrees per second)  
Settings.Instance.MAX_ROTATE_SPEED = 5.0f; // Lower = stricter
```

### Performance Tuning Guide

#### For Low-End Devices (iPhone 6S, older Android)
```csharp
ARScannerManager settings:
- Output Resolution: 512
- Capture Threshold: 0.7
- Auto Lock: Enabled
- Max Feed Resolution: 1280x720
```

#### For Mid-Range Devices (iPhone X, modern Android)
```csharp
ARScannerManager settings:
- Output Resolution: 1024 (default)
- Capture Threshold: 0.85 (default)
- Auto Lock: Enabled
- Max Feed Resolution: 1920x1080 (default)
```

#### For High-End Devices (iPhone 13+, flagship Android)
```csharp
ARScannerManager settings:
- Output Resolution: 2048
- Capture Threshold: 0.9
- Auto Lock: Enabled or Disabled
- Max Feed Resolution: 1920x1080
```

#### For Continuous Tracking (AR Games)
```csharp
ARScannerManager settings:
- Output Resolution: 512-1024
- Capture Threshold: 0.75
- Auto Lock: Disabled (keep updating)
- Max Feed Resolution: 1280x720
```

### ARContentSpawner Settings

**Configuration via Inspector**:
1. Select tracked image from dropdown
2. Assign prefab to spawn
3. Optionally set material index for texture application

**Programmatic Configuration**:
```csharp
// Access target data
public List<TargetData> GetTargetData()
{
    return _targetData;
}

// Add new mapping
var newTarget = new TargetData
{
    name = "MyImage",
    imageGuid = "...",
    prefab = myPrefab,
    materialIndex = 0
};
```

### Shader Configuration

The included **HomographyUnwarp** shader supports:

```shader
Properties {
    _MainTex ("Camera Feed", 2D) // Set by scanner
    _Homography ("Homography Matrix", Matrix) // Set by scanner
    _DisplayMatrix ("Display Transform", Matrix) // Usually identity
}
```

**No configuration needed** - ARScannerManager handles this automatically.

## ?? Platform-Specific Notes

### iOS (ARKit)

#### Device Requirements
| Device | iOS Version | ARKit Version | Status |
|--------|-------------|---------------|--------|
| iPhone 6S / 6S Plus | iOS 11.0+ | ARKit 1.0+ | ? Supported |
| iPhone SE (1st gen) | iOS 11.0+ | ARKit 1.0+ | ? Supported |
| iPhone 7 / 7 Plus | iOS 11.0+ | ARKit 1.0+ | ? Supported |
| iPhone 8 / 8 Plus / X | iOS 11.0+ | ARKit 1.0+ | ? Recommended |
| iPhone 11 and newer | iOS 13.0+ | ARKit 3.0+ | ?? Best |
| iPad (5th gen) and newer | iOS 11.0+ | ARKit 1.0+ | ? Supported |
| iPad Pro (all models) | iOS 11.0+ | ARKit 1.0+ | ?? Best |

#### Build Settings

1. **Target Minimum iOS Version**:
   ```
   Build Settings > iOS > Target minimum iOS Version: 11.0
   ```

2. **Graphics API**:
   ```
   Project Settings > Player > iOS > Graphics APIs
   - Metal (required for ARKit)
   - Remove OpenGL ES if present
   ```

3. **Camera Permission**:
   ```
   Project Settings > Player > iOS > Camera Usage Description
   Add: "AR experience requires camera access"
   ```

4. **Architecture**:
   ```
   Build Settings > iOS > Architecture: ARM64
   (Remove ARMv7 for modern devices)
   ```

#### Xcode Project Settings

After Unity build, in Xcode:

1. **Signing & Capabilities**:
   - Add your development team
   - Enable "Automatically manage signing"

2. **Info.plist**:
   ```xml
   <key>NSCameraUsageDescription</key>
   <string>AR experience requires camera access</string>
   <key>UIRequiredDeviceCapabilities</key>
   <array>
       <string>arkit</string>
   </array>
   ```

3. **Build Settings** (if needed):
   - Deployment Target: 11.0
   - Architectures: arm64

#### Performance Tips

- **Metal Frame Capture**: Use Xcode's GPU profiling
- **Target 30 FPS** on iPhone 6S/7, **60 FPS** on iPhone 8+
- **Thermal Management**: Monitor device temperature in long sessions

```csharp
// iOS-specific optimization
#if UNITY_IOS
    Application.targetFrameRate = 60;
    QualitySettings.vSyncCount = 0;
#endif
```

### Android (ARCore)

#### Device Requirements

**Supported Devices**: See [ARCore supported devices list](https://developers.google.com/ar/devices)

Common devices:
- Google Pixel series (all)
- Samsung Galaxy S8 and newer
- Samsung Galaxy Note 8 and newer
- OnePlus 5 and newer
- Xiaomi Mi 8 and newer

**Minimum**:
- Android 7.0 (API Level 24)
- OpenGL ES 3.0 or Vulkan support

#### Build Settings

1. **Target API Level**:
   ```
   Build Settings > Android > Minimum API Level: 24 (Android 7.0)
   Build Settings > Android > Target API Level: 30+ (Android 11+)
   ```

2. **Graphics API**:
   ```
   Project Settings > Player > Android > Graphics APIs
   Recommended order:
   1. Vulkan (best performance on modern devices)
   2. OpenGL ES 3.0
   ```

3. **Camera Permission**:
   ```
   Project Settings > Player > Android > Write Permissions: External (SD Card)
   
   AndroidManifest.xml will automatically include:
   <uses-permission android:name="android.permission.CAMERA" />
   <uses-feature android:name="android.hardware.camera.ar" android:required="true" />
   ```

4. **Architecture**:
   ```
   Build Settings > Android > Target Architectures:
   - ARM64: ? (required)
   - ARMv7: ? (optional, for older devices)
   ```

#### ARCore APK Configuration

In `AndroidManifest.xml`:

```xml
<application ...>
    <!-- ARCore requirement -->
    <meta-data 
        android:name="com.google.ar.core" 
        android:value="required" />
</application>

<!-- Camera -->
<uses-permission android:name="android.permission.CAMERA" />
<uses-feature android:name="android.hardware.camera.ar" android:required="true" />
```

**Note**: Unity's ARCore package handles this automatically.

#### Performance Tips

- **Target 30 FPS** universally for compatibility
- **Test on mid-range devices** (Samsung A series, Pixel 4a)
- **Reduce camera resolution** for low-end devices:

```csharp
#if UNITY_ANDROID
    // Detect device tier
    bool isLowEnd = SystemInfo.systemMemorySize < 3000; // < 3GB RAM
    
    if (isLowEnd)
    {
        _outputResolution = 512;
        _maxFeedResolution = 1280;
    }
#endif
```

#### Common Android Issues

**Issue**: "ARCore not installed" error
- **Cause**: User doesn't have ARCore services
- **Solution**: App prompts user to install from Play Store
- **Note**: Unity handles this automatically

**Issue**: Black screen on some devices
- **Cause**: Graphics API incompatibility
- **Solution**: Switch from Vulkan to OpenGL ES 3.0

**Issue**: Slow performance on budget devices
- **Solution**: Lower `outputResolution` to 256-512

### Editor Testing (Limitations)

**What Works**:
- ? Scene setup and configuration
- ? Component assignment
- ? Event subscription
- ? Basic logic testing

**What Doesn't Work**:
- ? Actual AR tracking (requires device)
- ? Camera feed (can use webcam with AR Foundation Remote)
- ? Native plugin calls (desktop builds use simulated data)

**AR Foundation Remote**:
- Install on device for live testing in editor
- [Documentation](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@latest)

### Cross-Platform Tips

```csharp
// Platform-specific configuration
void ConfigureForPlatform()
{
#if UNITY_IOS
    // iOS optimization
    _outputResolution = 1024;
    Application.targetFrameRate = 60;
#elif UNITY_ANDROID
    // Android optimization
    _outputResolution = SystemInfo.systemMemorySize < 3000 ? 512 : 1024;
    Application.targetFrameRate = 30;
#else
    // Editor/Desktop
    _outputResolution = 1024;
#endif
}
```

## ?? Troubleshooting

### Common Issues & Solutions

#### 1. "No compilation errors but nothing happens"

**Symptoms**: Scene runs but no AR tracking occurs

**Checklist**:
- ?? Is **XR Plug-in Management** enabled?
  - `Edit > Project Settings > XR Plug-in Management`
  - Enable **ARKit** (iOS) or **ARCore** (Android)
- ?? Is **AR Session** in scene and enabled?
- ?? Is **Reference Image Library** assigned to ARTrackedImageManager?
- ?? Are reference images **imported correctly** (readable, compressed format)?
- ?? Is **ARScannerManager.Instance** not null? (Check console for errors)

**Solution**:
```csharp
// Add debug logging to Start()
void Start()
{
    if (ARScannerManager.Instance == null)
        Debug.LogError("ARScannerManager not found!");
    else
        Debug.Log("Scanner initialized");
}
```

#### 2. "Image detected but prefab doesn't spawn"

**Symptoms**: AR tracking works but nothing appears

**Causes**:
- Prefab not assigned in ARContentSpawner inspector
- Image GUID mismatch
- Prefab scale too large/small
- Prefab instantiates behind image

**Solution**:
1. Check **ARContentSpawner** inspector:
   - Verify dropdown shows your image name
   - Verify prefab is assigned
2. Check console for warnings:
   ```
   "No prefab assigned for image (GUID: ...)"

3. Manually verify spawning:
   ```csharp
   void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
   {
       Debug.Log($"Added: {args.added.Count}");
       foreach (var img in args.added)
       {
           Debug.Log($"Image: {img.referenceImage.name}");
       }
   }
   ```

#### 3. "Texture not appearing on material"

**Symptoms**: Prefab spawns but stays original color

**Causes**:
- Material property name mismatch
- Wrong material index
- Shader doesn't support texture property
- Capture not triggering

**Solution**:
1. **Check property name**:
   - Standard Shader: `_MainTex`
   - URP Lit: `_BaseMap`
   - Custom shader: Check shader source
2. **Verify material index**:
   ```csharp
   // In ARPaintableObject or ARContentSpawner
   materialIndex = 0; // First material (usually correct)
   ```

3. **Check if capture fired**:
   ```csharp
   void Start()
   {
       ARScannerManager.Instance.OnTextureCaptured += (name, tex, score) =>
       {
           Debug.Log($"Captured {name}: {tex.width}x{tex.height}, quality {score}");
       };
   }
   ```

4. **Verify shader has property**:
   ```csharp
   if (!material.HasProperty("_MainTex"))
       Debug.LogError("Shader missing _MainTex property!");
   ```

#### 4. "GL_INVALID_ENUM error on Android"

**Symptoms**: RenderTexture errors in console

**Cause**: Incompatible RenderTexture format for device GPU

**Solution**: Package auto-detects supported formats, but verify:
```csharp
// ARScannerManager automatically uses:
var format = RenderTextureFormat.Default; // Safest option

// If still issues, try:
var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32)
    ? RenderTextureFormat.ARGB32
    : RenderTextureFormat.Default;
```

#### 5. "Poor capture quality / always blurry"

**Symptoms**: Captured textures are blurry or low quality

**Causes**:
- Device moving too much
- Poor lighting
- Threshold too low
- Resolution too low

**Solutions**:
1. **Increase stability thresholds**:
   ```csharp
   _maxMoveSpeed = 0.02f;  // Stricter (was 0.05f)
   _maxRotateSpeed = 2.0f; // Stricter (was 5.0f)
   ```

2. **Increase quality threshold**:
   ```csharp
   _captureThreshold = 0.9f; // Higher quality (was 0.85f)
   ```

3. **Increase resolution**:
   ```csharp
   _outputResolution = 2048; // Higher res (was 1024)
   ```

4. **Improve lighting**:
   - Use bright, even lighting
   - Avoid shadows on target image
   - Avoid glossy/reflective surfaces

#### 6. "Validator says AR Foundation not found"

**Symptoms**: `Felina > Validate Package Setup` shows AR Foundation missing

**Solution**:
1. Open **Window > Package Manager**
2. Switch to **"Unity Registry"**
3. Search for **"AR Foundation"**
4. Click **"Install"**
5. Install matching **ARKit/ARCore XR Plugin**
6. Re-run validator

**Version Matching**:
```
Unity 2019.4 ? AR Foundation 2.1.18
Unity 2020.3 ? AR Foundation 4.1.13
Unity 2021.3 ? AR Foundation 4.2.10
Unity 2022.3 ? AR Foundation 5.1.5
Unity 2023+  ? AR Foundation 6.0.0+
```

#### 7. "Sample scripts causing errors"

**Symptoms**: Compilation errors referencing AR Foundation samples

**Cause**: Old sample scripts from different AR Foundation version

**Solution**:
```
Felina > Clean AR Foundation Samples
```
Or manually delete:
```
Assets/Samples/AR Foundation/
Assets/Samples/ARFoundation/
```

See [SAMPLE_MANAGEMENT.md](SAMPLE_MANAGEMENT.md) for details.

### Debugging Tips

#### Enable Verbose Logging
```csharp
// Add to ARScannerManager.cs Update()
void Update()
{
    if (IsDeviceStable)
    {
        float score = CalculateQualityScore(target);
        Debug.Log($"[Quality] {target.Name}: {score:F2}");
    }
}
```

#### Visualize Camera Feed
```csharp
// Create RawImage UI element
public RawImage debugDisplay;

void Start()
{
    debugDisplay.texture = _cameraFeedRT;
}
```

#### Check Native Plugin
```csharp
void Start()
{
    try
    {
        int magic = GetDebugNumber(); // Should return 777
        Debug.Log($"Native plugin: {magic}");
    }
    catch (Exception e)
    {
        Debug.LogError($"Native plugin failed: {e.Message}");
    }
}
```

### Still Having Issues?

1. Check **[GitHub Issues](https://github.com/hhthunderbird/ARColoringBook/issues)**
2. Read **[Multi-Version Support](MULTI_VERSION_SUPPORT.md)**
3. Review **[Installation Guide](INSTALLATION.md)**
4. Email: **support@felina.dev**

## ?? API Reference

### ARScannerManager

**Singleton Access**:
```csharp
ARScannerManager.Instance
```

#### Events

**OnTextureCaptured**
```csharp
public event Action<string, RenderTexture, float> OnTextureCaptured;

// Usage:
void Start()
{
    ARScannerManager.Instance.OnTextureCaptured += HandleTextureCapture;
}

void HandleTextureCapture(string imageName, RenderTexture texture, float qualityScore)
{
    Debug.Log($"Captured: {imageName}");
    Debug.Log($"Resolution: {texture.width}x{texture.height}");
    Debug.Log($"Quality: {qualityScore:F2}");
    
    // Use the texture...
}

void OnDestroy()
{
    if (ARScannerManager.Instance != null)
        ARScannerManager.Instance.OnTextureCaptured -= HandleTextureCapture;
}
```

#### Methods

**ResetCapture()**
```csharp
public void ResetCapture()

// Resets the scanner to capture again
// Useful when auto-lock is enabled
ARScannerManager.Instance.ResetCapture();
```

**GetCapturedTexture()** (Future feature)
```csharp
public RenderTexture GetCapturedTexture(string targetName)

// Retrieves previously captured texture from cache
RenderTexture tex = ARScannerManager.Instance.GetCapturedTexture("MyImage");
if (tex != null)
{
    myMaterial.mainTexture = tex;
}
```

#### Properties

**IsDeviceStable**
```csharp
public bool IsDeviceStable { get; private set; }

// Check if device is stable enough for capture
if (ARScannerManager.Instance.IsDeviceStable)
{
    Debug.Log("Device is steady - capture imminent");
}
```

### IARBridge (Interface)

**Purpose**: Abstraction layer for AR platform

```csharp
public interface IARBridge
{
    // Events
    event Action<ScanTarget> OnTargetAdded;
    event Action<ScanTarget> OnTargetRemoved; // Future
    
    // Methods
    Camera GetARCamera();
    RenderTexture GetCameraFeedRT();
    RenderTextureSettings RenderTextureSettings { get; }
}
```

**Implementing Custom Bridge**:
```csharp
public class MyCustomBridge : MonoBehaviour, IARBridge
{
    public event Action<ScanTarget> OnTargetAdded;
    
    private Camera _arCamera;
    private RenderTexture _cameraFeed;
    
    public Camera GetARCamera() => _arCamera;
    
    public RenderTexture GetCameraFeedRT() => _cameraFeed;
    
    public RenderTextureSettings RenderTextureSettings => new RenderTextureSettings
    {
        width = 1920,
        height = 1080,
        format = RenderTextureFormat.Default
    };
    
    // Your implementation...
}
```

### ARFoundationBridge

**Built-in implementation of IARBridge**

```csharp
// Usually auto-configured, but can be accessed:
var bridge = GetComponent<ARFoundationBridge>();
Camera arCam = bridge.GetARCamera();
```

### ARScannerManager

**Core component for scanning and capturing**

```csharp
public class ARScannerManager : MonoBehaviour
{
    // Configurable settings
    [SerializeField] private int _outputResolution = 1024;
    [SerializeField] private float _captureThreshold = 0.85f;
    [SerializeField] private bool _autoLock = true;
    
    // Events
    public event Action<string, RenderTexture, float> OnTextureCaptured;
    
    void Update()
    {
        // Scanning logic...
    }
    
    public void ResetCapture() { /* ... */ }
}
```

### ARContentSpawner

**Component for automatic prefab instantiation**

```csharp
public class ARContentSpawner : MonoBehaviour
{
    // Configured via Inspector
    [SerializeField] private List<TargetData> _targetData;
}

// TargetData structure:
[Serializable]
public struct TargetData
{
    public string name;           // Image name
    public string imageGuid;      // Unique identifier
    public GameObject prefab;     // Prefab to spawn
    public Renderer renderer;     // Reference to spawned renderer
    public Texture2D blankMarker; // Optional blank texture
    public int materialIndex;     // Which material to update
}
```

**Programmatic Usage**:
```csharp
void Start()
{
    var spawner = GetComponent<ARContentSpawner>();
    
    // Access spawned instances
    var targetData = spawner.GetTargetData();
    foreach (var target in targetData)
    {
        if (target.renderer != null)
        {
            Debug.Log($"{target.name} spawned at {target.renderer.transform.position}");
        }
    }
}
```

### ARPaintableObject

**Component for applying captured textures to existing objects**

```csharp
public class ARPaintableObject : MonoBehaviour
{
    [SerializeField] private string referenceImageName;
    [SerializeField] private int materialIndex = 0;
    [SerializeField] private string texturePropertyName = "_MainTex";
}
```

**Usage**:
```csharp
// Attach to any GameObject with a Renderer
void Setup()
{
    var paintable = gameObject.AddComponent<ARPaintableObject>();
    paintable.ReferenceImageName = "MyTargetImage";
    paintable.MaterialIndex = 0; // First material
    paintable.TexturePropertyName = "_BaseMap"; // For URP
}
```

### ScanTarget (Struct)

**Data structure for tracked images**

```csharp
public struct ScanTarget
{
    public string Name;           // Reference image name
    public Vector3 Position;      // World position
    public Quaternion Rotation;   // World rotation
    public Vector2 Size;          // Physical size (meters)
    public Transform Transform;   // GameObject transform
    public bool IsTracking;       // Currently tracked?
}
```

### Helper Functions

**Quality Calculation** (Native C++):
```csharp
// Called internally by ARScannerManager
private static extern float CalculateQuality(
    float3 camPos,
    float3 camFwd,
    float3 imgPos,
    float3 imgUp,
    float2 imgScreenPos,
    float screenWidth,
    float screenHeight
);
```

Factors considered:
- Viewing angle (perpendicular is best)
- Distance from camera (closer is better, up to a limit)
- Screen space coverage (larger is better)
- Center alignment (center of screen is preferred)

**Stability Check** (Native C++):
```csharp
private static extern bool CheckStability(
    float3 curPos,
    quaternion curRot,
    float3 lastPos,
    quaternion lastRot,
    float dt,
    float maxMoveSpeed,
    float maxRotSpeed
);
```

### Code Examples

#### Example 1: Save Captured Texture to File
```csharp
using System.IO;
using UnityEngine;

public class TextureSaver : MonoBehaviour
{
    void Start()
    {
        ARScannerManager.Instance.OnTextureCaptured += SaveTexture;
    }
    
    void SaveTexture(string imageName, RenderTexture renderTex, float quality)
    {
        // Convert RenderTexture to Texture2D
        Texture2D tex = new Texture2D(renderTex.width, renderTex.height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTex;
        tex.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        
        // Encode to PNG
        byte[] bytes = tex.EncodeToPNG();
        
        // Save to persistent data path
        string path = Path.Combine(Application.persistentDataPath, $"{imageName}.png");
        File.WriteAllBytes(path, bytes);
        
        Debug.Log($"Saved to: {path}");
        
        Destroy(tex);
    }
}
```

#### Example 2: Send Texture to Server
```csharp
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class TextureUploader : MonoBehaviour
{
    private string serverUrl = "https://myserver.com/upload";
    
    void Start()
    {
        ARScannerManager.Instance.OnTextureCaptured += UploadTexture;
    }
    
    void UploadTexture(string imageName, RenderTexture renderTex, float quality)
    {
        StartCoroutine(UploadCoroutine(imageName, renderTex, quality));
    }
    
    IEnumerator UploadCoroutine(string name, RenderTexture renderTex, float quality)
    {
        // Convert to Texture2D
        Texture2D tex = new Texture2D(renderTex.width, renderTex.height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTex;
        tex.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        
        // Encode
        byte[] bytes = tex.EncodeToJPG(75); // 75% quality
        
        // Upload
        WWWForm form = new WWWForm();
        form.AddField("imageName", name);
        form.AddField("quality", quality.ToString());
        form.AddBinaryData("file", bytes, $"{name}.jpg", "image/jpeg");
        
        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, form))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Upload complete!");
            }
            else
            {
                Debug.LogError($"Upload failed: {www.error}");
            }
        }
        
        Destroy(tex);
    }
}
```

#### Example 3: Apply Filter to Captured Texture
```csharp
using UnityEngine;

public class TextureFilter : MonoBehaviour
{
    [SerializeField] private Material filterMaterial; // Your filter shader
    
    void Start()
    {
        ARScannerManager.Instance.OnTextureCaptured += ApplyFilter;
    }
    
    void ApplyFilter(string imageName, RenderTexture source, float quality)
    {
        // Create output texture
        RenderTexture filtered = RenderTexture.GetTemporary(
            source.width, 
            source.height, 
            0, 
            source.format
        );
        
        // Apply filter
        Graphics.Blit(source, filtered, filterMaterial);
        
        // Use filtered texture
        GetComponent<Renderer>().material.mainTexture = filtered;
        
        // Note: Remember to ReleaseTemporary when done
    }
}
```

## ?? Use Cases & Examples

### 1. Interactive Children's Coloring Book

**Scenario**: Child colors a printed page, points device at it, character comes to life in 3D

**Implementation**:
```csharp
public class ColoringBookManager : MonoBehaviour
{
    [SerializeField] private GameObject character3D;
    [SerializeField] private Material characterMaterial;
    
    void Start()
    {
        ARScannerManager.Instance.OnTextureCaptured += OnColoringCaptured;
    }
    
    void OnColoringCaptured(string imageName, RenderTexture coloredTexture, float quality)
    {
        // Apply colored texture to 3D character
        characterMaterial.SetTexture("_ColorMap", coloredTexture);
        
        // Play animation
        character3D.GetComponent<Animator>().SetTrigger("ComeAlive");
        
        // Play sound effect
        AudioSource.PlayClipAtPoint(cheerSound, character3D.transform.position);
    }
}
```

**Settings**:
- Resolution: 512-1024 (sufficient for characters)
- Threshold: 0.8 (balance speed and quality)
- Auto Lock: Enabled (one capture per page)

### 2. Educational Flash Cards

**Scenario**: Teacher uses AR cards for interactive lessons

**Implementation**:
```csharp
public class FlashCardAR : MonoBehaviour
{
    [SerializeField] private AudioClip[] pronunciations;
    [SerializeField] private GameObject[] educationalObjects;
    
    void Start()
    {
        var spawner = GetComponent<ARContentSpawner>();
        // Spawner handles prefab instantiation
    }
    
    public void OnCardScanned(string cardName)
    {
        // Play pronunciation
        int index = GetCardIndex(cardName);
        AudioSource.PlayClipAtPoint(pronunciations[index], Camera.main.transform.position);
        
        // Show related 3D object
        educationalObjects[index].SetActive(true);
    }
}
```

**Settings**:
- Resolution: 256-512 (fast capture for classroom)
- Threshold: 0.7 (quick response)
- Auto Lock: Disabled (reusable cards)

### 3. AR Business Cards

**Scenario**: Scan business card to see 3D portfolio or contact info

**Implementation**:
```csharp
public class BusinessCardAR : MonoBehaviour
{
    [SerializeField] private GameObject portfolioUI;
    [SerializeField] private string linkedInURL;
    
    void Start()
    {
        ARScannerManager.Instance.OnTextureCaptured += OnBusinessCardScanned;
    }
    
    void OnBusinessCardScanned(string cardOwner, RenderTexture cardTexture, float quality)
    {
        // Extract text using OCR (not included, use third-party)
        // string email = OCRService.ExtractEmail(cardTexture);
        
        // Show portfolio
        portfolioUI.SetActive(true);
        
        // Enable "Connect" button
        connectButton.onClick.AddListener(() => OpenURL(linkedInURL));
    }
}
```

**Settings**:
- Resolution: 1024 (text readability)
- Threshold: 0.9 (high quality for OCR)
- Auto Lock: Enabled

### 4. Museum Exhibit Enhancement

**Scenario**: Point phone at painting to see historical context

**Implementation**:
```csharp
public class MuseumGuideAR : MonoBehaviour
{
    [SerializeField] private VideoPlayer historyVideo;
    [SerializeField] private TextMeshProUGUI infoPanel;
    
    private Dictionary<string, ExhibitData> exhibits;
    
    void Start()
    {
        LoadExhibitDatabase();
        
        ARScannerManager.Instance.OnTextureCaptured += OnArtworkScanned;
    }
    
    void OnArtworkScanned(string artworkName, RenderTexture artwork, float quality)
    {
        if (exhibits.TryGetValue(artworkName, out var data))
        {
            // Show info panel
            infoPanel.text = data.description;
            
            // Play historical video
            historyVideo.url = data.videoURL;
            historyVideo.Play();
            
            // Highlight artist signature
            HighlightRegion(data.signaturePosition);
        }
    }
}
```

**Settings**:
- Resolution: 1024-2048 (artwork detail)
- Threshold: 0.85
- Auto Lock: Disabled (continuous viewing)

### 5. AR Trading Card Game

**Scenario**: Physical cards spawn 3D creatures for battles

**Implementation**:
```csharp
public class TradingCardGame : MonoBehaviour
{
    private List<Creature> playerCreatures = new List<Creature>();
    
    void Start()
    {
        var spawner = GetComponent<ARContentSpawner>();
        // Creatures spawn via ARContentSpawner
    }
    
    public void OnCreatureSpawned(string cardName, GameObject creatureObject)
    {
        var creature = creatureObject.GetComponent<Creature>();
        creature.LoadStats(cardName); // HP, Attack, etc.
        
        playerCreatures.Add(creature);
        
        // Check if battle can start
        if (playerCreatures.Count >= 2)
        {
            StartBattle();
        }
    }
}
```

**Settings**:
- Resolution: 512 (fast spawning)
- Threshold: 0.75
- Auto Lock: Disabled (multiple cards)

### 6. Product Packaging AR Experience

**Scenario**: Scan product box for instructions or promotions

**Implementation**:
```csharp
public class ProductAR : MonoBehaviour
{
    [SerializeField] private GameObject assemblyAnimation;
    [SerializeField] private string promoCode;
    
    void Start()
    {
        ARScannerManager.Instance.OnTextureCaptured += OnPackagingScanned;
    }
    
    void OnPackagingScanned(string productName, RenderTexture packaging, float quality)
    {
        // Show assembly instructions
        assemblyAnimation.SetActive(true);
        
        // Display promo code
        ShowPromoCodeUI(promoCode);
        
        // Track analytics
        Analytics.CustomEvent("ProductScanned", new Dictionary<string, object>
        {
            { "productName", productName },
            { "quality", quality }
        });
    }
}
```

**Settings**:
- Resolution: 512
- Threshold: 0.7 (quick activation)
- Auto Lock: Enabled

## ? Frequently Asked Questions

### General Questions

**Q: Do I need a separate license per project?**
A: No. One license covers unlimited projects by the licensee. See [LICENSE.md](LICENSE.md).

**Q: Can I use this in a commercial application?**
A: Yes! This is a commercial license. You can sell apps using this package.

**Q: Is source code included?**
A: Yes, full C# source code is included. Native C++ binaries are provided (source available upon request).

**Q: What Unity versions are supported?**
A: Unity 2019.4 through Unity 6.3+. See [MULTI_VERSION_SUPPORT.md](MULTI_VERSION_SUPPORT.md).

**Q: Does this work with URP/HDRP?**
A: Yes, the package is render pipeline agnostic. Use appropriate shader properties (`_BaseMap` for URP).

### Technical Questions

**Q: How accurate is the texture capture?**
A: Very accurate with proper conditions. Quality score of 0.85+ indicates <5% distortion.

**Q: Can I capture from multiple images simultaneously?**
A: Currently captures one image at a time. Multi-target capture is planned for v1.1.

**Q: Does this require internet connection?**
A: No, all processing is done on-device.

**Q: What's the performance impact?**
A: Minimal. Native C++ processing, GPU shader unwarp. Typical: <5ms per frame.

**Q: Can I use custom shaders?**
A: Yes! Just ensure your shader has a texture property (e.g., `_MainTex`, `_BaseMap`).

### Platform Questions

**Q: Does this work in Unity Editor?**
A: Partially. Scene setup works, but AR tracking requires device. Use AR Foundation Remote for testing.

**Q: Can I deploy to WebGL?**
A: Not currently. AR Foundation doesn't support WebGL. iOS and Android only.

**Q: What about older devices?**
A: iPhone 6S+ and ARCore-compatible Android devices. Lower resolution settings help older devices.

### Troubleshooting Questions

**Q: Why isn't my image being detected?**
A:
1. Ensure image is in Reference Image Library
2. Check image has good contrast and detail
3. Verify XR Plug-in Management is configured
4. Print image at sufficient size (15cm+ recommended)

**Q: Why is the captured texture blurry?**
A:
1. Increase `_captureThreshold` (e.g., 0.9)
2. Decrease `_maxMoveSpeed` (stricter stability)
3. Ensure good lighting
4. Keep device steady during capture

**Q: Why do I get compilation errors?**
A:
1. Run `Felina > Validate Package Setup`
2. Install AR Foundation for your Unity version
3. Remove old AR Foundation sample scripts
4. Check [INSTALLATION.md](INSTALLATION.md)

### Licensing Questions

**Q: Can multiple developers on my team use this?**
A: Each developer needs a license. Team licensing available - contact support@felina.dev.

**Q: Can I modify the source code?**
A: Yes, you own the code you purchase and can modify it for your projects.

**Q: Can I redistribute this package?**
A: No. You cannot resell or redistribute the source code. See [LICENSE.md](LICENSE.md).

**Q: What if I need technical support?**
A: Email support@felina.dev or open GitHub issues. Active license holders get priority support.

## ?? Support

### Documentation
- **README** (this file): Overview and quick start
- **[INSTALLATION.md](INSTALLATION.md)**: Detailed installation for all Unity versions
- **[MULTI_VERSION_SUPPORT.md](MULTI_VERSION_SUPPORT.md)**: Multi-version architecture
- **[SAMPLE_MANAGEMENT.md](SAMPLE_MANAGEMENT.md)**: AR Foundation sample handling
- **[CHANGELOG.md](CHANGELOG.md)**: Version history

### Getting Help

1. **Check Documentation** (above)
2. **Run Validator**: `Felina > Validate Package Setup`
3. **Search Issues**: [GitHub Issues](https://github.com/hhthunderbird/ARColoringBook/issues)
4. **Create Issue**: [New Issue](https://github.com/hhthunderbird/ARColoringBook/issues/new)
5. **Email Support**: support@felina.dev

### Community

- **GitHub Repository**: [https://github.com/hhthunderbird/ARColoringBook](https://github.com/hhthunderbird/ARColoringBook)
- **Discussions**: [GitHub Discussions](https://github.com/hhthunderbird/ARColoringBook/discussions)
- **Issues**: [GitHub Issues](https://github.com/hhthunderbird/ARColoringBook/issues)

### Commercial Support

For priority support, custom features, or consulting:
- **Email**: support@felina.dev
- **Response Time**: <24 hours for active license holders
- **Custom Development**: Available upon request

## ?? License

**Felina AR Coloring Book** is licensed under a **commercial license**.

### Quick Summary

? **You CAN**:
- Use in personal and commercial projects
- Modify source code for your projects
- Ship in paid applications
- Use in unlimited projects (single developer)

? **You CANNOT**:
- Resell or redistribute the package
- Share license with team members (they need their own)
- Extract and sell individual components
- Remove copyright notices

See [LICENSE.md](LICENSE.md) for complete terms.

### Attribution

Attribution is **not required** but appreciated:
```
Powered by Felina AR Coloring Book
https://github.com/hhthunderbird/ARColoringBook
```

## ?? Credits

### Developed By
**Felina Studios**
- GitHub: [@hhthunderbird](https://github.com/hhthunderbird)
- Website: [GitHub Repository](https://github.com/hhthunderbird/ARColoringBook)

### Technology Stack
- **Native Processing**: Custom C++ library for homography and quality estimation
- **Async Operations**: [UniTask](https://github.com/Cysharp/UniTask) by Cysharp (MIT License)
- **AR Framework**: [Unity AR Foundation](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@latest)
- **Mathematics**: [Unity Mathematics](https://docs.unity3d.com/Packages/com.unity.mathematics@latest)

### Special Thanks
- Unity Technologies for AR Foundation framework
- The Unity AR community for feedback and testing
- All customers and supporters

### Third-Party Licenses
- **UniTask**: MIT License - Copyright (c) 2019 Yoshifumi Kawai
- **AR Foundation**: Unity Companion License
- **Unity Mathematics**: Unity Companion License

---

## ?? Get Started Now

### Ready to bring your ideas to life?

1. **Install**: Follow [Installation Guide](#-installation)
2. **Configure**: Set up [Scanner](#-quick-start-5-minutes) in 5 minutes
3. **Deploy**: Build for [iOS](#ios-arkit) or [Android](#android-arcore)
4. **Create**: Explore [Use Cases](#-use-cases--examples) for inspiration

### Need Help?

- ?? Read the [docs](#-system-requirements)
- ?? Check [troubleshooting](#-troubleshooting)
- ?? Join [discussions](https://github.com/hhthunderbird/ARColoringBook/discussions)
- ?? Email [support@felina.dev](mailto:support@felina.dev)

---

**Version**: 2.0.0  
**Unity Support**: 2019.4 - 6.3+  
**AR Foundation**: 4.1.x - 6.x  
**Platforms**: iOS (ARKit), Android (ARCore)  
**License**: Commercial  
