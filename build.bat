@echo off
setlocal
cd /d "%~dp0"
dotnet script test\build.csx %*
exit /b %ERRORLEVEL%
