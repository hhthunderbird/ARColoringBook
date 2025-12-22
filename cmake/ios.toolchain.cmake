# iOS Toolchain for CMake
# Based on the official iOS CMake toolchain

# Standard settings
set(CMAKE_SYSTEM_NAME iOS)
set(CMAKE_SYSTEM_VERSION 1)
set(CMAKE_OSX_SYSROOT iphoneos)

# Supported platforms
set(PLATFORM "OS64" CACHE STRING "Target iOS platform")
set_property(CACHE PLATFORM PROPERTY STRINGS "OS" "OS64" "SIMULATOR" "SIMULATORARM64" "SIMULATOR64")

# Platform-specific settings
if(PLATFORM STREQUAL "OS")
    set(CMAKE_OSX_ARCHITECTURES "armv7;armv7s;arm64")
    set(CMAKE_OSX_SYSROOT iphoneos)
    set(DEPLOYMENT_TARGET "9.0")
elseif(PLATFORM STREQUAL "OS64")
    set(CMAKE_OSX_ARCHITECTURES "arm64")
    set(CMAKE_OSX_SYSROOT iphoneos)
    set(DEPLOYMENT_TARGET "11.0")
elseif(PLATFORM STREQUAL "SIMULATOR")
    set(CMAKE_OSX_ARCHITECTURES "i386;x86_64")
    set(CMAKE_OSX_SYSROOT iphonesimulator)
    set(DEPLOYMENT_TARGET "9.0")
elseif(PLATFORM STREQUAL "SIMULATORARM64")
    set(CMAKE_OSX_ARCHITECTURES "arm64")
    set(CMAKE_OSX_SYSROOT iphonesimulator)
    set(DEPLOYMENT_TARGET "14.0")
elseif(PLATFORM STREQUAL "SIMULATOR64")
    set(CMAKE_OSX_ARCHITECTURES "x86_64")
    set(CMAKE_OSX_SYSROOT iphonesimulator)
    set(DEPLOYMENT_TARGET "9.0")
endif()

# Set deployment target
set(CMAKE_OSX_DEPLOYMENT_TARGET ${DEPLOYMENT_TARGET} CACHE STRING "Minimum iOS version")

# Compiler flags
set(CMAKE_C_FLAGS_INIT "-fvisibility=hidden")
set(CMAKE_CXX_FLAGS_INIT "-fvisibility=hidden -fvisibility-inlines-hidden")

# Bitcode settings
option(ENABLE_BITCODE "Enable Apple Bitcode" OFF)
if(ENABLE_BITCODE)
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -fembed-bitcode")
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fembed-bitcode")
endif()

# ARC settings
option(ENABLE_ARC "Enable Automatic Reference Counting" ON)
if(ENABLE_ARC)
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -fobjc-arc")
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fobjc-arc")
endif()

# Set find root path modes
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)

# Skip compiler tests
set(CMAKE_C_COMPILER_WORKS TRUE)
set(CMAKE_CXX_COMPILER_WORKS TRUE)

message(STATUS "iOS Toolchain configured for ${PLATFORM}")
message(STATUS "Architecture: ${CMAKE_OSX_ARCHITECTURES}")
message(STATUS "Deployment Target: ${CMAKE_OSX_DEPLOYMENT_TARGET}")
message(STATUS "SDK: ${CMAKE_OSX_SYSROOT}")
