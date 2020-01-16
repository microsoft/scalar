@ECHO OFF
SETLOCAL
setlocal enabledelayedexpansion
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")
IF "%2"=="" (SET "ScalarVersion=0.2.173.2") ELSE (SET "ScalarVersion=%2")

dotnet publish %SCALAR_SRCDIR%\Scalar.sln -p:ScalarVersion=%ScalarVersion% --configuration %Configuration% --runtime win-x64 -v:n || exit /b 1

ENDLOCAL
