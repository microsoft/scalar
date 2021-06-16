@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")

SETLOCAL
SET PATH=C:\Program Files\Scalar;C:\Program Files\Git\cmd;%PATH%

SET publishFragment=bin\%Configuration%\netcoreapp3.1\win10-x64\publish
SET functionalTestsDir=%SCALAR_OUTPUTDIR%\Scalar.FunctionalTests\%publishFragment%

ECHO **************************
ECHO Testing Scalar on the PATH
ECHO **************************
ECHO PATH:
ECHO %PATH%
ECHO Scalar location:
where scalar
ECHO Git location:
where git

:startTests
%functionalTestsDir%\Scalar.FunctionalTests /result:TestResultNetCore.xml %2 %3 %4 %5 %6 %7 %8 || goto :endTests

:endTests
SET error=%errorlevel%

EXIT /b %error%
