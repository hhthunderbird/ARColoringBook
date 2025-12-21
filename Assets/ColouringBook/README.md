# Felina AR Coloring Book

Professional AR image tracking and texture capture system for Unity, designed for interactive coloring book experiences.

## Features

- **Advanced Image Tracking**: ARFoundation-based image tracking with stability detection
- **Real-time Texture Capture**: GPU-accelerated homography unwarp for scanned textures
- **Content Spawning**: Automatic AR content placement on tracked images
- **Material Integration**: Seamless texture application using MaterialPropertyBlock
- **Multi-platform**: iOS (ARKit), Android (ARCore) support
- **Performance Optimized**: Configurable resolution, auto-locking, and smart caching

## Requirements

- Unity 2022.3 or later
- AR Foundation 5.0+
- ARKit XR Plugin 5.0+ (iOS)
- ARCore XR Plugin 5.0+ (Android)
- Unity Mathematics package

## Installation

### Via Unity Package Manager (UPM)
1. Open Unity Package Manager (Window > Package Manager)
2. Click the '+' button and select "Add package from disk"
3. Navigate to `Assets/ColouringBook/package.json`
4. Click "Open"

### Manual Installation
1. Import the `ColouringBook.unitypackage` from the Asset Store
2. Ensure AR Foundation and platform-specific AR packages are installed
3. Configure XR Plug-in Management for your target platform(s)

## Quick Start

### 1. Setup AR Scene
- Add an AR Session and AR Session Origin to your scene
- Add an AR Tracked Image Manager component
- Assign your XR Reference Image Library

### 2. Configure Scanner
- Create a GameObject and add `ARScannerManager` component
- Assign the unwarp material (provided in Materials folder)
- Adjust capture threshold and output resolution for your needs

### 3. Add Paintable Objects
- Add `ARPaintableObject` component to objects you want to receive captured textures
- Select the reference image from your library
- Configure material index and texture property name

### 4. Spawn AR Content (Optional)
- Add `ARContentSpawner` component to spawn prefabs on tracked images
- Use the custom inspector to assign prefabs to specific reference images

## Architecture

```
ARFoundationBridge (IARBridge)
    ? Events
ARScannerManager
    ? OnTextureCaptured
ARPaintableObject / Custom Consumers
```

### Core Components

- **IARBridge**: Interface for AR platform abstraction
- **ARFoundationBridge**: ARFoundation implementation of IARBridge
- **ARScannerManager**: Manages image scanning and texture capture
- **ARContentSpawner**: Spawns GameObjects on tracked images
- **ARPaintableObject**: Applies captured textures to materials

## Configuration

### Scanner Settings
- **Output Resolution**: Texture resolution (512-2048, default 1024)
- **Capture Threshold**: Quality threshold for auto-lock (0-1, default 0.85)
- **Auto Lock**: Lock capture after threshold is met
- **Max Move/Rotate Speed**: Device stability thresholds

### Mobile Optimization
For better performance on mobile devices:
- Reduce output resolution to 512 or 256
- Enable auto-lock to stop processing after capture
- Adjust max feed resolution in ARFoundationBridge (default 1920)

## Platform-Specific Notes

### iOS
- Requires ARKit-capable device (iPhone 6S or later)
- Metal graphics API required
- Camera permission in Info.plist

### Android
- Requires ARCore-supported device
- Vulkan or OpenGL ES 3.0+
- Camera permission in AndroidManifest.xml

## Troubleshooting

### GL_INVALID_ENUM Error
- The package automatically detects supported RenderTexture formats
- If issues persist, check your Graphics API settings

### Texture Not Appearing
- Verify reference image name matches in ARPaintableObject
- Check material property name matches your shader
- Ensure ARScannerManager is receiving OnTargetAdded events

### Poor Capture Quality
- Increase output resolution
- Ensure good lighting conditions
- Keep device stable during capture
- Adjust capture threshold

## API Reference

### ARScannerManager
```csharp
// Subscribe to texture capture events
ARScannerManager.Instance.OnTextureCaptured += OnTextureReceived;

// Get cached texture for a target
RenderTexture texture = ARScannerManager.Instance.GetCapturedTexture(targetName);
```

### IARBridge
```csharp
public interface IARBridge
{
    event Action<ScanTarget> OnTargetAdded;
    Camera GetARCamera();
    RenderTexture GetCameraFeedRT();
    RenderTextureSettings RenderTextureSettings { get; }
}
```

## Support

- Documentation: [GitHub Wiki](https://github.com/hhthunderbird/ARColoringBook/wiki)
- Issues: [GitHub Issues](https://github.com/hhthunderbird/ARColoringBook/issues)
- Email: support@felina.dev

## License

This package is licensed under a custom commercial license. See LICENSE.md for details.

## Credits

- Native plugin: Custom C++ homography and quality estimation
- UniTask: Asynchronous operations
- AR Foundation: Unity's AR framework

---

**Version**: 1.0.0  
**Unity**: 2022.3+  
**Platforms**: iOS, Android
