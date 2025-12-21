#!/bin/bash
# iOS build script for Felina native library

set -e

echo "Building Felina for iOS (arm64 device + simulator)..."

# Create build directory
BUILD_DIR="build_ios"
mkdir -p "$BUILD_DIR"

# Build for iOS device (arm64)
echo "Building for iOS device (arm64)..."
cmake -S . -B "$BUILD_DIR/device" \
  -DCMAKE_SYSTEM_NAME=iOS \
  -DCMAKE_OSX_ARCHITECTURES=arm64 \
  -DCMAKE_OSX_DEPLOYMENT_TARGET=12.0 \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_XCODE_ATTRIBUTE_ONLY_ACTIVE_ARCH=NO

cmake --build "$BUILD_DIR/device" --config Release

# Build for iOS simulator (arm64 + x86_64)
echo "Building for iOS simulator (arm64 + x86_64)..."
cmake -S . -B "$BUILD_DIR/simulator" \
  -DCMAKE_SYSTEM_NAME=iOS \
  -DCMAKE_OSX_ARCHITECTURES="arm64;x86_64" \
  -DCMAKE_OSX_DEPLOYMENT_TARGET=12.0 \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_OSX_SYSROOT=iphonesimulator \
  -DCMAKE_XCODE_ATTRIBUTE_ONLY_ACTIVE_ARCH=NO

cmake --build "$BUILD_DIR/simulator" --config Release

# Create XCFramework (recommended for iOS)
echo "Creating XCFramework..."
xcodebuild -create-xcframework \
  -library "$BUILD_DIR/device/libFelina.dylib" \
  -library "$BUILD_DIR/simulator/libFelina.dylib" \
  -output "$BUILD_DIR/Felina.xcframework"

# Create Unity plugin directory
UNITY_PLUGIN_DIR="../Assets/Plugins/iOS"
mkdir -p "$UNITY_PLUGIN_DIR"

# Copy device library (Unity builds for device by default)
cp "$BUILD_DIR/device/libFelina.dylib" "$UNITY_PLUGIN_DIR/libFelina.a"

echo "✓ Built and copied libFelina.a to $UNITY_PLUGIN_DIR"
echo "  iOS device (arm64)"
echo ""
echo "Note: XCFramework created at $BUILD_DIR/Felina.xcframework"
echo "      for advanced iOS/simulator support"

# Verify macOS library
echo "Verifying macOS library..."
if [ -f "Assets/Plugins/macOS/libFelina.dylib" ]; then
  echo "✓ libFelina.dylib exists in Assets/Plugins/macOS/"
  file Assets/Plugins/macOS/libFelina.dylib
else
  echo "✗ libFelina.dylib not found in Assets/Plugins/macOS/"
  exit 1
fi
