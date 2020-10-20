@ECHO OFF

IF "%1"=="" (
	ECHO "Missing layout directory argument"
	EXIT /B 1
) ELSE (SET "LAYOUTDIR=%1")

IF "%2"=="" (
	ECHO "Missing signing root directory argument"
	EXIT /B 1
) ELSE (SET "SIGNDIR=%2")

echo Copying files to sign...

rmdir /s /q "%SIGNDIR%" > nul 2>&1
mkdir "%SIGNDIR%\pe"
mkdir "%SIGNDIR%\macho"
xcopy "%LAYOUTDIR%\usr\local\scalar\scalar.dll"                   "%SIGNDIR%\pe"    /k/h/y
xcopy "%LAYOUTDIR%\usr\local\scalar\scalar.common.dll"            "%SIGNDIR%\pe"    /k/h/y
xcopy "%LAYOUTDIR%\usr\local\scalar\scalar.service.dll"           "%SIGNDIR%\pe"    /k/h/y
xcopy "%LAYOUTDIR%\usr\local\scalar\scalar"                       "%SIGNDIR%\macho" /k/h/y
xcopy "%LAYOUTDIR%\usr\local\scalar\scalar.service"               "%SIGNDIR%\macho" /k/h/y
xcopy "%LAYOUTDIR%\Library\Application Support\Scalar\Scalar.app" "%SIGNDIR%\macho\Scalar.app\" /s/h/e/k/f/c/y
