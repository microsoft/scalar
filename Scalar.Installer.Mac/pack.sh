#!/bin/bash

PACKAGE_VERSION=$1
if [ -z $PACKAGE_VERSION ]; then
  echo "Error: Installer package version not specified"
  exit 1
fi

SRC_DIR=$2
if [ -z $SRC_DIR ]; then
  echo "Error: Source directory not specified"
  exit 1
fi

LAYOUT_DIR=$3
if [ -z $LAYOUT_DIR ]; then
  echo "Error: Layout directory not specified"
  exit 1
fi

OUT_DIR=$4
if [ -z $OUT_DIR ]; then
  echo "Error: Output directory not specified"
  exit 1
fi

INSTALLER_PACKAGE_NAME="Scalar.$PACKAGE_VERSION"
INSTALLER_PACKAGE_ID="com.scalar.pkg"

LAYOUT_ROOT_DIR="${LAYOUT_DIR}/root"
LAYOUT_FLATPKG_DIR="${LAYOUT_DIR}/pkg"

INSTALLER_SRC_DIR="${SRC_DIR}/Scalar.Installer.Mac"
SCRIPTS_DIR="${INSTALLER_SRC_DIR}/scripts"
COMPONENTSPLIST_PATH="${INSTALLER_SRC_DIR}/scalar_components.plist"
DIST_TEMPLATE_PATH="${INSTALLER_SRC_DIR}/distribution.template.xml"

function CheckLayoutExists()
{
    if [ ! -d "$LAYOUT_ROOT_DIR" ] ; then
        echo "Error: Could not find layout root to package at ${LAYOUT_ROOT_DIR}."
        exit 1
    fi
}

function SetPermissions()
{
    chmodCommand="chmod -R 755 \"${LAYOUT_ROOT_DIR}\""
    eval $chmodCommand || exit 1
}

function CreateScalarInstaller()
{
    mkdirOutDir="mkdir -p \"${OUT_DIR}\" \"${LAYOUT_FLATPKG_DIR}\""
    eval $mkdirOutDir || exit 1

    pkgBuildCommand="/usr/bin/pkgbuild --identifier $INSTALLER_PACKAGE_ID --component-plist \"${COMPONENTSPLIST_PATH}\" --scripts \"${SCRIPTS_DIR}\" --root \"${LAYOUT_ROOT_DIR}\" \"${LAYOUT_FLATPKG_DIR}/$INSTALLER_PACKAGE_NAME.pkg\""
    echo $pkgBuildCommand
    eval $pkgBuildCommand || exit 1
}

function CreateScalarDistribution()
{
    if [ ! -f "$DIST_TEMPLATE_PATH" ] ; then
        echo "Error: Could not find package distribution XML template at ${DIST_TEMPLATE_PATH}."
        exit 1
    fi

    # Create the distribution file from the template
    SCALAR_PKG_VERSION=$PACKAGE_VERSION
    SCALAR_PKG_NAME="$INSTALLER_PACKAGE_NAME.pkg"

    DIST_FINAL_PATH="${LAYOUT_FLATPKG_DIR}/distribution.xml"

    /usr/bin/sed -e "s|SCALAR_VERSION_PLACHOLDER|$SCALAR_PKG_VERSION|g" \
                 -e "s|SCALAR_PKG_NAME_PLACEHOLDER|$SCALAR_PKG_NAME|g" \
                 "${DIST_TEMPLATE_PATH}" > "${DIST_FINAL_PATH}" || exit 1

    buildScalarDistCmd="/usr/bin/productbuild --distribution \"${DIST_FINAL_PATH}\" --package-path \"${LAYOUT_FLATPKG_DIR}\" \"${OUT_DIR}/$INSTALLER_PACKAGE_NAME.pkg\""
    echo $buildScalarDistCmd
    eval $buildScalarDistCmd || exit 1
}

function Run()
{
    CheckLayoutExists
    SetPermissions
    CreateScalarInstaller
    CreateScalarDistribution
}

Run
