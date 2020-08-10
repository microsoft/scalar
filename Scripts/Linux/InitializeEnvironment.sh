SCRIPTDIR="$(dirname ${BASH_SOURCE[0]})"

# convert to an absolute path because it is required by `dotnet publish`
pushd $SCRIPTDIR &>/dev/null
export SCALAR_SCRIPTSDIR="$(pwd)"
popd &>/dev/null

export SCALAR_SRCDIR=$SCALAR_SCRIPTSDIR/../..

export SCALAR_ENLISTMENTDIR=$SCALAR_SRCDIR/..
export SCALAR_OUTPUTDIR=$SCALAR_ENLISTMENTDIR/out
