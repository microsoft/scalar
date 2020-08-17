#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

TESTS_EXEC=$SCALAR_OUTPUTDIR/Scalar.FunctionalTests/bin/$CONFIGURATION/netcoreapp3.1/linux-x64/publish/Scalar.FunctionalTests

mkdir ~/Scalar.FT

# Consume the first argument
shift

# Ensure the binary is executable
chmod +x $TESTS_EXEC

# Run the tests!
$TESTS_EXEC "$@"
