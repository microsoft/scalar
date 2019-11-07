#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/../InitializeEnvironment.sh"

CONFIGURATION=$1
SCALAR_STAGEDIR=$2
if [ -z $SCALAR_STAGEDIR ] || [ -z $CONFIGURATION ]; then
  echo 'ERROR: Usage: CreateInstallerDrop.sh [configuration] [build drop root directory]'
  exit 1
fi

# Set up the installer directory structure
rm -rf $SCALAR_STAGEDIR
mkdir -p $SCALAR_STAGEDIR

# Copy to the build drop, retaining directory structure
cp -Rf $SCALAR_OUTPUTDIR/Scalar.Distribution.Mac/dist/$CONFIGURATION/ $SCALAR_STAGEDIR
