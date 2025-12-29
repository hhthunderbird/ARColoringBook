LOCAL_PATH := $(call my-dir)
include $(CLEAR_VARS)

LOCAL_MODULE    := Felina
LOCAL_SRC_FILES := src/Felina.cpp

APP_ABI := arm64-v8a
APP_PLATFORM := android-21
APP_OPTIM := release

# Optimization flags
APP_CFLAGS += -O3 -ffast-math -flto
APP_CPPFLAGS += -O3 -ffast-math -flto

# Android 15+ Compatibility: 16KB Page Alignment
# Required for ARM64 devices running Android 15 or later
LOCAL_CFLAGS += -fno-short-enums
LOCAL_LDFLAGS += -Wl,-z,max-page-size=16384

include $(BUILD_SHARED_LIBRARY)

# After building with ndk-build the .so will be placed under libs/arm64-v8a.
# The batch script copies it to Unity's Assets/Plugins/Android/


