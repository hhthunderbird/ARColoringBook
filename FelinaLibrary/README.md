# Felina Native Library

This directory contains the native C++ code for the Felina AR Coloring Book Unity package.

## Overview

The Felina native library provides performance-critical operations for:
- **Homography calculation**: GPU-accelerated image unwarp
- **Quality estimation**: Real-time capture quality metrics
- **Stability detection**: Device motion analysis
- **License verification**: Secure license management
- **Watermark embedding**: DRM protection

## Building

### Prerequisites

- **CMake** 3.22.1 or later
- **C++ Compiler**: 
  - Windows: Visual Studio 2019+
  - macOS: Xcode Command Line Tools
  - Linux: GCC/Clang
  - iOS: Xcode with iOS SDK
  - Android: Android NDK

### Quick Build

#### Windows (DLL)
```bash
cd FelinaLibrary
mkdir build
cd build
cmake .. -G "Visual Studio 17 2022" -A x64
cmake --build . --config Release
```

Output: `build/Release/Felina.dll`

#### macOS (dylib)
```bash
cd FelinaLibrary
mkdir build
cd build
cmake .. -G Xcode
cmake --build . --config Release
```

Output: `build/Release/libFelina.dylib`

#### iOS (static library)
```bash
cd FelinaLibrary
mkdir -p build/ios
cd build/ios

cmake ../.. \
  -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=../../cmake/ios.toolchain.cmake \
  -DPLATFORM=OS64 \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_INSTALL_PREFIX=../../install/ios

ninja
ninja install
```

Output: `install/ios/lib/libFelina.a`

#### Android (shared library)
```bash
cd FelinaLibrary
mkdir -p build/android
cd build/android

cmake ../.. \
  -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK/build/cmake/android.toolchain.cmake \
  -DANDROID_ABI=arm64-v8a \
  -DANDROID_PLATFORM=android-24 \
  -DCMAKE_BUILD_TYPE=Release

ninja
```

Output: `build/android/libFelina.so`

## Integration with Unity

The GitHub Actions workflow automatically builds for all platforms and copies the libraries to:

```
Assets/
??? Plugins/
    ??? iOS/
    ?   ??? Felina.xcframework/    (modern iOS)
    ?   ??? libFelina.a             (fallback)
    ??? x86_64/
    ?   ??? Felina.dll              (Windows 64-bit)
    ??? macOS/
    ?   ??? libFelina.dylib         (macOS)
    ??? Android/
        ??? libs/
            ??? arm64-v8a/
                ??? libFelina.so    (Android 64-bit)
```

## Source Files

```
FelinaLibrary/
??? src/
?   ??? Felina.cpp           # Main implementation
??? include/                 # (optional) Public headers
??? CMakeLists.txt          # Build configuration
??? cmake/
?   ??? ios.toolchain.cmake # iOS cross-compilation
??? README.md               # This file
```

## API

The native library exports the following functions (declared in Unity C# scripts):

### Math Operations
```cpp
extern "C" {
    // Homography calculation
    void ComputeTransformMatrix(float width, float height, 
                                float screenW, float screenH,
                                void* screenPoints, void* resultMatrix);
    
    // Quality estimation
    float CalculateQuality(float3 camPos, float3 camFwd,
                          float3 imgPos, float3 imgUp,
                          float2 screenPos, float screenW, float screenH);
    
    // Stability detection
    bool CheckStability(float3 pos1, quaternion rot1,
                       float3 pos2, quaternion rot2,
                       float deltaTime, float maxMoveSpeed, float maxRotateSpeed);
}
```

### License Management
```cpp
extern "C" {
    // Validation
    void GetValidationURL(byte* encryptedInvoice, int len, 
                         char* urlBuffer, int bufferSize);
    bool ValidateLicense(byte* encryptedInvoice, int len, 
                        const char* responseJson);
    
    // Encryption
    void EncryptInvoiceString(const char* input, byte* output, int* length);
    
    // License check timing
    bool IsLicenseCheckDue(long lastCheck, long now);
    
    // Configuration
    int GetConfigInt(int configId);
    void GetConfigString(int configId, char* buffer, int maxLen);
    
    // Watermark
    void WatermarkCheckin();
    void* GetWatermarkData(int* size);
}
```

## Development

### Adding New Functions

1. Add implementation to `src/Felina.cpp`
2. Add C# P/Invoke declaration in Unity scripts
3. Mark function as `extern "C"` to prevent name mangling
4. Use C-compatible types (no C++ classes in API)

### Building Locally

```bash
# Test local build before pushing
cd FelinaLibrary
mkdir build && cd build
cmake .. -G Ninja
ninja

# Verify output
file libFelina.* # or Felina.dll on Windows
```

### CI/CD

The GitHub Actions workflow (`.github/workflows/build-and-package.yml`) automatically:
1. Builds for all platforms
2. Creates XCFramework for iOS
3. Copies libraries to Unity Plugins folder
4. Generates Unity .meta files
5. Creates .unitypackage
6. Publishes release on version tags

## Troubleshooting

### "No such file or directory: Felina.cpp"
- Ensure source file is in `src/` folder
- Check `CMakeLists.txt` references `src/Felina.cpp`

### iOS build fails
- Install Xcode Command Line Tools: `xcode-select --install`
- Ensure `cmake/ios.toolchain.cmake` exists
- Check iOS SDK is installed

### Linker errors in Unity
- Verify library is in correct Plugins folder
- Check platform import settings in `.meta` file
- For iOS, ensure using `[DllImport("__Internal")]`

## Platform Notes

### iOS
- Must use STATIC library (.a)
- XCFramework recommended for modern Xcode
- Fat library provides backward compatibility
- Link with `-fembed-bitcode` for App Store (optional)

### Android
- Use SHARED library (.so)
- Build for arm64-v8a minimum
- NDK r21+ recommended

### Windows
- DLL must be 64-bit (x86_64)
- MSVC runtime: /MD (release), /MDd (debug)

### macOS
- dylib requires code signing for distribution
- Universal binary (x86_64 + arm64) recommended

## License

See main project LICENSE.md

## Support

- Issues: https://github.com/hhthunderbird/ARColoringBook/issues
- Email: support@felina.dev

---

**Version**: 1.0.0  
**Last Updated**: 2024-01-15
