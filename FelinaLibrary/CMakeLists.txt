# 1. Update minimum version to silence the deprecation warning
cmake_minimum_required(VERSION 3.22.1)

# 2. Define the project name (Fixes "No project() command" warning)
project(Felina LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# 3. Add the library
# Use STATIC for iOS, SHARED for other platforms
if(IOS OR CMAKE_SYSTEM_NAME STREQUAL "iOS")
    add_library(Felina STATIC src/Felina.cpp)
else()
    add_library(Felina SHARED src/Felina.cpp)
endif()

# Optimization Flags
# Apply optimization flags only for Release builds to avoid conflicts with Debug runtimes
if(MSVC)
    target_compile_options(Felina PRIVATE
        $<$<CONFIG:Release>:/O2>
        $<$<CONFIG:Release>:/Ot>
        $<$<CONFIG:Release>:/fp:fast>
    )
else()
    target_compile_options(Felina PRIVATE
        $<$<CONFIG:Release>:-O3>
        $<$<CONFIG:Release>:-ffast-math>
    )
endif()

# Set Unity plugin output directories when building with CMake
if(APPLE)
    # iOS: place the compiled library and headers into Assets/Plugins/iOS
    set_target_properties(Felina PROPERTIES
        ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Assets/Plugins/iOS"
        LIBRARY_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Assets/Plugins/iOS"
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Assets/Plugins/iOS"
    )
elseif(WIN32)
    # Windows x86_64 DLL output
    set_target_properties(Felina PROPERTIES
        ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Assets/Plugins/x86_64"
        LIBRARY_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Assets/Plugins/x86_64"
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Assets/Plugins/x86_64"
    )
else()
    # Android and other platforms
    set_target_properties(Felina PROPERTIES
        ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Assets/Plugins/Android/"
        LIBRARY_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Assets/Plugins/Android/"
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Assets/Plugins/Android/"
    )
endif()

# Install rules for GitHub Actions workflow
install(TARGETS Felina
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
    RUNTIME DESTINATION bin
)

