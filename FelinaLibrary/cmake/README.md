# CMake Configuration Files

This directory contains CMake toolchain files and configuration scripts for cross-platform builds.

## iOS Toolchain (`ios.toolchain.cmake`)

CMake toolchain file for building iOS libraries (both device and simulator).

### Supported Platforms

- **OS64**: iOS Device (arm64 architecture)
- **SIMULATORARM64**: iOS Simulator on Apple Silicon Macs (arm64)
- **SIMULATOR64**: iOS Simulator on Intel Macs (x86_64)

### Usage

```bash
cmake .. \
  -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=../cmake/ios.toolchain.cmake \
  -DPLATFORM=OS64 \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_INSTALL_PREFIX=../install/ios \
  -DENABLE_BITCODE=OFF \
  -DENABLE_ARC=ON
```

### Configuration Options

| Option | Values | Description |
|--------|--------|-------------|
| `PLATFORM` | OS64, SIMULATORARM64, SIMULATOR64 | Target platform |
| `ENABLE_BITCODE` | ON/OFF | Enable/disable bitcode generation |
| `ENABLE_ARC` | ON/OFF | Enable/disable Automatic Reference Counting |
| `CMAKE_OSX_DEPLOYMENT_TARGET` | "12.0" (default) | Minimum iOS version |

### Examples

#### Build for iOS Device (arm64)
```bash
mkdir -p build/ios && cd build/ios
cmake ../.. \
  -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=../../cmake/ios.toolchain.cmake \
  -DPLATFORM=OS64 \
  -DCMAKE_BUILD_TYPE=Release \
  -DENABLE_BITCODE=OFF \
  -DENABLE_ARC=ON
ninja
```

#### Build for iOS Simulator (Apple Silicon)
```bash
mkdir -p build/ios-sim && cd build/ios-sim
cmake ../.. \
  -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=../../cmake/ios.toolchain.cmake \
  -DPLATFORM=SIMULATORARM64 \
  -DCMAKE_BUILD_TYPE=Release \
  -DENABLE_BITCODE=OFF \
  -DENABLE_ARC=ON
ninja
```

#### Create Universal Binary (Device + Simulator)
```bash
# Build device library
mkdir -p build/ios && cd build/ios
cmake ../.. -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=../../cmake/ios.toolchain.cmake \
  -DPLATFORM=OS64 \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_INSTALL_PREFIX=../../install/ios
ninja install

# Build simulator library
cd ../..
mkdir -p build/ios-simulator && cd build/ios-simulator
cmake ../.. -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=../../cmake/ios.toolchain.cmake \
  -DPLATFORM=SIMULATORARM64 \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_INSTALL_PREFIX=../../install/ios-simulator
ninja install

# Create XCFramework
cd ../..
xcodebuild -create-xcframework \
  -library install/ios/lib/libFelina.a \
  -library install/ios-simulator/lib/libFelina.a \
  -output install/Felina.xcframework
```

## Requirements

### macOS
- Xcode Command Line Tools
- CMake 3.22.1 or later
- Ninja build system (recommended)

### Installation
```bash
# Install Homebrew (if not installed)
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

# Install dependencies
brew install cmake ninja
```

## Troubleshooting

### Error: Could not find toolchain file
**Solution:** Ensure the path to the toolchain file is correct relative to your build directory.
```bash
# If in FelinaLibrary/build/ios
-DCMAKE_TOOLCHAIN_FILE=../../cmake/ios.toolchain.cmake

# If in FelinaLibrary/
-DCMAKE_TOOLCHAIN_FILE=./cmake/ios.toolchain.cmake
```

### Error: CMAKE_MAKE_PROGRAM is not set
**Solution:** Install Ninja or use Xcode generator
```bash
brew install ninja
# OR
cmake .. -G "Xcode" -DCMAKE_TOOLCHAIN_FILE=...
```

### Error: No architectures to compile for
**Solution:** Ensure you're on macOS with Xcode installed
```bash
xcode-select --install
```

## Additional Resources

- [CMake iOS Documentation](https://cmake.org/cmake/help/latest/manual/cmake-toolchains.7.html#cross-compiling-for-ios-tvos-or-watchos)
- [Apple Developer Documentation](https://developer.apple.com/documentation/)
- [iOS CMake Toolchain (leetal)](https://github.com/leetal/ios-cmake)
