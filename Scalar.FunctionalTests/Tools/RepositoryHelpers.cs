using Scalar.FunctionalTests.FileSystemRunners;
using System.Runtime.InteropServices;

namespace Scalar.FunctionalTests.Tools
{
    public static class RepositoryHelpers
    {
        public static void DeleteTestDirectory(string repoPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use cmd.exe to delete the enlistment as it properly handles tombstones and reparse points
                CmdRunner.DeleteDirectoryWithLimitedRetries(repoPath);
            }
            else
            {
                BashRunner.DeleteDirectoryWithLimitedRetries(repoPath);
            }
        }
    }
}
