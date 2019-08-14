@ECHO OFF
CALL %~dp0\..\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" GOTO USAGE
IF "%2"=="" GOTO USAGE

SETLOCAL enableextensions
SET Configuration=%1
SET Scalar_STAGEDIR=%2

REM Prepare the staging directories for installer drop.
IF EXIST %Scalar_STAGEDIR% (
  rmdir /s /q %Scalar_STAGEDIR%
)

mkdir %Scalar_STAGEDIR%

copy %Scalar_OUTPUTDIR%\Scalar.Build\*.exe %Scalar_STAGEDIR%

GOTO END

:USAGE
echo "ERROR: Usage: CreateInstallerDrop.bat [configuration] [build drop root directory]"
exit /b 1

:END
exit 0
