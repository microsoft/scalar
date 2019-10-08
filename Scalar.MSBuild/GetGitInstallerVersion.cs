using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.Text.RegularExpressions;

namespace Scalar.MSBuild
{
    public class GetGitInstallerVersion : Task
    {
        [Required]
        public string PackagePath { get; set; }

        [Required]
        public string InstallerExtension { get; set; }

        [Output]
        public string GitVersion { get; set; }

        public override bool Execute()
        {
            this.Log.LogMessage(MessageImportance.Normal, "Getting Git version from installer in package {0}...", this.PackagePath);

            string toolsPath = Path.Combine(this.PackagePath, "tools");

            if (!Directory.Exists(toolsPath))
            {
                this.Log.LogError("Could not find tools directory in NuGet package '{0}'.", toolsPath);
                return false;
            }

            string[] installerFiles = Directory.GetFiles(toolsPath, $"*{this.InstallerExtension}", SearchOption.TopDirectoryOnly);
            if (installerFiles.Length == 0)
            {
                this.Log.LogError( "Failed to find installer in package with file extension '{0}'.", this.InstallerExtension);
                return false;
            }

            if (installerFiles.Length > 1)
            {
                this.Log.LogWarning("Found multiple file with extension '{0}'. Using first found '{1}'.", this.InstallerExtension, installerFiles[0]);
            }

            string installerFileName = Path.GetFileName(installerFiles[0]);
            this.Log.LogMessage(MessageImportance.Low,  "Extracting Git version from file '{0}'", installerFileName);

            var match = Regex.Match(installerFileName, @"(\d+\.\d+\.\d+\.[A-Z]+\.\d+\.\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                this.Log.LogError( "Failed to extract Git version from file name '{0}'...", installerFileName);
                return false;
            }

            this.GitVersion = match.Groups[0].Value;
            this.Log.LogMessage(MessageImportance.Low,  "Extracted Git version '{0}'", this.GitVersion);

            return true;
        }
    }
}
