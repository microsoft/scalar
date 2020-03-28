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

# Default installation flags
INSTALL_WATCHMAN=1

# Check the presence of known installation flags
for arg in "$@"
do
    if [ "$arg" = "--no-watchman" ]; then
        INSTALL_WATCHMAN=0
    fi
done

# Check if any of the installation options require Homebrew
REQUIRE_HOMEBREW=0
if [ $INSTALL_WATCHMAN -eq 1 ]; then
    REQUIRE_HOMEBREW=1
fi

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

echo ""
echo "=============================="
echo "Checking prerequisites..."

if [ $REQUIRE_HOMEBREW -eq 1 ]; then
    ## Check for brew installation
    BREW_INSTALLED=0

    if which -s brew; then
        BREW_INSTALLED=1
    else
        BREW_INSTALLED=0
    fi

    if [ $BREW_INSTALLED -eq 0 ]; then
        echo ""
        echo "Homebrew is required to install watchman. Please install Homebrew with the following command and run the installation script again:"
        echo "/usr/bin/ruby -e \"\$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install)\""
        exit 1
    else
        echo "Homebrew already installed!"
    fi
fi


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

# Install Watchman
if [ $INSTALL_WATCHMAN -eq 1 ]; then
    echo ""
    echo "=============================="
    echo "Installing watchman as: $CURRENT_USER"

    sudo chown -R $CURRENT_USER /usr/local/Cellar
    sudo -u $CURRENT_USER brew update
    sudo -u $CURRENT_USER brew unlink python@2 || echo "Python 2 was not installed"
    sudo -u $CURRENT_USER brew install watchman
else
    echo ""
    echo "=============================="
    echo "Skipping watchman installation"
    echo "(--no-watchman was specified)"
fi

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
