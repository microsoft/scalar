@ECHO OFF
SETLOCAL
setlocal enabledelayedexpansion
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")
IF "%2"=="" (SET "ScalarVersion=0.2.173.2") ELSE (SET "ScalarVersion=%2")

REM Restore using the NuGet CLI rather than the dotnet CLI to workaround a bug
REM in the version of NuGet included with the dotnet CLI (https://github.com/NuGet/Home/issues/8692)
nuget restore %SCALAR_SRCDIR%\Scalar.sln || exit /b 1
dotnet publish --no-restore %SCALAR_SRCDIR%\Scalar.sln -p:ScalarVersion=%ScalarVersion% --configuration %Configuration% --runtime win-x64 || exit /b 1

ENDLOCAL
