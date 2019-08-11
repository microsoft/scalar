@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

taskkill /f /im Scalar.Mount.exe 2>&1
verify >nul

powershell -NonInteractive -NoProfile -Command "& { (Get-MpPreference).ExclusionPath | ? {$_.StartsWith('C:\Repos\')} | %%{Remove-MpPreference -ExclusionPath $_} }"

IF EXIST C:\Repos\ScalarFunctionalTests\enlistment (
    rmdir /s /q C:\Repos\ScalarFunctionalTests\enlistment
) ELSE (
    ECHO no test enlistment found
)

IF EXIST C:\Repos\ScalarPerfTest (
    rmdir /s /q C:\Repos\ScalarPerfTest
) ELSE (
    ECHO no perf test enlistment found
)

IF EXIST %Scalar_OUTPUTDIR% (
    ECHO deleting build outputs
    rmdir /s /q %Scalar_OUTPUTDIR%
) ELSE (
    ECHO no build outputs found
)

IF EXIST %Scalar_PUBLISHDIR% (
    ECHO deleting published output
    rmdir /s /q %Scalar_PUBLISHDIR%
) ELSE (
    ECHO no packages found
)

IF EXIST %Scalar_PACKAGESDIR% (
    ECHO deleting packages
    rmdir /s /q %Scalar_PACKAGESDIR%
) ELSE (
    ECHO no packages found
)

call %Scalar_SCRIPTSDIR%\StopAllServices.bat
