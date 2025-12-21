#!/bin/bash
# Test script to verify FelinaLibrary builds correctly

set -e  # Exit on error

echo "=================================="
echo "Felina Library Build Test"
echo "=================================="
echo ""

# Check if we're in the right directory
if [ ! -f "CMakeLists.txt" ]; then
    echo "? Error: Must run from FelinaLibrary directory"
    echo "Usage: cd FelinaLibrary && ./test-build.sh"
    exit 1
fi

# Check if source file exists
if [ ! -f "src/Felina.cpp" ]; then
    echo "? Error: src/Felina.cpp not found"
    exit 1
fi

echo "? Source file found: src/Felina.cpp"
echo ""

# Create build directory
BUILD_DIR="build/test-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

echo "?? Build directory: $BUILD_DIR"
echo ""

# Configure
echo "??  Configuring CMake..."
if cmake ../.. -G Ninja -DCMAKE_BUILD_TYPE=Release; then
    echo "? CMake configuration successful"
else
    echo "? CMake configuration failed"
    exit 1
fi
echo ""

# Build
echo "?? Building..."
if ninja; then
    echo "? Build successful"
else
    echo "? Build failed"
    exit 1
fi
echo ""

# Check output
echo "?? Build artifacts:"
if [ -f "libFelina.a" ]; then
    echo "  ? libFelina.a (static library)"
    file libFelina.a
elif [ -f "libFelina.so" ]; then
    echo "  ? libFelina.so (shared library)"
    file libFelina.so
elif [ -f "libFelina.dylib" ]; then
    echo "  ? libFelina.dylib (dynamic library)"
    file libFelina.dylib
elif [ -f "Felina.dll" ]; then
    echo "  ? Felina.dll (Windows DLL)"
    file Felina.dll 2>/dev/null || ls -lh Felina.dll
else
    echo "  ??  No library output found"
    ls -la
fi
echo ""

# Test install
echo "?? Testing install..."
if ninja install; then
    echo "? Install successful"
    echo "   Check: $(pwd)/../../install/"
    ls -R ../../install/ 2>/dev/null || echo "   (Install directory structure)"
else
    echo "? Install failed"
    exit 1
fi
echo ""

echo "=================================="
echo "? All tests passed!"
echo "=================================="
echo ""
echo "Build artifacts location:"
echo "  $(pwd)"
echo ""
echo "To clean up:"
echo "  rm -rf $(pwd)"
