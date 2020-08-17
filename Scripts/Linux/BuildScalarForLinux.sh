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

ARCH=$(uname -m)
if test "$ARCH" != "x86_64"; then
  >&2 echo "architecture must be x86_64 for struct stat; stopping"
  exit 1
fi

CC=${CC:-cc}

echo 'main(){int i=1;const char *n="n";struct stat b;i=__xstat64(i,n,&b);}' | \
  cc -xc -include sys/stat.h -o /dev/null - 2>/dev/null

if test $? != 0; then
  >&2 echo "__xstat64() not found in libc ABI; stopping"
  exit 1
fi

# If we're building the Profiling(Release) configuration, remove Profiling() for building .NET code
if [ "$CONFIGURATION" == "Profiling(Release)" ]; then
  CONFIGURATION=Release
fi

dotnet publish $SCALAR_SRCDIR/Scalar.sln --runtime linux-x64 -p:ScalarVersion=$VERSION --configuration $CONFIGURATION || exit 1
