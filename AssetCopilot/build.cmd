@echo off
setlocal
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set "DOTNET_CLI_HOME=%~dp0..\work\dotnet"
set "TEMP=%~dp0..\work"
set "TMP=%TEMP%"
if not exist "%TEMP%" mkdir "%TEMP%"
dotnet build "%~dp0AssetCopilot.csproj" -c Release -o "%~dp0dist-next" --nologo %*
exit /b %errorlevel%
