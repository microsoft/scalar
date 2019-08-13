@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

call %Scalar_SCRIPTSDIR%\StopService.bat Scalar.Service
call %Scalar_SCRIPTSDIR%\StopService.bat Test.Scalar.Service
