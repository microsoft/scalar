@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")

SETLOCAL
SET PATH=C:\Program Files\Scalar;C:\Program Files\Git\cmd;%PATH%

if not "%2"=="--test-scalar-on-path" goto :startFunctionalTests

REM Force Scalar.FunctionalTests.exe to use the installed version of Scalar
del %Scalar_OUTPUTDIR%\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\Scalar.exe
del %Scalar_OUTPUTDIR%\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\Scalar.ReadObjectHook.exe
del %Scalar_OUTPUTDIR%\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\Scalar.Mount.exe
del %Scalar_OUTPUTDIR%\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\Scalar.Service.exe
del %Scalar_OUTPUTDIR%\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\Scalar.Service.UI.exe

REM Same for Scalar.FunctionalTests.Windows.exe
del %Scalar_OUTPUTDIR%\Scalar.FunctionalTests.Windows\bin\x64\%Configuration%\Scalar.exe
del %Scalar_OUTPUTDIR%\Scalar.FunctionalTests.Windows\bin\x64\%Configuration%\Scalar.ReadObjectHook.exe
del %Scalar_OUTPUTDIR%\Scalar.FunctionalTests.Windows\bin\x64\%Configuration%\Scalar.Mount.exe
del %Scalar_OUTPUTDIR%\Scalar.FunctionalTests.Windows\bin\x64\%Configuration%\Scalar.Service.exe
del %Scalar_OUTPUTDIR%\Scalar.FunctionalTests.Windows\bin\x64\%Configuration%\Scalar.Service.UI.exe

echo PATH = %PATH%
echo scalar location:
where scalar
echo Scalar.Service location:
where Scalar.Service
echo git location:
where git

:startFunctionalTests
dotnet %Scalar_OUTPUTDIR%\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\Scalar.FunctionalTests.dll /result:TestResultNetCore.xml %2 %3 %4 %5 || goto :endFunctionalTests

:endFunctionalTests
set error=%errorlevel%

call %Scalar_SCRIPTSDIR%\StopAllServices.bat

exit /b %error%
