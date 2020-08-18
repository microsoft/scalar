#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

TESTRESULTSDIR=$2
if [ -z $TESTRESULTSDIR ]; then
  TESTRESULTSDIR=$SCALAR_OUTPUTDIR/TestResults
fi

dotnet test $SCALAR_SRCDIR/Scalar.sln --configuration $CONFIGURATION --logger trx --results-directory $TESTRESULTSDIR || exit 1
