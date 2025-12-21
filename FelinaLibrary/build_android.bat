@echo off
:: Use the new Junction path (No spaces allowed!)
SET NDK_PATH=C:\UnityNDK

echo Using NDK at: %NDK_PATH%

%NDK_PATH%\build\ndk-build.cmd NDK_PROJECT_PATH=. APP_BUILD_SCRIPT=Android.mk APP_ABI=arm64-v8a

REM Copy .so to Unity Plugins folder
set SRC_SO=libs\arm64-v8a\libFelina.so
set DST_DIR=..\Assets\Plugins\Android\

if exist "%SRC_SO%" (
    if not exist "%DST_DIR%" mkdir "%DST_DIR%"
    echo Copying %SRC_SO% to %DST_DIR%\
    copy /Y "%SRC_SO%" "%DST_DIR%\" >nul
    if %ERRORLEVEL% EQU 0 (
        echo Copied libFelina.so to Unity Android plugins folder.
    ) else (
        echo Failed to copy %SRC_SO% to %DST_DIR% (error %ERRORLEVEL%).
    )
) else (
    echo Build output %SRC_SO% not found.
)

pause
