#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

VERSION=$2
if [ -z $VERSION ]; then
  VERSION="0.2.173.2"
fi

# If we're building the Profiling(Release) configuration, remove Profiling() for building .NET code
if [ "$CONFIGURATION" == "Profiling(Release)" ]; then
  CONFIGURATION=Release
fi

dotnet publish $SCALAR_SRCDIR/Scalar.sln --runtime osx-x64 -p:ScalarVersion=$VERSION --configuration $CONFIGURATION || exit 1
