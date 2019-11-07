@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")
IF "%2"=="" (SET "TestResultsDir=%SCALAR_OUTPUTDIR%\TestResults") ELSE (SET "TestResultsDir=%2" )

set RESULT=0

dotnet test %SCALAR_SRCDIR%\Scalar.sln --configuration %Configuration% --logger trx --results-directory %TestResultsDir% || set RESULT=1

exit /b %RESULT%
