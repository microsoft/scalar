@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")

SETLOCAL
SET PATH=C:\Program Files\Scalar;C:\Program Files\watchman;C:\Program Files\Git\cmd;%PATH%

SET publishFragment=bin\%Configuration%\netcoreapp3.0\win10-x64\publish
SET functionalTestsDir=%SCALAR_OUTPUTDIR%\Scalar.FunctionalTests\%publishFragment%

IF "%2"=="--test-scalar-on-path" GOTO :testPath

:testBuilt
ECHO *******************************
ECHO Testing built version of Scalar
ECHO *******************************
REM Copy most recently build Scalar binaries
SET copyOptions=/s /njh /njs /nfl /ndl
robocopy %SCALAR_OUTPUTDIR%\Scalar\%publishFragment% %functionalTestsDir% %copyOptions%
robocopy %SCALAR_OUTPUTDIR%\Scalar.Service\%publishFragment% %functionalTestsDir% %copyOptions%
robocopy %SCALAR_OUTPUTDIR%\Scalar.Service.UI\%publishFragment% %functionalTestsDir% %copyOptions%
robocopy %SCALAR_OUTPUTDIR%\Scalar.Upgrader\%publishFragment% %functionalTestsDir% %copyOptions%
GOTO :startTests

:testPath
ECHO **************************
ECHO Testing Scalar on the PATH
ECHO **************************
ECHO PATH:
ECHO %PATH%
ECHO Scalar location:
where scalar
ECHO Scalar.Service location:
where scalar.service
ECHO Git location:
where git

:startTests
%functionalTestsDir%\Scalar.FunctionalTests /result:TestResultNetCore.xml %2 %3 %4 %5 %6 %7 %8 || goto :endTests

:endTests
SET error=%errorlevel%
CALL %SCALAR_SCRIPTSDIR%\StopAllServices.bat

EXIT /b %error%
