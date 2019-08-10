#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

sudo rm -Rf $Scalar_OUTPUTDIR
rm -Rf $Scalar_PACKAGESDIR
rm -Rf $Scalar_PUBLISHDIR

echo git --work-tree=$Scalar_SRCDIR clean -Xdf -n
git --work-tree=$Scalar_SRCDIR clean -Xdf -n
