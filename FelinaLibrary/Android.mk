LOCAL_PATH := $(call my-dir)
include $(CLEAR_VARS)

LOCAL_MODULE    := Felina
LOCAL_SRC_FILES := Felina.cpp

APP_ABI := arm64-v8a
APP_PLATFORM := android-21
APP_OPTIM := release
# Force aggressive optimization and NEON support
APP_CFLAGS += -O3 -ffast-math -flto
APP_CPPFLAGS += -O3 -ffast-math -flto

include $(BUILD_SHARED_LIBRARY)

# After building with ndk-build the .so will be placed under libs/arm64-v8a.
# The batch script copies it to Unity's Assets/Plugins/Android/


