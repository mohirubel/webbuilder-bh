@echo off
rem ------------------------------------------------------------------
rem  Run Web Builders locally: website + Domains API
rem  http://localhost:5080  (opens domains.html in your browser)
rem  Keys come from your .NET user-secrets (docs/local-development.md).
rem  Stop with Ctrl+C.
rem ------------------------------------------------------------------
setlocal
cd /d "%~dp0src\WebBuilders.Domains.Api"

rem A copy left running from earlier would block port 5080.
powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe' OR Name='WebBuilders.Domains.Api.exe'\" | Where-Object { $_.CommandLine -match 'WebBuilders\.Domains\.Api\.(dll|exe)' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }" >nul 2>&1

dotnet run --launch-profile http
set EXITCODE=%errorlevel%

call "%~dp0deploy\pause-if-double-clicked.cmd"
exit /b %EXITCODE%
