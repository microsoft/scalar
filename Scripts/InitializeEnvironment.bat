@ECHO OFF

REM Set environment variables for interesting paths that scripts might need access to.
PUSHD %~dp0
SET Scalar_SCRIPTSDIR=%CD%
POPD

CALL :RESOLVEPATH "%Scalar_SCRIPTSDIR%\.."
SET Scalar_SRCDIR=%_PARSED_PATH_%

CALL :RESOLVEPATH "%Scalar_SRCDIR%\.."
SET Scalar_ENLISTMENTDIR=%_PARSED_PATH_%

SET Scalar_OUTPUTDIR=%Scalar_ENLISTMENTDIR%\BuildOutput
SET Scalar_PACKAGESDIR=%Scalar_ENLISTMENTDIR%\packages
SET Scalar_PUBLISHDIR=%Scalar_ENLISTMENTDIR%\Publish
SET Scalar_TOOLSDIR=%Scalar_ENLISTMENTDIR%\.tools

REM Clean up
SET _PARSED_PATH_=

GOTO :EOF

:RESOLVEPATH
SET "_PARSED_PATH_=%~f1"
GOTO :EOF
