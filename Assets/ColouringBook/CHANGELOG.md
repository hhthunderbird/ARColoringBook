# Changelog

All notable changes to Felina AR Coloring Book will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2024-01-15

### Added
- Initial release of Felina AR Coloring Book
- Core AR image tracking with ARFoundation integration
- Real-time texture capture with GPU-accelerated homography unwarp
- ARScannerManager for quality-based image scanning
- ARContentSpawner for automatic prefab instantiation on tracked images
- ARPaintableObject for texture application to materials
- MaterialPropertyBlock-based texture updates (zero material instance allocation)
- Multi-platform support (iOS ARKit, Android ARCore)
- Configurable resolution and quality thresholds
- Auto-lock feature to optimize performance
- Device stability detection
- Native plugin integration for performance-critical operations
- Platform-specific P/Invoke handling (iOS __Internal, desktop/Android Felina.dll)
- Centralized constants in FelinaConstants
- Sample scene demonstrating AR coloring book workflow
- Custom inspectors for editor workflow
- License management system
- Build-time encryption for secure asset packaging

### Technical Features
- Safe RenderTexture format selection with SystemInfo checks
- Singleton pattern for manager components
- Event-driven architecture for loose coupling
- NativeArray usage for efficient memory management
- Frame-based camera feed caching to avoid redundant blits
- Support for custom AR bridges via IARBridge interface

### Documentation
- Complete README with quick start guide
- API reference documentation
- Troubleshooting guide
- Platform-specific setup instructions

### Known Issues
- None at release

---

## [Unreleased]

### Planned Features
- Additional sample scenes (multiple markers, advanced shaders)
- Performance profiling tools
- Visual debugging overlays
- Texture export functionality
- Multi-marker scanning
- Cloud-based reference image libraries

---

**Note**: This is the initial release version. Future updates will maintain backward compatibility where possible, with breaking changes clearly marked and migration guides provided.
