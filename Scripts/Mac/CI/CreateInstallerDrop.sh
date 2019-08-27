#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/../InitializeEnvironment.sh"

CONFIGURATION=$1
BUILDDROP_ROOT=$2
if [ -z $BUILDDROP_ROOT ] || [ -z $CONFIGURATION ]; then
  echo 'ERROR: Usage: CreateInstallerDrop.sh [configuration] [build drop root directory]'
  exit 1
fi

# Set up the installer directory structure
rm -rf $BUILDDROP_ROOT
mkdir -p $BUILDDROP_ROOT

# Generate Scalar Distribution
$Scalar_SCRIPTDIR/CreateScalarDistribution.sh $CONFIGURATION

# Copy to the build drop, retaining directory structure.
cp -Rf $Scalar_OUTPUTDIR/Scalar.Distribution $BUILDDROP_ROOT
