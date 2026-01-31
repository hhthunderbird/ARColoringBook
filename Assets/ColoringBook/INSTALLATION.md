# Felina AR Texture Mapping - Installation Guide

Felina AR Texture Mapping requires:
- Unity 2019.4 LTS or newer
- AR Foundation 4.x or higher (no known max version)
- iOS (ARKit) or Android (ARCore) device for runtime

## Steps
1. Import the package from the Unity Asset Store.
2. Ensure AR Foundation 4.x+ is installed via Package Manager.
3. Add ARKit XR Plugin (iOS) or ARCore XR Plugin (Android) as needed.
4. Open the included sample scene or add the provided components to your own scene:
   - ARScannerManager
   - ARContentSpawner
   - ARPaintableObject (optional)
5. Assign your XR Reference Image Library to the ARTrackedImageManager.
6. Configure output resolution and quality threshold as desired.
7. Build and deploy to an iOS or Android device.

## Platform Notes
- **iOS:** Requires ARKit-compatible device and Metal graphics API. Native plugin: `InternalSolver.a` (iOS)
- **Android:** Requires ARCore-compatible device and Android 7.0+. Native plugin: `InternalSolver.so` (Android armv7/arm64)
- **macOS:** Not supported for runtime AR.
- **Editor:** Use for configuration and setup only; AR tracking requires device.

## License
Single-purchase Asset Store license. The buyer may use the asset in their Unity projects.

## References
All information is based on the package's included documentation and source files. No unsupported features are claimed.
