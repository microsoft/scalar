@ECHO OFF
REM ---------------------------------------------------------
REM InstallScalar.bat
REM
REM Description: Main logic for installing Scalar and supporting
REM              Components.
REM ---------------------------------------------------------

SET SCALAR_DISTRIBUTION_ROOT=%~dp0
SET GIT_INSTALLER_EXE=##GIT_INSTALLER_EXE_PLACEHOLDER##
SET SCALAR_INSTALLER_EXE=##SCALAR_INSTALLER_EXE_PLACEHOLDER##

ECHO Scalar distribution root: %SCALAR_DISTRIBUTION_ROOT%
ECHO Git installer exe: %GIT_INSTALLER_EXE%
ECHO Scalar installer exe: %SCALAR_INSTALLER_EXE%

REM Install Git
ECHO.
ECHO ==============================
ECHO Installing Git for Windows for Scalar
%SCALAR_DISTRIBUTION_ROOT%\Git\%GIT_INSTALLER_EXE% /DIR="C:\Program Files\Git" /NOICONS /COMPONENTS="ext,ext\shellhere,ext\guihere,assoc,assoc_sh" /GROUP="Git" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART || EXIT /B 1

REM Install Scalar
ECHO.
ECHO ==============================
ECHO Installing Scalar
%SCALAR_DISTRIBUTION_ROOT%\Scalar\%SCALAR_INSTALLER_EXE% /VERYSILENT /SUPPRESSMSGBOXES /NORESTART || EXIT /B 1

REM Run the post install script (if any)
IF EXIST "%SCALAR_DISTRIBUTION_ROOT%\Scripts\PostInstall.bat" (
    ECHO.
    ECHO ==============================
    ECHO Running post install script
    START "%SCALAR_DISTRIBUTION_ROOT%\Scripts\PostInstall.bat"
)

REM Installation Complete!
ECHO.
ECHO ==============================
ECHO Installation Complete!!!
