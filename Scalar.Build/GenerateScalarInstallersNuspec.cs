using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;

namespace Scalar.PreBuild
{
    public class GenerateScalarInstallersNuspec : Task
    {
        [Required]
        public string ScalarSetupPath { get; set; }

        [Required]
        public string GitPackageVersion { get; set; }

        [Required]
        public string PackagesPath { get; set; }

        [Required]
        public string OutputFile { get; set; }

        public override bool Execute()
        {
            this.Log.LogMessage(MessageImportance.High, "Generating Scalar.Installers.nuspec");

            this.ScalarSetupPath = Path.GetFullPath(this.ScalarSetupPath);
            this.PackagesPath = Path.GetFullPath(this.PackagesPath);

            File.WriteAllText(
                this.OutputFile,
                string.Format(
@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>Scalar.Installers</id>
    <version>$ScalarVersion$</version>
    <authors>Microsoft</authors>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Scalar and G4W installers</description>
  </metadata>
  <files>
    <file src=""{0}"" target=""Scalar"" />
    <file src=""{1}\GitForWindows.GVFS.Installer.{2}\tools\*"" target=""G4W"" />
    <file src=""{1}\GitForWindows.GVFS.Portable.{2}\tools\*"" target=""G4W"" />
  </files>
</package>",
                    this.ScalarSetupPath,
                    this.PackagesPath,
                    this.GitPackageVersion));

            return true;
        }
    }
}
