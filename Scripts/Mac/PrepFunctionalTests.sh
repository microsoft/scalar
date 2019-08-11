#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

# Install Scalar-aware Git (that was published by the build script)
GITPUBLISH=$Scalar_OUTPUTDIR/Git
if [[ ! -d $GITPUBLISH ]]; then
    echo "Scalar-aware Git package not found. Run BuildScalarForMac.sh and try again"
    exit 1
fi
hdiutil attach $GITPUBLISH/*.dmg || exit 1
GITPKG="$(find /Volumes/Git* -type f -name *.pkg)" || exit 1
sudo installer -pkg "$GITPKG" -target / || exit 1
hdiutil detach /Volumes/Git*
