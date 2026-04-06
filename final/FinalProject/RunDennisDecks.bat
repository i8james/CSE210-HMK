@echo off
REM Dennis's Deck Device Launcher
REM This script runs the FinalProject application

setlocal enabledelayedexpansion

REM Get the directory where this batch file is located
set SCRIPT_DIR=%~dp0

REM Build latest Debug output in lock-safe folder so we can launch reliably
echo Building latest Dennis's Deck Device...
cd /d "%SCRIPT_DIR%"
dotnet build FinalProject.csproj -c Debug /p:UseAppHost=false /p:BaseOutputPath=bin_locksafe\

if errorlevel 1 (
    echo.
    echo ERROR: Build failed.
    exit /b 1
)

set APP_EXE=
set APP_DLL=

if exist "%SCRIPT_DIR%bin_locksafe\Debug\net8.0-windows\FinalProject.exe" set APP_EXE=%SCRIPT_DIR%bin_locksafe\Debug\net8.0-windows\FinalProject.exe
if exist "%SCRIPT_DIR%bin_locksafe\Debug\net8.0-windows\FinalProject.dll" set APP_DLL=%SCRIPT_DIR%bin_locksafe\Debug\net8.0-windows\FinalProject.dll

if defined APP_DLL (
    if defined LOCALAPPDATA (
        set RUN_ROOT=%LOCALAPPDATA%\FizbanDeckStudio
    ) else (
        set RUN_ROOT=%TEMP%\FizbanDeckStudio
    )

    set RUN_DIR=!RUN_ROOT!\run
    if not exist "!RUN_DIR!" mkdir "!RUN_DIR!"

    echo Preparing runtime files in: !RUN_DIR!\
    xcopy /y /q "%SCRIPT_DIR%bin_locksafe\Debug\net8.0-windows\*" "!RUN_DIR!\" >nul

    powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path '!RUN_DIR!' -File | Unblock-File" >nul 2>nul

    echo Launching: dotnet "!RUN_DIR!\FinalProject.dll"
    dotnet "!RUN_DIR!\FinalProject.dll" %*
    if errorlevel 1 goto :policy_blocked
    exit /b 0
)

if defined APP_EXE (
    echo Launching: %APP_EXE%
    "%APP_EXE%" %*
    if errorlevel 1 goto :policy_blocked
    exit /b 0
)

echo.
echo ERROR: Could not find FinalProject launch target (.exe or .dll)
echo Checked:
echo   bin_locksafe\Debug\net8.0-windows\
exit /b 1

:policy_blocked
echo.
echo ERROR: Windows application control blocked launching FinalProject.
echo Try these fixes:
echo   1. Move the repository out of Downloads into a trusted folder (for example: C:\Dev\CSE210-HMK).
echo   2. Right-click the project folder ^> Properties ^> check "Unblock" if shown.
echo   3. Run this once in PowerShell:
echo      Get-ChildItem -Path "%SCRIPT_DIR%" -Recurse -File ^| Unblock-File
echo   4. Re-run RunDennisDecks.bat.
exit /b 1
