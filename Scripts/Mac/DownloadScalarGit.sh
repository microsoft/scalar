. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

BUILDDIR=$Scalar_OUTPUTDIR/Scalar.Build
GITVERSION="$($Scalar_SCRIPTDIR/GetGitVersionNumber.sh)"
cp $Scalar_SRCDIR/nuget.config $BUILDDIR
dotnet new classlib -n Restore.GitInstaller -o $BUILDDIR --force
dotnet add $BUILDDIR/Restore.GitInstaller.csproj package --package-directory $Scalar_PACKAGESDIR GitForMac.Scalar.Installer --version $GITVERSION
