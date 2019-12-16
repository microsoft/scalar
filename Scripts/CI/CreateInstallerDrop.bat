@ECHO OFF
CALL %~dp0\..\InitializeEnvironment.bat || EXIT /B 10

IF "%1"=="" GOTO USAGE
IF "%2"=="" GOTO USAGE

SETLOCAL enableextensions
SET Configuration=%1
SET SCALAR_STAGEDIR=%2

REM Prepare the staging directories for installer drop.
IF EXIST %SCALAR_STAGEDIR% (
  rmdir /S /Q %SCALAR_STAGEDIR%
)

mkdir %SCALAR_STAGEDIR%

xcopy %SCALAR_OUTPUTDIR%\Scalar.Installer.Windows\dist\%Configuration%\* %SCALAR_STAGEDIR% /S /Y

GOTO END

:USAGE
echo "ERROR: Usage: CreateInstallerDrop.bat [configuration] [build drop root directory]"
EXIT /B 1

:END
EXIT 0
