@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

if /i "%1"=="--integration" (
    echo Running integration tests [requires connected Arduino]...
    dotnet test extras\CSharp\Tests\CommandMessenger.IntegrationTests\CommandMessenger.IntegrationTests.csproj %2 %3 %4 %5 %6 %7 %8 %9
    exit /b !ERRORLEVEL!
)

if /i "%1"=="--all" (
    echo Running unit tests...
    dotnet test extras\CSharp\Tests\CommandMessenger.Tests\CommandMessenger.Tests.csproj
    if !ERRORLEVEL! neq 0 exit /b !ERRORLEVEL!
    echo.
    echo Running integration tests [requires connected Arduino]...
    dotnet test extras\CSharp\Tests\CommandMessenger.IntegrationTests\CommandMessenger.IntegrationTests.csproj
    exit /b !ERRORLEVEL!
)

dotnet test extras\CSharp\Tests\CommandMessenger.Tests\CommandMessenger.Tests.csproj %*
exit /b %ERRORLEVEL%
