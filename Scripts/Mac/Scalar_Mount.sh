#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

$SCALAR_OUTPUTDIR/Scalar/bin/$CONFIGURATION/netcoreapp3.0/osx-x64/publish/scalar mount ~/ScalarTest
