#!/bin/bash
# macOS build script for Felina native library

set -e

echo "Building Felina for macOS (arm64 + x86_64)..."

# Create build directory
BUILD_DIR="build_macos"
mkdir -p "$BUILD_DIR"

# Build for arm64 (Apple Silicon)
echo "Building for arm64..."
cmake -S . -B "$BUILD_DIR/arm64" \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_OSX_ARCHITECTURES=arm64 \
  -DCMAKE_OSX_DEPLOYMENT_TARGET=10.15

cmake --build "$BUILD_DIR/arm64" --config Release

# Build for x86_64 (Intel)
echo "Building for x86_64..."
cmake -S . -B "$BUILD_DIR/x86_64" \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_OSX_ARCHITECTURES=x86_64 \
  -DCMAKE_OSX_DEPLOYMENT_TARGET=10.15

cmake --build "$BUILD_DIR/x86_64" --config Release

# Create universal binary
echo "Creating universal binary..."
lipo -create \
  "$BUILD_DIR/arm64/libFelina.dylib" \
  "$BUILD_DIR/x86_64/libFelina.dylib" \
  -output "$BUILD_DIR/libFelina.dylib"

# Create Unity plugin directory
UNITY_PLUGIN_DIR="../Assets/Plugins/macOS"
mkdir -p "$UNITY_PLUGIN_DIR"

# Copy to Unity
cp "$BUILD_DIR/libFelina.dylib" "$UNITY_PLUGIN_DIR/"

echo "? Built and copied libFelina.dylib to $UNITY_PLUGIN_DIR"
echo "  Universal binary (arm64 + x86_64)"

# Optional: display architectures
echo ""
echo "Library architectures:"
lipo -info "$UNITY_PLUGIN_DIR/libFelina.dylib"
