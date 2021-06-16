#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

PUBLISH_FRAGMENT="bin/$CONFIGURATION/netcoreapp3.1/osx-x64/publish"
FUNCTIONAL_TESTS_DIR="$SCALAR_OUTPUTDIR/Scalar.FunctionalTests/$PUBLISH_FRAGMENT"

# Always test Scalar on the PATH, because it
# was installed with Git
echo "PATH:"
echo "$PATH"
echo "Scalar location:"
where scalar

TESTS_EXEC="$FUNCTIONAL_TESTS_DIR/Scalar.FunctionalTests"

mkdir ~/Scalar.FT

# Consume the first argument
shift

# Ensure the binary is executable
chmod +x $TESTS_EXEC

# Run the tests!
$TESTS_EXEC "$@"
