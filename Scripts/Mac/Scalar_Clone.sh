#!/bin/bash
. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

REPOURL=$1

CONFIGURATION=$2
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

$Scalar_PUBLISHDIR/scalar clone $REPOURL ~/ScalarTest --local-cache-path ~/ScalarTest/.scalarCache
