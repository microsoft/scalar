#!/bin/bash

# To test on a Debian-based Linux machine:
#
# ./InstallFromSource.sh
# (outputs Git, GCM, and Scalar versions)
#
# Small clone should work:
# scalar clone https://dev.azure.com/gvfs/ci/_git/ForTests
#
# Big clone for authenticated repo should work:
# scalar clone <url>
#
# Note: if you are using a headless Linux machine, then GCM Core will
# not be able to store your credentials. Instead, create a PAT and
# use that for password (multiple times). This will be fixed soon.

# Nuke previous data
rm -rf git-vfs*.deb gcmcore-linux*.deb packages*.deb

echo "Installing dotnet SDK 3.1"

sudo apt-get update -qq
sudo apt-get install -y apt-transport-https && sudo apt-get update -qq

sudo apt-get install -y wget
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb

if sudo apt-get install -y dotnet-sdk-3.1
then
	echo "dotnet successfully installed"
else
	echo "error: failed to install dotnet"
	echo "please follow instructions here to install: https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu"
	exit 1
fi

echo "Installing Git from microsoft/git"
wget https://github.com/microsoft/git/releases/download/v2.28.0.vfs.0.0/git-vfs_2.28.0.vfs.0.0.deb 

if ! sudo dpkg -i ./git-vfs_2.28.0.vfs.0.0.deb
then
	echo "error: failed to install microsoft/git"
	exit 1
fi

echo "Installing GCM Core"
wget https://github.com/microsoft/Git-Credential-Manager-Core/releases/download/v2.0.246-beta/gcmcore-linux_amd64.2.0.246.34937.deb

if ! sudo dpkg -i ./gcmcore-linux*.deb
then
	echo "error: failed to install GCM core"
	exit 1
fi

echo "Cloning and installing Scalar"
mkdir scalar
git clone --filter=blob:none https://github.com/microsoft/scalar scalar/src
(
	cd scalar/src
	dotnet restore
	dotnet build --configuration Release --no-restore
	dotnet test --configuration Release --no-restore --verbosity normal
	sudo rm -rf /usr/local/lib/scalar
	sudo mkdir /usr/local/lib/scalar
	sudo cp -r ../out/Scalar/bin/Release/netcoreapp3.1/* /usr/local/lib/scalar/
	sudo rm -rf /usr/local/bin/scalar
	sudo ln -s /usr/local/lib/scalar/scalar /usr/local/bin/scalar
	sudo chmod a+x /usr/local/bin/scalar
)

git version
git-credential-manager-core --version


if ! scalar version
then
	echo "Scalar was not properly installed. Please double-check the build output"
	exit 1
fi
