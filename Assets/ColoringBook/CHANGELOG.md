# Changelog

All notable changes to Felina AR Coloring Book will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2024-06-XX

### Changed
- Minimum supported AR Foundation version is now 4.1.x (due to XRCpuImage and modern API requirements).
- Minimum supported Unity version is now 2019.4 LTS.
- Documentation, package manifest, and asmdefs updated to reflect new requirements.
- Solver parameters (inlier threshold, max iterations, refine with LM) are now configured in the `Settings` asset (ScriptableObject) instead of on the ARScannerManager component. Update your project settings via the Settings asset in the Inspector.

## [1.0.0] - 2024-01-15

### ?? Initial Release

**Felina AR Coloring Book v1.0.0** - Professional AR image tracking and texture capture system for Unity.

---

### ? Core Features Added

#### AR Image Tracking System
- **Multi-Version Support** (Unity 2019.4 - 6.3+)
  - Automatic Unity version detection via assembly definitions
  - Conditional compilation for API compatibility
  - Single codebase works across all supported versions
  - No code changes needed when upgrading Unity

- **ARFoundation Integration**
  - Seamless integration with ARFoundation 2.x through 6.x
  - Platform abstraction via IARBridge interface
  - ARFoundationBridge implementation with version adapters
  - Support for both ARSessionOrigin (older) and XR Origin (newer)

- **Quality-Based Tracking**
  - Native C++ quality scoring algorithm
  - Factors: viewing angle, distance, screen coverage, centering
  - Real-time quality feedback (0.0 - 1.0 score)
  - Configurable quality threshold for auto-capture

- **Device Stability Detection**
  - Native C++ stability checking
  - Monitors camera movement and rotation
  - Prevents blurry captures from device shake
  - Configurable sensitivity thresholds

#### Real-Time Texture Capture

- **GPU-Accelerated Unwarp**
  - Custom homography shader for perspective correction
  - Native C++ homography matrix calculation
  - Runs entirely on GPU for maximum performance
  - Supports resolution from 256px to 2048px

- **Smart Capture System**
  - Automatic capture when quality threshold met
  - Auto-lock feature stops processing after capture
  - Frame-based camera feed caching (no redundant updates)
  - Async processing with UniTask integration

- **Flexible Output**
  - Configurable output resolution (256, 512, 1024, 2048)
  - RenderTexture-based output for zero-allocation updates
  - Event-driven architecture (`OnTextureCaptured` event)
  - Compatible with Standard, URP, and HDRP shaders

#### Content Management

- **ARContentSpawner Component**
  - Automatic prefab instantiation on tracked images
  - Custom inspector for easy prefab-to-image mapping
  - Dropdown selection from Reference Image Library
  - Show/hide based on tracking state
  - Automatic texture application to spawned objects

- **ARPaintableObject Component**
  - Apply captured textures to existing GameObjects
  - MaterialPropertyBlock-based updates (zero allocations)
  - Support for multiple material slots
  - Custom texture property name support
  - Compatible with any shader

- **Material Integration**
  - Zero-allocation texture updates via MaterialPropertyBlock
  - Support for `_MainTex`, `_BaseMap`, `_DrawingTex` properties
  - Works with Standard, URP Lit, and custom shaders
  - Automatic material property detection

#### Developer Tools

- **Package Validator**
  - Automatic validation on first import
  - Manual validation via `Felina > Validate Package Setup` menu
  - Detects Unity version and recommends AR Foundation version
  - Verifies all required dependencies installed
  - Provides fix instructions for missing packages

- **Sample Manager**
  - Automatic detection of incompatible AR Foundation samples
  - Dialog with options: Remove, Show in Explorer, Ignore
  - Manual cleanup via `Felina > Clean AR Foundation Samples`
  - Prevents compilation errors from version conflicts
  - SessionState-based to avoid repeated prompts

- **Custom Inspectors**
  - ARContentSpawner: Visual prefab mapping interface
  - Dropdown menus populated from Reference Image Library
  - Real-time validation and error messages
  - Auto-refresh when library changes

### ??? Technical Features

#### Performance Optimizations
- **Native C++ Plugin**
  - Homography calculation (perspective transform)
  - Quality estimation (multi-factor scoring)
  - Stability checking (motion detection)
  - Platform-specific builds (iOS, Android, Windows, macOS)

- **Memory Management**
  - NativeArray usage for efficient memory handling
  - Proper cleanup and disposal patterns
  - RenderTexture pooling ready (future)
  - MaterialPropertyBlock reuse

- **Frame Optimization**
  - Camera feed cached per frame (no redundant blits)
  - Quality checks only when device stable
  - GPU-only unwarp processing
  - Configurable max camera resolution (default 1920)

