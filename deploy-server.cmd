@echo off
rem ------------------------------------------------------------------
rem  Build and deploy Web Builders to https://webbuilder.byteheart.com
rem  Asks for the FTP password (or uses %WB_FTP_PASSWORD%).
rem    deploy-server              build + upload
rem    deploy-server -BuildOnly   build into artifacts\publish only
rem  Details: docs/server-deployment.md
rem ------------------------------------------------------------------
setlocal
set PS=powershell
where pwsh >nul 2>&1 && set PS=pwsh

%PS% -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\deploy.ps1" %*
set EXITCODE=%errorlevel%

call "%~dp0deploy\pause-if-double-clicked.cmd"
exit /b %EXITCODE%
