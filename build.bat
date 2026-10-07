@echo off
setlocal
set TMPOUT=%~dp0publish\.tmp
dotnet publish "%~dp0src\BF2Presence" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "%TMPOUT%" || exit /b 1

copy /y "%TMPOUT%\BF2Presence.exe" "%~dp0publish\" >nul || (echo. & echo ERROR: could not replace publish\BF2Presence.exe. Close BF2Presence and run build.bat again. & exit /b 1)
if not exist "%~dp0publish\appsettings.json" copy "%TMPOUT%\appsettings.json" "%~dp0publish\" >nul
if not exist "%~dp0publish\offsets.json" copy "%TMPOUT%\offsets.json" "%~dp0publish\" >nul
rmdir /s /q "%TMPOUT%"

echo.
echo Built: %~dp0publish\BF2Presence.exe
