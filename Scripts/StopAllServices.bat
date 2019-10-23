@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

call %SCALAR_SCRIPTSDIR%\StopService.bat Scalar.Service
call %SCALAR_SCRIPTSDIR%\StopService.bat Test.Scalar.Service
