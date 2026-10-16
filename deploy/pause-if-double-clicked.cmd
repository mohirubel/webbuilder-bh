@echo off
rem Keeps the window open when a script was started by double-clicking it in Explorer,
rem so its output can be read. In a terminal (PowerShell, cmd, VS Code) it does nothing.
setlocal
set LAUNCHER=
for /f "usebackq delims=" %%P in (`powershell -NoProfile -Command "$cmd = Get-CimInstance Win32_Process -Filter ('ProcessId=' + (Get-CimInstance Win32_Process -Filter ('ProcessId=' + $PID)).ParentProcessId); (Get-Process -Id $cmd.ParentProcessId -ErrorAction SilentlyContinue).ProcessName"`) do set LAUNCHER=%%P
if /i "%LAUNCHER%"=="explorer" pause
