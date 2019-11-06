. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

if [ -z $1 ]; then
  echo "Version Number not defined for CommonAssemblyVersion.cs"
fi

# Update the version number in Scalar.props for other consumers of ScalarVersion
sed -i "" -E "s@<ScalarVersion>[0-9]+(\.[0-9]+)*</ScalarVersion>@<ScalarVersion>$1</ScalarVersion>@g" $Scalar_SRCDIR/Scalar.Build/Scalar.props

# Then generate CommonAssemblyVersion.cs
cat >$Scalar_OUTPUTDIR/CommonAssemblyVersion.cs <<TEMPLATE
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyVersion("$1")]
[assembly: AssemblyFileVersion("$1")]
[assembly: InternalsVisibleTo("Scalar.UnitTests")]
TEMPLATE
