@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

taskkill /F /T /FI "IMAGENAME eq git.exe"
taskkill /F /T /FI "IMAGENAME eq Scalar.exe"
taskkill /F /T /FI "IMAGENAME eq Scalar.Mount.exe"

if not exist "c:\Program Files\Scalar" goto :end

call %Scalar_SCRIPTSDIR%\StopAllServices.bat

REM Find the latest uninstaller file by date and run it. Goto the next step after a single execution.
for /F "delims=" %%f in ('dir "c:\Program Files\Scalar\unins*.exe" /B /S /O:-D') do %%f /VERYSILENT /SUPPRESSMSGBOXES /NORESTART & goto :deleteScalar

:deleteScalar
rmdir /q/s "c:\Program Files\Scalar"

REM Delete ProgramData\Scalar directory (logs, downloaded upgrades, repo-registry, scalar.config). It can affect the behavior of a future Scalar install.
if exist "C:\ProgramData\Scalar" rmdir /q/s "C:\ProgramData\Scalar"

:end
