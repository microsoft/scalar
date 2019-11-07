#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/../InitializeEnvironment.sh"

CONFIGURATION=$1
SCALAR_STAGEDIR=$2
if [ -z $SCALAR_STAGEDIR ] || [ -z $CONFIGURATION ]; then
  echo 'ERROR: Usage: CreateBuildDrop.sh [configuration] [build drop root directory]'
  exit 1
fi

# Set up some paths
SCRIPTS_SRC=$SCALAR_SCRIPTSDIR
TESTS_SRC=$SCALAR_OUTPUTDIR/Scalar.FunctionalTests/bin/$CONFIGURATION/netcoreapp3.0/osx-x64/publish

SCRIPTS_DEST=$SCALAR_STAGEDIR/src/Scripts/Mac
TESTS_DEST=$SCALAR_STAGEDIR/out/Scalar.FunctionalTests/bin/$CONFIGURATION/netcoreapp3.0/osx-x64/publish

# Set up the build drop directory structure
rm -rf $SCALAR_STAGEDIR
mkdir -p $SCRIPTS_DEST
mkdir -p $TESTS_DEST

# Copy to the build drop, retaining directory structure
cp -Rf $SCRIPTS_SRC/ $SCRIPTS_DEST
cp -Rf $TESTS_SRC/ $TESTS_DEST
