SCRIPTDIR="$(dirname ${BASH_SOURCE[0]})"

# convert to an absolute path because it is required by `dotnet publish`
pushd $SCRIPTDIR &>/dev/null
export Scalar_SCRIPTDIR="$(pwd)"
popd &>/dev/null

export Scalar_SRCDIR=$Scalar_SCRIPTDIR/../..

Scalar_ENLISTMENTDIR=$Scalar_SRCDIR/..
export Scalar_OUTPUTDIR=$Scalar_ENLISTMENTDIR/BuildOutput
export Scalar_PUBLISHDIR=$Scalar_ENLISTMENTDIR/Publish
export Scalar_PACKAGESDIR=$Scalar_ENLISTMENTDIR/packages
