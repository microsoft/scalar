@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10
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

IF EXIST %SCALAR_OUTPUTDIR% (
    ECHO deleting build outputs
    rmdir /s /q %Scalar_OUTPUTDIR%
) ELSE (
    ECHO no build outputs found
)

call %SCALAR_SCRIPTSDIR%\StopAllServices.bat
