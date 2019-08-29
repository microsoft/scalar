#!/bin/bash
# ---------------------------------------------------------
# CreateScalarDistribution.sh
#
# Description: Gathers components required for a complete Scalar installation
#              and organizes them with a script to install Scalar and supporting
#              components.
# ---------------------------------------------------------

set -e

## To enable extra output when running the script, uncomment the following set commands
# set -x
# set -v

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

# Gather different parameters
SCALAR_SRC_DIR=$Scalar_SRCDIR
SCALAR_ENLISTMENT_DIR=$Scalar_ENLISTMENTDIR
SCALAR_PACKAGES_DIR="$SCALAR_ENLISTMENT_DIR/packages"
SCALAR_SCRIPT_DIR=$Scalar_SCRIPTDIR
BUILD_OUTPUT_DIR="$Scalar_OUTPUTDIR"
DISTRIBUTION_DIR="$BUILD_OUTPUT_DIR/Scalar.Distribution"

if [ -z $SCALAR_ENLISTMENT_DIR ]; then
    echo "Scalar enlistment directory not set - exiting."
    exit 1;
fi

if [ -z $SCALAR_SRC_DIR ]; then
    echo "Scalar source directory not set - exiting."
    exit 1;
fi

CONFIGURATION=$1
CONFIGURATION=${CONFIGURATION:-"Debug"}

function GetScalarVersion()
{
    SCALAR_PROPS=$SCALAR_SRC_DIR/Scalar.Build/Scalar.props
    SCALAR_VERSION="$(cat $SCALAR_PROPS | grep ScalarVersion | grep -Eo '[0-9.]+(-\w+)*')"
}

# Git Version, Git Installer Package
GIT_VERSION="$($SCALAR_SCRIPT_DIR/GetGitVersionNumber.sh)"
GIT_INSTALLER_PKG_SRC_PATH="$(find $SCALAR_PACKAGES_DIR/gitformac.gvfs.installer/$GIT_VERSION -type f -name *.pkg)" || exit 1
GIT_INSTALLER_PKG="${GIT_INSTALLER_PKG_SRC_PATH##*/}"

# GCM Version
GCM_VERSION=""
GCM_INSTALLER_PKG="gcmcore-osx-2.0.33.21076.pkg"
GCM_INSTALLER_DOWNLOAD_URL="https://github.com/microsoft/Git-Credential-Manager-Core/releases/download/v2.0.33-beta/$GCM_INSTALLER_PKG"

# Scalar Version
GetScalarVersion
SCALAR_INSTALLER_PKG="Scalar.$SCALAR_VERSION.pkg"
SCALAR_INSTALLER_PKG_SRC_PATH="$BUILD_OUTPUT_DIR/Scalar.Installer.Mac/bin/x64/$CONFIGURATION/netcoreapp2.1/osx-x64/$SCALAR_INSTALLER_PKG"

# Clean target folder
rm -Rf "$DISTRIBUTION_DIR"

# Create target folder
mkdir "$DISTRIBUTION_DIR"
mkdir "$DISTRIBUTION_DIR/GCM"
mkdir "$DISTRIBUTION_DIR/Git"
mkdir "$DISTRIBUTION_DIR/Scalar"

# Download GCM Core
curl -L -o "$DISTRIBUTION_DIR/GCM/$GCM_INSTALLER_PKG" "$GCM_INSTALLER_DOWNLOAD_URL"

# Copy Git installer package
cp -Rf "$GIT_INSTALLER_PKG_SRC_PATH" "$DISTRIBUTION_DIR/Git/$GIT_INSTALLER_PKG"

# Copy Scalar
cp -Rf "$SCALAR_INSTALLER_PKG_SRC_PATH" "$DISTRIBUTION_DIR/Scalar/$SCALAR_INSTALLER_PKG"

# Write out Scalar Installation Script

/usr/bin/sed -e "s|##GIT_INSTALLER_PKG_PLACEHOLDER##|$GIT_INSTALLER_PKG|g" "$SCALAR_SCRIPT_DIR/InstallScalarTemplate.sh" > "$DISTRIBUTION_DIR/InstallScalar.sh"
/usr/bin/sed -i.bak "s|##GCM_CORE_INSTALLER_PKG_PLACEHOLDER##|$GCM_INSTALLER_PKG|g" "$DISTRIBUTION_DIR/InstallScalar.sh"
/usr/bin/sed -i.bak "s|##SCALAR_INSTALLER_PKG_PLACEHOLDER##|$SCALAR_INSTALLER_PKG|g" "$DISTRIBUTION_DIR/InstallScalar.sh"
chmod +x "$DISTRIBUTION_DIR/InstallScalar.sh"
