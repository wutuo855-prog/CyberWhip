@echo off
setlocal DisableDelayedExpansion
set "WHIP_SOURCE=%~dp0"
set "WHIP_BIN=%LOCALAPPDATA%\CyberWhip\bin\v1.0.1"
if not defined LOCALAPPDATA set "WHIP_BIN=%TEMP%\CyberWhip\bin\v1.0.1"
set "WHIP_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%WHIP_CSC%" set "WHIP_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%WHIP_BIN%" mkdir "%WHIP_BIN%"
if not exist "%WHIP_BIN%" goto directory_error
if /I "%~1"=="/check" goto check
if /I "%~1"=="/rebuild" goto build
if exist "%WHIP_BIN%\CyberWhip.exe" goto launch

:build
if not exist "%WHIP_CSC%" goto compiler_error
echo Building CyberWhip v1.0.1 with the Windows built-in .NET compiler...
echo No downloads. No package installation. No administrator rights requested.
"%WHIP_CSC%" /nologo /target:winexe /main:CyberWhip.Program /platform:anycpu /optimize+ /codepage:65001 /utf8output /out:"%WHIP_BIN%\CyberWhip.exe" /win32manifest:"%WHIP_SOURCE%src\app.manifest" /win32icon:"%WHIP_SOURCE%src\app.ico" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll /resource:"%WHIP_SOURCE%sounds\A.wav",CyberWhip.Sounds.A.wav /resource:"%WHIP_SOURCE%sounds\B.wav",CyberWhip.Sounds.B.wav /resource:"%WHIP_SOURCE%sounds\C.wav",CyberWhip.Sounds.C.wav /resource:"%WHIP_SOURCE%sounds\D.wav",CyberWhip.Sounds.D.wav /resource:"%WHIP_SOURCE%sounds\E.wav",CyberWhip.Sounds.E.wav "%WHIP_SOURCE%src\*.cs" >"%WHIP_BIN%\build.log" 2>&1
if errorlevel 1 goto build_error
copy /Y "%WHIP_SOURCE%src\CyberWhip.exe.config" "%WHIP_BIN%\CyberWhip.exe.config" >nul
if errorlevel 1 goto directory_error
if /I "%~1"=="/rebuild" goto build_done

:launch
echo Starting CyberWhip. Find its icon in the system tray.
start "" "%WHIP_BIN%\CyberWhip.exe"
exit /b 0

:check
if not exist "%WHIP_CSC%" goto compiler_error
echo Building and running local checks. No network requests are made.
"%WHIP_CSC%" /nologo /target:exe /main:CyberWhip.SelfTest /platform:anycpu /optimize+ /codepage:65001 /utf8output /out:"%WHIP_BIN%\CyberWhip.Check.exe" /win32manifest:"%WHIP_SOURCE%src\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll "%WHIP_SOURCE%src\*.cs" >"%WHIP_BIN%\check_build.log" 2>&1
if errorlevel 1 goto check_build_error
copy /Y "%WHIP_SOURCE%src\CyberWhip.exe.config" "%WHIP_BIN%\CyberWhip.Check.exe.config" >nul
"%WHIP_BIN%\CyberWhip.Check.exe" >"%WHIP_BIN%\check.log" 2>&1
if errorlevel 1 goto check_error
type "%WHIP_BIN%\check.log"
echo.
echo Check log: "%WHIP_BIN%\check.log"
pause
exit /b 0

:build_done
echo Build completed: "%WHIP_BIN%\CyberWhip.exe"
echo You can now double-click START.cmd or the generated EXE.
pause
exit /b 0

:compiler_error
echo The built-in .NET Framework C# compiler was not found.
echo This package requires the Windows .NET Framework 4.x components.
echo It does not download or install them automatically.
echo Please use a Windows 10/11 or Server desktop with those components present.
pause
exit /b 1

:directory_error
echo Could not write to the current user's local application directory:
echo "%WHIP_BIN%"
echo Check available disk space and write permissions.
pause
exit /b 1

:build_error
echo Build failed. Close any running CyberWhip instance before rebuilding.
type "%WHIP_BIN%\build.log"
echo Build log: "%WHIP_BIN%\build.log"
pause
exit /b 1

:check_build_error
echo Check build failed:
type "%WHIP_BIN%\check_build.log"
echo Log: "%WHIP_BIN%\check_build.log"
pause
exit /b 1

:check_error
type "%WHIP_BIN%\check.log"
echo Some checks failed. Log: "%WHIP_BIN%\check.log"
pause
exit /b 1
