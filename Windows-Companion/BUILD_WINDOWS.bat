@echo off
setlocal
set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%MSBUILD%" set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%MSBUILD%" (
  echo Visual Studio 2022 or Build Tools with .NET desktop build tools is required.
  pause
  exit /b 1
)
"%MSBUILD%" SmartHubRemote.sln /t:Rebuild /p:Configuration=Release /m
if errorlevel 1 pause & exit /b 1
echo.
echo Build completed: SmartHubRemote\bin\Release\SmartHubRemote.exe
pause

