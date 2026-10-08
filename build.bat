@echo off
rem ---------------------------------------------------------------------------
rem  GaeKai build script - uses the C# compiler that ships with Windows
rem  (.NET Framework 4.x), so no Visual Studio / .NET SDK is required.
rem
rem    build.bat          build dist\GaeKai.exe and the installer dist\GaeKai-Setup.exe
rem    build.bat test     build and run the unit tests
rem    build.bat icon     regenerate src\GaeKai.ico
rem ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] C# compiler not found. Please install .NET Framework 4.8.
    exit /b 1
)

rem /codepage:65001 is required: source files are UTF-8 and contain Thai text.
set "CSC_FLAGS=/nologo /codepage:65001 /warn:4 /optimize+"

if not exist dist mkdir dist

if /i "%~1"=="test" goto test
if /i "%~1"=="icon" goto icon

:build
"%CSC%" %CSC_FLAGS% /target:winexe /platform:anycpu ^
    /out:dist\GaeKai.exe ^
    /win32icon:src\GaeKai.ico ^
    /win32manifest:src\app.manifest ^
    /resource:src\GaeKai.ico,GaeKai.ico ^
    /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    src\*.cs
if errorlevel 1 (
    echo [ERROR] Build failed.
    exit /b 1
)
echo Built dist\GaeKai.exe

rem The installer embeds the GaeKai.exe that was just built.
"%CSC%" %CSC_FLAGS% /target:winexe /platform:anycpu ^
    /out:dist\GaeKai-Setup.exe ^
    /win32icon:src\GaeKai.ico ^
    /win32manifest:src\app.manifest ^
    /resource:dist\GaeKai.exe,GaeKai.exe ^
    /resource:src\GaeKai.ico,GaeKai.ico ^
    /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    installer\*.cs src\InstallLayout.cs src\NativeMethods.cs
if errorlevel 1 (
    echo [ERROR] Installer build failed.
    exit /b 1
)
echo Built dist\GaeKai-Setup.exe
exit /b 0

:test
"%CSC%" %CSC_FLAGS% /target:exe /out:dist\GaeKai.Tests.exe ^
    /r:System.Windows.Forms.dll ^
    tests\*.cs src\LayoutConverter.cs src\Hotkey.cs src\NativeMethods.cs
if errorlevel 1 (
    echo [ERROR] Test build failed.
    exit /b 1
)
dist\GaeKai.Tests.exe
exit /b %errorlevel%

:icon
"%CSC%" %CSC_FLAGS% /target:exe /out:dist\IconGen.exe /r:System.Drawing.dll tools\IconGen.cs
if errorlevel 1 exit /b 1
dist\IconGen.exe src\GaeKai.ico dist\icon-preview.png
exit /b %errorlevel%
