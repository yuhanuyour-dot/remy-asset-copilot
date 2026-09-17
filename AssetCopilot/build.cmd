@echo off
setlocal
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set "DOTNET_CLI_HOME=%~dp0..\work\dotnet"
set "TEMP=%~dp0..\work"
set "TMP=%TEMP%"
if not exist "%TEMP%" mkdir "%TEMP%"
dotnet build "%~dp0AssetCopilot.csproj" -c Release -f net48 -o "%~dp0dist-universal" --nologo %*
exit /b %errorlevel%
