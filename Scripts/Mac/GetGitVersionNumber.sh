. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

ScalarPROPS=$Scalar_SRCDIR/Scalar.Build/Scalar.props
GITVERSION="$(cat $ScalarPROPS | grep GitPackageVersion | grep -Eo '[0-9.]+(-\w+)*')"
echo $GITVERSION
