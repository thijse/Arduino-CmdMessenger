@echo off
setlocal
cd /d "%~dp0"
dotnet test test\CSharp\CommandMessenger.Tests\CommandMessenger.Tests.csproj %*
exit /b %ERRORLEVEL%
