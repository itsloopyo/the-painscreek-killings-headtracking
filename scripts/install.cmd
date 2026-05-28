@echo off
:: ============================================
:: Painscreek Killings - Install
:: ============================================
:: Thin wrapper - install body lives in cameraunlock-core/scripts/install-body-cecil.cmd.

:: --- CONFIG BLOCK ---
set "GAME_ID=painscreek-killings"
set "MOD_DISPLAY_NAME=Painscreek Head Tracking"
set "MOD_DLLS=PainscreekHeadTracking.dll CameraUnlock.Core.dll Mono.Cecil.dll"
set "MOD_INTERNAL_NAME=PainscreekHeadTracking"
set "MOD_VERSION=1.0.0"
set "STATE_FILE=.headtracking-state.json"
set "FRAMEWORK_TYPE=MonoCecil"
set "MANAGED_SUBFOLDER=Painscreek_Data\Managed"
set "ASSEMBLY_DLL=Assembly-CSharp.dll"
set "PATCHER_FILE=BootstrapPatcher.cs"
set "MOD_CONTROLS=Controls:&echo   Home/Ctrl+Shift+T - Recenter&echo   End/Ctrl+Shift+Y  - Toggle tracking&echo   PgUp/Ctrl+Shift+G - Cycle tracking mode&echo   PgDn/Ctrl+Shift+H - Toggle yaw mode"
:: --- END CONFIG BLOCK ---

set "WRAPPER_DIR=%~dp0"
set "_BODY=%WRAPPER_DIR%shared\install-body-cecil.cmd"
if not exist "%_BODY%" set "_BODY=%WRAPPER_DIR%..\cameraunlock-core\scripts\install-body-cecil.cmd"
if not exist "%_BODY%" (
    echo ERROR: install-body-cecil.cmd not found in shared\ or ..\cameraunlock-core\scripts\.
    echo If this is a release ZIP, re-download it from GitHub ^(corrupt installer^).
    echo If this is the dev tree, run: git submodule update --init --recursive
    exit /b 1
)
call "%_BODY%" %*
exit /b %errorlevel%