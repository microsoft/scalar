#!/bin/sh
# ---------------------------------------------------------
# InstallScalar.sh
#
# Description: Main logic for installing Scalar and supporting
#              Components.
# ---------------------------------------------------------

set -e

GIT_INSTALLER_PKG="##GIT_INSTALLER_PKG_PLACEHOLDER##"
GCM_CORE_INSTALLER_PKG="##GCM_CORE_INSTALLER_PKG_PLACEHOLDER##"
SCALAR_INSTALLER_PKG="##SCALAR_INSTALLER_PKG_PLACEHOLDER##"

SCALAR_DISTRIBUTION_ROOT="$(dirname ${BASH_SOURCE[0]})"

echo ""
echo "Welcome - running Scalar installation script"

CURRENT_USER=$(/usr/bin/logname)

if [ "$CURRENT_USER" = "root" ]; then
	CURRENT_USER=$(/usr/bin/whoami)
fi

if [ -z GIT_INSTALLER_PKG ]; then
    echo "ERROR: GIT_INSTALLER_PKG environment variable not set - exiting"
    exit 1;
fi

if [ -z GCM_CORE_INSTALLER_PKG ]; then
    echo "ERROR: GCM_CORE_INSTALLER_PKG environment variable not set - exiting"
    exit 1;
fi

if [ -z SCALAR_INSTALLER_PKG ]; then
    echo "ERROR: SCALAR_INSTALLER_PKG environment variable not set - exiting"
    exit 1;
fi

echo "Scalar distribution root: $SCALAR_DISTRIBUTION_ROOT"
echo "Git installer pkg: $GIT_INSTALLER_PKG"
echo "GCM installer pkg: $GCM_CORE_INSTALLER_PKG"
echo "Scalar installer pkg: $SCALAR_INSTALLER_PKG"

# Install Git
echo ""
echo "=============================="
echo "Installing Git for Mac for Scalar"
sudo /usr/sbin/installer -pkg "$SCALAR_DISTRIBUTION_ROOT/Git/$GIT_INSTALLER_PKG" -target /

# Install GCM Core
echo ""
echo "=============================="
echo "Installing GCM Core"
sudo /usr/sbin/installer -pkg "$SCALAR_DISTRIBUTION_ROOT/GCM/$GCM_CORE_INSTALLER_PKG" -target /

# Install Scalar
echo ""
echo "=============================="
echo "Installing Scalar"
sudo /usr/sbin/installer -pkg "$SCALAR_DISTRIBUTION_ROOT/Scalar/$SCALAR_INSTALLER_PKG" -target /

# Install optional package if specified
if [ ! -z "$OPTIONAL_INSTALLER_PKG" ]; then
    echo ""
    echo "=============================="
    echo "Installing Optional Install Package"
    sudo /usr/sbin/installer -pkg "$SCALAR_DISTRIBUTION_ROOT/Optional/$OPTIONAL_INSTALLER_PKG" -target /
fi

# Run the post install script (if any)
if [ -f "$SCALAR_DISTRIBUTION_ROOT/Scripts/PostInstall.sh" ]; then
    echo ""
    echo "=============================="
    echo "Running post install script"
    sudo /bin/bash "$SCALAR_DISTRIBUTION_ROOT/Scripts/PostInstall.sh"
fi

# Installation Complete!
echo ""
echo "=============================="
echo "Installation Complete!!!"
