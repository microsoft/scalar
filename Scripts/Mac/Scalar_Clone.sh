#!/bin/bash
. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

REPOURL=$1

CONFIGURATION=$2
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

$SCALAR_OUTPUTDIR/Scalar/bin/$CONFIGURATION/netcoreapp3.0/osx-x64/publish/scalar clone $REPOURL ~/ScalarTest --local-cache-path ~/ScalarTest/.scalarCache
