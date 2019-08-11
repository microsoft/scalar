@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")

set RESULT=0

%Scalar_OUTPUTDIR%\Scalar.UnitTests.Windows\bin\x64\%Configuration%\Scalar.UnitTests.Windows.exe  || set RESULT=1
dotnet %Scalar_OUTPUTDIR%\Scalar.UnitTests\bin\x64\%Configuration%\netcoreapp2.1\Scalar.UnitTests.dll  || set RESULT=1

exit /b %RESULT%
