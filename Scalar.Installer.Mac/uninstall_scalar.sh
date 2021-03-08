#!/bin/bash

ScalarFORDIRECTORY="/usr/local/scalar"
LAUNCHDAEMONDIRECTORY="/Library/LaunchDaemons"
LAUNCHAGENTDIRECTORY="/Library/LaunchAgents"
LIBRARYAPPSUPPORTDIRECTORY="/Library/Application Support/Scalar"
ScalarCOMMANDPATH="/usr/local/bin/scalar"
UNINSTALLERCOMMANDPATH="/usr/local/bin/uninstall_scalar.sh"
INSTALLERPACKAGEID="com.scalar.pkg"

function UnInstallScalar()
{           
    if [ -f "${LAUNCHDAEMONDIRECTORY}/$LOGDAEMONLAUNCHDFILENAME" ]; then
        rmCmd="sudo /bin/rm -Rf ${LAUNCHDAEMONDIRECTORY}/$LOGDAEMONLAUNCHDFILENAME"
        echo "$rmCmd..."
        eval $rmCmd || { echo "Error: Could not delete ${LAUNCHDAEMONDIRECTORY}/$LOGDAEMONLAUNCHDFILENAME. Delete it manually."; exit 1; }
    fi

    # Unloading Service LaunchAgent for each user
    # There will be one loginwindow instance for each logged in user, 
    # get its uid (this will correspond to the logged in user's id.) 
    # Then use launchctl bootout gui/uid to unload the Service 
    # for each user.
    declare -a launchAgents=(
    "org.scalar.usernotification"
    "org.scalar.service"
    )
    for nextLaunchAgent in "${launchAgents[@]}"; do
        for uid in $(ps -Ac -o uid,command | grep -iw "loginwindow" | awk '{print $1}'); do
            isLoadedCmd="sudo launchctl kill SIGCONT gui/$uid/$nextLaunchAgent"
            echo "$isLoadedCmd"
            if $isLoadedCmd; then
                unloadCmd="launchctl bootout gui/$uid /Library/LaunchAgents/$nextLaunchAgent.plist"
                echo "Unloading Service: '$unloadCmd'..."
                eval $unloadCmd
                error=$?
                # Ignore error 113: Could not find specified service
                if [ $error -ne 113 ] && [ $error -ne 0 ]; then
                    exit 1
                fi
            fi
        done

        rmCmd="sudo /bin/rm -Rf ${LAUNCHAGENTDIRECTORY}/$nextLaunchAgent.plist"
        echo "$rmCmd..."
        eval $rmCmd || { echo "Error: Could not delete ${LAUNCHAGENTDIRECTORY}/$nextLaunchAgent.plist. Delete it manually."; exit 1; }
    done

    if [ -s "${ScalarCOMMANDPATH}" ]; then
        rmCmd="sudo /bin/rm -Rf ${ScalarCOMMANDPATH}"
        echo "$rmCmd..."
        eval $rmCmd || { echo "Error: Could not delete ${ScalarCOMMANDPATH}. Delete it manually."; exit 1; }
    fi

    if [ -d "${LIBRARYAPPSUPPORTDIRECTORY}" ]; then
        rmCmd="sudo /bin/rm -Rf \"${LIBRARYAPPSUPPORTDIRECTORY}\""
        echo "$rmCmd..."
        eval $rmCmd || { echo "Error: Could not delete ${LIBRARYAPPSUPPORTDIRECTORY}. Delete it manually."; exit 1; }
    fi

    if [ -d "${ScalarFORDIRECTORY}" ]; then
        rmCmd="sudo /bin/rm -Rf ${ScalarFORDIRECTORY}"
        echo "$rmCmd..."
        eval $rmCmd || { echo "Error: Could not delete ${ScalarFORDIRECTORY}. Delete it manually."; exit 1; }
    fi
}

function ForgetPackage()
{
    if [ -f "/usr/sbin/pkgutil" ]; then
        forgetCmd="sudo /usr/sbin/pkgutil --forget $INSTALLERPACKAGEID"
        echo "$forgetCmd..."
        eval $forgetCmd
    fi
}

function Run()
{
    UnInstallScalar
    ForgetPackage
    echo "Successfully uninstalled Scalar"
}

Run
