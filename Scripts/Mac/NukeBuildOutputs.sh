#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

sudo rm -Rf $SCALAR_OUTPUTDIR

echo git --work-tree=$SCALAR_SRCDIR clean -Xdf -n
git --work-tree=$SCALAR_SRCDIR clean -Xdf -n
