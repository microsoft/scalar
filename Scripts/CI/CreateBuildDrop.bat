@ECHO OFF
CALL %~dp0\..\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" GOTO USAGE
IF "%2"=="" GOTO USAGE

SETLOCAL enableextensions
SET Configuration=%1
SET VFS_STAGEDIR=%2

REM Prepare the staging directories for functional tests.
IF EXIST %VFS_STAGEDIR% (
  rmdir /s /q %VFS_STAGEDIR%
)

mkdir %VFS_STAGEDIR%\src\Scripts
mkdir %VFS_STAGEDIR%\BuildOutput\GVFS.Build
mkdir %VFS_STAGEDIR%\BuildOutput\GVFS.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1

REM Make a minimal 'test' enlistment to pass along our pipeline.
copy %VFS_SCRIPTSDIR%\*.* %VFS_STAGEDIR%\src\Scripts\ || exit /b 1
copy %VFS_OUTPUTDIR%\GVFS.Build\*.* %VFS_STAGEDIR%\BuildOutput\GVFS.Build || exit /b 1
dotnet publish %VFS_SRCDIR%\GVFS\GVFS.FunctionalTests\GVFS.FunctionalTests.csproj -p:StyleCopEnabled=False --self-contained --framework netcoreapp2.1 -r win-x64 -c Release -o %VFS_STAGEDIR%\BuildOutput\GVFS.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\ || exit /b 1
robocopy %VFS_OUTPUTDIR%\GVFS.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\ %VFS_STAGEDIR%\BuildOutput\GVFS.FunctionalTests\bin\x64\%Configuration%\netcoreapp2.1\ /E /XC /XN /XO
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

