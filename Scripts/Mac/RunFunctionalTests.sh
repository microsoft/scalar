#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

# consume $1 so the next "$@" is all arguments after it
shift

mkdir ~/Scalar.FT

$Scalar_PUBLISHDIR/Scalar.FunctionalTests --full-suite "$@"
