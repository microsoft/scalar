using Scalar.Common;
using Scalar.Common.Git;

namespace Scalar.Platform.POSIX
{
    public class POSIXGitInstallation : IGitInstallation
    {
        public string GetInstalledGitBinPath()
        {
            ProcessResult result = ProcessHelper.Run("which", args: "git", redirectOutput: true);
            if (result.ExitCode != 0)
            {
                return null;
            }

            return result.Output.Trim();
        }
    }
}
