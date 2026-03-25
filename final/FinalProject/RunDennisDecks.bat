@echo off
REM Dennis's Deck Device Launcher
REM This script runs the FinalProject application

setlocal enabledelayedexpansion

REM Get the directory where this batch file is located
set SCRIPT_DIR=%~dp0

REM Always build latest Release so launcher uses current UI/code
echo Building latest Dennis's Deck Device...
cd /d "%SCRIPT_DIR%"
dotnet build FinalProject.csproj -c Release

if errorlevel 1 (
    echo.
    echo ERROR: Build failed.
    exit /b 1
)

if exist "%SCRIPT_DIR%bin\Release\net6.0-windows\FinalProject.exe" (
    "%SCRIPT_DIR%bin\Release\net6.0-windows\FinalProject.exe" %*
    exit /b !errorlevel!
)

echo.
echo ERROR: Could not find FinalProject.exe
echo Please ensure the project is built: dotnet build FinalProject.csproj
exit /b 1
