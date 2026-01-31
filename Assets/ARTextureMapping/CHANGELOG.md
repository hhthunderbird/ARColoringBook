# Changelog

All notable changes to Felina AR Texture Mapping will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2024-06-XX

### Changed
- Minimum supported AR Foundation version is now 4.x (due to XRCpuImage and modern API requirements).
- Minimum supported Unity version is now 2019.4 LTS.
- Documentation, package manifest, and asmdefs updated to reflect new requirements.
- Solver parameters (inlier threshold, max iterations, refine with LM) are now configured in the `Settings` asset (ScriptableObject) instead of on the ARScannerManager component. Update your project settings via the Settings asset in the Inspector.

## [1.0.0] - 2024-01-15

### Initial Release

**Felina AR Texture Mapping v1.0.0** - Toolkit for Unity AR Foundation image tracking: real-time texture capture, prefab spawning, and material updates. Supports Unity 2019.4+ and AR Foundation 4.x+.

---

### Core Features

- **AR Foundation Integration**
  - Works with Unity 2019.4 LTS and newer
  - Requires AR Foundation 4.x or higher (no known max version)
  - Compatible with ARSessionOrigin and XR Origin setups
  - Platform abstraction for multi-version support

- **Real-Time Texture Capture**
  - Captures and unwarps textures from tracked images in real time
  - GPU-accelerated homography unwarp (custom shader)
  - Native C++ plugin for homography matrix calculation and quality scoring (`InternalSolver.so` for Android, `InternalSolver.a` for iOS)
  - Configurable output resolution
  - Auto-lock system: stops processing after successful capture
  - Event-driven: `OnTextureCaptured` event for easy integration

- **Quality and Stability Detection**
  - Native C++ quality scoring based on viewing angle, distance, and centering
  - Device stability detection to prevent blurry captures
  - Configurable quality threshold for auto-capture

- **Content Management**
  - **ARContentSpawner**: Automatically spawns prefabs on tracked images and applies captured textures
  - **ARPaintableObject**: Updates existing GameObjects’ materials with captured textures
  - Supports multiple material slots and custom texture property names
  - Works with Standard, URP, HDRP, and custom shaders

- **Developer Tools**
  - **Package Validator**: Checks setup and dependencies on import
  - **Sample Manager**: Detects and helps clean up incompatible AR Foundation samples
  - Custom inspectors for ARContentSpawner and ARPaintableObject

- **Performance and Platform Support**
  - Native plugins: `InternalSolver.so` (Android armv7/arm64), `InternalSolver.a` (iOS)
  - Efficient memory management with NativeArray and MaterialPropertyBlock
  - Mobile-optimized and supports AR Foundation Remote for in-editor testing

- **Documentation and Samples**
  - Complete README, installation, and multi-version support guides
  - Example scene with pre-configured AR setup, sample images, and 3D content

- **License**
  - Single-purchase Asset Store license for use by the buyer in their projects

---

**References:**
All information is based on the package's included documentation and source files. No features or claims have been added that are not present in these files.
