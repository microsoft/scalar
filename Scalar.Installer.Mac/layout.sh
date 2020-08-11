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

BIN_DIR=$5
if [ -z $BIN_DIR ]; then
  echo "Error: Binaries directory not specified"
  exit 1
fi

OUT_DIR=$6
if [ -z $OUT_DIR ]; then
  echo "Error: Output directory not specified"
  exit 1
fi

LOCALBIN_DIR="${OUT_DIR}/usr/local/bin"
SCALAR_DESTINATION="${OUT_DIR}/usr/local/scalar"
AGENTPLIST_DESTINATION="${OUT_DIR}/Library/LaunchAgents"
LIBRARYAPPSUPPORT_DESTINATION="${OUT_DIR}/Library/Application Support/Scalar"

function CreateLayoutDirectories()
{
    # Ensure the layout directory is clean so we don't accidentally package any old files
    cleanLayout="rm -rf \"${OUT_DIR}\""
    eval $cleanLayout || exit 1

    mkdirLayout="mkdir -p \"${LOCALBIN_DIR}\" \"${SCALAR_DESTINATION}\" \"${AGENTPLIST_DESTINATION}\" \"${LIBRARYAPPSUPPORT_DESTINATION}\""
    eval $mkdirLayout || exit 1
}

function CopyScalar()
{
    # The trailing slash on the path here is important to ensure the *contents*
    # of the directory are copied, and NOT the parent directory itself.
    PUBPATH_FRAGMENT="bin/${CONFIGURATION}/${TARGETFRAMEWORK}/${RUNTIMEIDENTIFIER}/publish/"

    copyCmd="cp -Rf \"${BIN_DIR}/Scalar/${PUBPATH_FRAGMENT}\" \"${SCALAR_DESTINATION}\"" || exit 1
    eval $copyCmd || exit 1

    copyCmd="cp -Rf \"${BIN_DIR}/Scalar.Service/${PUBPATH_FRAGMENT}\" \"${SCALAR_DESTINATION}\"" || exit 1
    eval $copyCmd || exit 1

    copyCmd="cp -Rf \"${BIN_DIR}/Scalar.Upgrader/${PUBPATH_FRAGMENT}\" \"${SCALAR_DESTINATION}\"" || exit 1
    eval $copyCmd || exit 1

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

function Run()
{
    CreateLayoutDirectories
    CopyScalar
    CopyUninstaller
}

Run
