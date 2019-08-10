#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

mkdir ~/Scalar.FT

$Scalar_PUBLISHDIR/Scalar.FunctionalTests --full-suite $2
