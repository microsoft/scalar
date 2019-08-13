@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

SETLOCAL

IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")
 
SET SolutionConfiguration=%Configuration%.Windows

SET nuget="%Scalar_TOOLSDIR%\nuget.exe"
IF NOT EXIST %nuget% (
  mkdir %nuget%\..
  powershell -ExecutionPolicy Bypass -Command "Invoke-WebRequest 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile %nuget%"
)

%nuget% restore %Scalar_SRCDIR%\Scalar.sln || exit /b 1

dotnet restore %Scalar_SRCDIR%\Scalar.sln /p:Configuration=%SolutionConfiguration% /p:VCTargetsPath="C:\Program Files (x86)\MSBuild\Microsoft.Cpp\v4.0\V140" --packages %Scalar_PACKAGESDIR% || exit /b 1

ENDLOCAL
