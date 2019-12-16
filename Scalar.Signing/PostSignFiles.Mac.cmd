@ECHO OFF

IF "%1"=="" (
	ECHO "Missing signing root directory argument"
	EXIT /B 1
) ELSE (SET "SIGNDIR=%1")

IF "%2"=="" (
	ECHO "Missing layout directory argument"
	EXIT /B 1
) ELSE (SET "LAYOUTDIR=%2")

echo Copying signed files...

xcopy "%SIGNDIR%\pe\scalar.dll"          "%LAYOUTDIR%\usr\local\scalar\" /k/h/y
xcopy "%SIGNDIR%\pe\scalar.common.dll"   "%LAYOUTDIR%\usr\local\scalar\" /k/h/y
xcopy "%SIGNDIR%\pe\scalar.service.dll"  "%LAYOUTDIR%\usr\local\scalar\" /k/h/y
xcopy "%SIGNDIR%\pe\scalar.upgrader.dll" "%LAYOUTDIR%\usr\local\scalar\" /k/h/y
xcopy "%SIGNDIR%\macho\scalar"           "%LAYOUTDIR%\usr\local\scalar\" /k/h/y
xcopy "%SIGNDIR%\macho\scalar.service"   "%LAYOUTDIR%\usr\local\scalar\" /k/h/y
xcopy "%SIGNDIR%\macho\scalar.upgrader"  "%LAYOUTDIR%\usr\local\scalar\" /k/h/y
xcopy "%SIGNDIR%\macho\Scalar.app"      "%LAYOUTDIR%\Library\Application Support\Scalar\Scalar.app\" /s/h/e/k/f/c/y
