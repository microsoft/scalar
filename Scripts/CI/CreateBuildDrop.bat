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

REM Make a minimal 'test' enlistment to pass along our pipeline.
copy %Scalar_SCRIPTSDIR%\*.* %Scalar_STAGEDIR%\src\Scripts\ || exit /b 1
copy %Scalar_OUTPUTDIR%\Scalar.Build\*.* %Scalar_STAGEDIR%\BuildOutput\Scalar.Build
dotnet publish %Scalar_SRCDIR%\Scalar.FunctionalTests\Scalar.FunctionalTests.csproj -p:StyleCopEnabled=False --self-contained --framework netcoreapp2.1 -r win-x64 -c Release -o %Scalar_STAGEDIR%\BuildOutput\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\ || exit /b 1
robocopy %Scalar_OUTPUTDIR%\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\ %Scalar_STAGEDIR%\BuildOutput\Scalar.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\ /E /XC /XN /XO
IF %ERRORLEVEL% GTR 7 (
  echo "ERROR: robocopy had at least one failure"
  exit /b 1
)
GOTO END

:USAGE
echo "ERROR: Usage: CreateBuildDrop.bat [configuration] [build drop root directory]"
exit /b 1

:END
exit 0
