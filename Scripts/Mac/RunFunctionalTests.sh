#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

PUBLISH_FRAGMENT="bin/$CONFIGURATION/netcoreapp3.1/osx-x64/publish"
FUNCTIONAL_TESTS_DIR="$SCALAR_OUTPUTDIR/Scalar.FunctionalTests/$PUBLISH_FRAGMENT"

if [ "$2" = "--test-scalar-on-path" ]; then
  echo "PATH:"
  echo "$PATH"
  echo "Scalar location:"
  where scalar
else
  # Copy most recently build Scalar binaries
  rsync -r "$SCALAR_OUTPUTDIR/Scalar/$PUBLISH_FRAGMENT/" "$FUNCTIONAL_TESTS_DIR"
fi

TESTS_EXEC="$FUNCTIONAL_TESTS_DIR/Scalar.FunctionalTests"

mkdir ~/Scalar.FT

# Consume the first argument
shift

# Ensure the binary is executable
chmod +x $TESTS_EXEC

# Run the tests!
$TESTS_EXEC "$@"