#### Platform Support
- **iOS (ARKit)**
  - iPhone 6S+ support
  - Metal graphics API required
  - P/Invoke to `__Internal` for native calls
  - Info.plist camera permission handling

- **Android (ARCore)**
  - API Level 24+ (Android 7.0+)
  - Vulkan and OpenGL ES 3.0 support
  - P/Invoke to `Felina.dll` for native calls
  - AndroidManifest camera permission handling

- **Editor Support**
  - Scene setup and configuration works in Editor
  - Desktop native library for development testing
  - AR Foundation Remote compatibility
  - Debug visualization tools

#### Architecture
- **Singleton Pattern** for ARScannerManager
- **Event-Driven** with C# events for loose coupling
- **Interface-Based** AR platform abstraction (IARBridge)
- **Struct-Based** data (ScanTarget) for performance
- **Async/Await** with UniTask for non-blocking operations

### ?? Package Structure

- **Runtime Scripts** (`Scripts/Runtime/`)
  - ARScannerManager.cs - Core scanning logic
  - ARContentSpawner.cs - Prefab spawning system
  - ARPaintableObject.cs - Material updater
  - ARFoundationBridge.cs - AR Foundation adapter
  - IARBridge.cs - Platform abstraction interface
  - ScanTarget.cs - Tracked image data structure

- **Editor Scripts** (`Editor/`)
  - PackageValidator.cs - Setup validation tool
  - ARFoundationSampleManager.cs - Sample cleanup tool
  - ARContentSpawnerEditor.cs - Custom inspector

- **Shaders** (`Shader/`)
  - HomographyUnwarp.shader - Perspective correction shader

- **Materials** (`Materials/`)
  - HomographyUnwarp.mat - Unwarp material preset

- **Native Plugins** (`Plugins/`)
  - iOS: `Felina.a` (ARM64 static library)
  - Android: `Felina.so` (ARM64/ARMv7 shared library)
  - Windows: `Felina.dll` (x64 DLL)
  - macOS: `Felina.bundle` (universal binary)

- **Assembly Definitions** (`Scripts/Runtime/`, `Editor/`)
  - Version detection symbols
  - Conditional compilation support
  - Clean dependency management

### ?? Documentation

- **README.md** - Complete overview, quick start, API reference
- **INSTALLATION.md** - Detailed installation for all Unity versions
- **MULTI_VERSION_SUPPORT.md** - Technical architecture explanation
- **SAMPLE_MANAGEMENT.md** - AR Foundation sample cleanup guide
- **CHANGELOG.md** (this file) - Version history
- **LICENSE.md** - Commercial license terms
- **CONTRIBUTING.md** - Contribution guidelines (for open source)

### ?? Sample Content

- **Example Scene** (`Scenes/ARFoundationSample.unity`)
  - Pre-configured AR setup
  - Sample reference images
  - Example 3D content
  - Working material setup

- **Sample Scripts** (examples in documentation)
  - Texture saving to file
  - Texture upload to server
  - Filter application
  - Custom consumers

### ?? Security & Licensing

- **License Management System**
  - License verification for commercial use
  - Development mode for testing
  - Invoice-based activation
  - Per-seat licensing enforcement

- **Build-Time Encryption**
  - Secure asset packaging
  - Native library protection
  - Prevents unauthorized redistribution

### ?? Known Issues
- None at release

### ?? Planned Features (v1.1+)
- Multi-target simultaneous capture
- Texture caching and retrieval system
- Performance profiling tools
- Visual debugging overlays
- Texture export functionality
- Cloud-based reference image libraries
- Additional sample scenes

---

## Version History

### Semantic Versioning
- **MAJOR** (1.x.x): Breaking changes, major new features
- **MINOR** (x.1.x): New features, backward compatible
- **PATCH** (x.x.1): Bug fixes, minor improvements

### Release Schedule
- **Patch releases**: As needed for critical bugs
- **Minor releases**: Quarterly (new features)
- **Major releases**: Annually or for breaking changes

---

## Migration Guides

### Future Versions
Migration guides will be provided for any breaking changes in major versions.

### Upgrading from Pre-Release
If you used pre-release versions (0.x.x), please contact support@felina.dev for upgrade assistance.

---

## Support & Feedback

- **Bug Reports**: [GitHub Issues](https://github.com/hhthunderbird/ARColoringBook/issues)
- **Feature Requests**: [GitHub Discussions](https://github.com/hhthunderbird/ARColoringBook/discussions)
- **Email Support**: support@felina.dev
- **Documentation**: See README.md and other guides

---

**Thank you for using Felina AR Coloring Book!** ??

**Last Updated**: June XX, 2024  
**Package Version**: 2.0.0  
**Unity Support**: 2019.4 LTS - 6.3+
**AR Foundation**: 4.1.x - 6.x
