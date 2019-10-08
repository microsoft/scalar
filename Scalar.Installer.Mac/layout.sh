#!/bin/bash

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  echo "Error: Build configuration not specified"
  exit 1
fi

TARGETFRAMEWORK=$2
if [ -z $TARGETFRAMEWORK ]; then
  echo "Error: Target framework not specified"
  exit 1
fi

RUNTIMEIDENTIFIER=$3
if [ -z $RUNTIMEIDENTIFIER ]; then
  echo "Error: Runtime identifier not specified"
  exit 1
fi

SRC_DIR=$4
if [ -z $SRC_DIR ]; then
  echo "Error: Source directory not specified"
  exit 1
fi

PUBLISH_DIR=$5
if [ -z $PUBLISH_DIR ]; then
  echo "Error: Publish directory not specified"
  exit 1
fi

LAYOUT_DIR=$6
if [ -z $LAYOUT_DIR ]; then
  echo "Error: Layout directory not specified"
  exit 1
fi

LAYOUT_ROOT_DIR="${LAYOUT_DIR}/root"
LOCALBIN_DIR="${LAYOUT_ROOT_DIR}/usr/local/bin"
SCALAR_DESTINATION="${LAYOUT_ROOT_DIR}/usr/local/scalar"
AGENTPLIST_DESTINATION="${LAYOUT_ROOT_DIR}/Library/LaunchAgents"
LIBRARYAPPSUPPORT_DESTINATION="${LAYOUT_ROOT_DIR}/Library/Application Support/Scalar"

function CreateLayoutDirectories()
{
    # Ensure the layout directory is clean so we don't accidentally package any old files
    cleanLayout="rm -rf \"${LAYOUT_DIR}\""
    eval $cleanLayout || exit 1

    mkdirLayout="mkdir -p \"${LOCALBIN_DIR}\" \"${SCALAR_DESTINATION}\" \"${AGENTPLIST_DESTINATION}\" \"${LIBRARYAPPSUPPORT_DESTINATION}\""
    eval $mkdirLayout || exit 1
}

function CopyScalar()
{
    SCALAR_PUBPATH="${PUBLISH_DIR}/${CONFIGURATION}/${TARGETFRAMEWORK}/${RUNTIMEIDENTIFIER}"

    if [ ! -d "$SCALAR_PUBPATH" ] ; then
        echo "Error: Could not find published .NET applications at ${SCALAR_PUBPATH}."
        exit 1
    fi

    # The trailing slash on the source path here is important to ensure the *contents*
    # of the directory are copied, and NOT the parent directory itself.
    copyScalar="cp -Rf \"${SCALAR_PUBPATH}/\" \"${SCALAR_DESTINATION}\"" || exit 1
    eval $copyScalar || exit 1

    # Create the symlink
    pushd $LOCALBIN_DIR
    linkCommand="ln -sf ../scalar/scalar scalar"
    eval $linkCommand
    popd
}

function CopyUninstaller()
{
    UNINSTALL_SCRIPT="${SRC_DIR}/Scalar.Installer.Mac/uninstall_scalar.sh"

    if [ ! -f "$UNINSTALL_SCRIPT" ] ; then
        echo "Error: Could not find uninstall script at ${UNINSTALL_SCRIPT}."
        exit 1
    fi

    copyUninstaller="cp -f \"${UNINSTALL_SCRIPT}\" \"${SCALAR_DESTINATION}/.\""
    eval $copyUninstaller || exit 1
}

function CopyNotificationApp()
{
    SCALAR_NOTIFICATION_APP="${PUBLISH_DIR}/${CONFIGURATION}/native/${RUNTIMEIDENTIFIER}/Scalar.app"

    if [ ! -d "$SCALAR_NOTIFICATION_APP" ] ; then
        echo "Error: Could not find native notification app at ${SCALAR_NOTIFICATION_APP}."
        exit 1
    fi

    copyNotificationApp="cp -Rf \"${SCALAR_NOTIFICATION_APP}\" \"${LIBRARYAPPSUPPORT_DESTINATION}\""
    eval $copyNotificationApp || exit 1
}

function CopyAgentPlists()
{
    NOTIFICATION_PLIST_PATH="${SRC_DIR}/Scalar.Notifications.Mac/org.scalar.usernotification.plist"
    SERVICE_PLIST_PATH="${SRC_DIR}/Scalar.Service/Mac/org.scalar.service.plist"

    if [ ! -f "$NOTIFICATION_PLIST_PATH" ] ; then
        echo "Error: Could not find notification app agent plist at ${NOTIFICATION_PLIST_PATH}."
        exit 1
    fi

    if [ ! -f "$SERVICE_PLIST_PATH" ] ; then
        echo "Error: Could not find service agent plist at ${SERVICE_PLIST_PATH}."
        exit 1
    fi

    copyNotificationPlist="cp -Rf \"${NOTIFICATION_PLIST_PATH}\" \"${AGENTPLIST_DESTINATION}/.\""
    eval $copyNotificationPlist || exit 1

    copyServicePlist="cp -Rf \"${SERVICE_PLIST_PATH}\" \"${AGENTPLIST_DESTINATION}/.\""
    eval $copyServicePlist || exit 1
}

function Run()
{
    CreateLayoutDirectories
    CopyScalar
    CopyUninstaller
    CopyNotificationApp
    CopyAgentPlists
}

Run
