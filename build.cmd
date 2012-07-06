REM ----------------------------------------------------------------------------
REM This file is subject to the terms and conditions defined in
REM file 'LICENSE.txt', which is part of this source code package.
REM ----------------------------------------------------------------------------

@echo off

:Build
cls

SET TARGET="Default"

IF NOT [%1]==[] (set TARGET="%1")
  
"lib\FAKE\Fake.exe" "build.fsx" "target=%TARGET%"

rem Bail if we're running a TeamCity build.
if defined TEAMCITY_PROJECT_NAME goto Quit

rem Loop the build script.
set CHOICE=nothing
echo (Q)uit, (Enter) runs the build again
set /P CHOICE= 
if /i "%CHOICE%"=="Q" goto :Quit

GOTO Build

:Quit
exit /b %errorlevel%