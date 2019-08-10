@ECHO OFF
CALL %~dp0\..\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" GOTO USAGE
IF "%2"=="" GOTO USAGE

SETLOCAL enableextensions
SET Configuration=%1
SET Scalar_STAGEDIR=%2

REM Prepare the staging directories for functional tests.
IF EXIST %Scalar_STAGEDIR% (
  rmdir /s /q %Scalar_STAGEDIR%
)
mkdir %Scalar_STAGEDIR%\src\Scripts
mkdir %Scalar_STAGEDIR%\BuildOutput\Scalar.Build
mkdir %Scalar_STAGEDIR%\BuildOutput\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1
mkdir %Scalar_STAGEDIR%\BuildOutput\Scalar.FunctionalTests.Windows\bin\x64\%Configuration%\

REM Make a minimal 'test' enlistment to pass along our pipeline.
copy %Scalar_SRCDIR%\init.cmd %Scalar_STAGEDIR%\src\
copy %Scalar_SCRIPTSDIR%\*.* %Scalar_STAGEDIR%\src\Scripts\
copy %Scalar_OUTPUTDIR%\Scalar.Build\*.* %Scalar_STAGEDIR%\BuildOutput\Scalar.Build
dotnet publish %Scalar_SRCDIR%\Scalar\Scalar.FunctionalTests\Scalar.FunctionalTests.csproj -p:StyleCopEnabled=False --self-contained --framework netcoreapp2.1 -r win-x64 -c Release -o %Scalar_STAGEDIR%\BuildOutput\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\
robocopy %Scalar_OUTPUTDIR%\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\ %Scalar_STAGEDIR%\BuildOutput\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\ /E /XC /XN /XO
copy %Scalar_OUTPUTDIR%\Scalar.FunctionalTests.Windows\bin\x64\%Configuration%\*.* %Scalar_STAGEDIR%\BuildOutput\Scalar.FunctionalTests.Windows\bin\x64\%Configuration%\
GOTO END

:USAGE
echo "ERROR: Usage: CreateBuildDrop.bat [configuration] [build drop root directory]"
exit 1

:END
