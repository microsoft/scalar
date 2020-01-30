@ECHO OFF
CALL %~dp0\..\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" GOTO USAGE
IF "%2"=="" GOTO USAGE

SETLOCAL enableextensions
SET Configuration=%1
SET SCALAR_STAGEDIR=%2

REM Prepare the staging directories for functional tests.
IF EXIST %SCALAR_STAGEDIR% (
  rmdir /s /q %SCALAR_STAGEDIR%
)

SET scriptsSrc=%SCALAR_SCRIPTSDIR%\*
SET testsSrc=%SCALAR_OUTPUTDIR%\Scalar.FunctionalTests\bin\%Configuration%\netcoreapp3.0\win10-x64\publish

SET scriptsDest=%SCALAR_STAGEDIR%\src\Scripts
SET testsDest=%SCALAR_STAGEDIR%\out\Scalar.FunctionalTests\bin\%Configuration%\netcoreapp3.0\win10-x64\publish

mkdir %scriptsDest%
mkdir %testsDest%

REM Make a minimal 'test' enlistment to pass along our pipeline.
xcopy %scriptsSrc% %scriptsDest% /S /Y || EXIT /B 1
xcopy %testsSrc% %testsDest% /S /Y || EXIT /B 1
GOTO END

:USAGE
echo "ERROR: Usage: CreateFTDrop.bat [configuration] [build drop root directory]"
EXIT /b 1

:END
EXIT 0
