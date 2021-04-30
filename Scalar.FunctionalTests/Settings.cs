using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Scalar.FunctionalTests.Properties
{
    public static class Settings
    {
        public enum ValidateWorkingTreeMode
        {
            None = 0,
            Full = 1,
            SparseMode = 2,
        }

        public enum MaintenanceMode
        {
            Scalar = 0,
            Git = 1,
        }

        public static class Default
        {
            public static string CurrentDirectory { get; private set; }

            public static string RepoToClone { get; set; }
            public static string PathToBash { get; set; }
            public static string PathToScalar { get; set; }
            public static string Commitish { get; set; }
            public static string CommitId { get; set; }
            public static string ControlGitRepoRoot { get; set; }
            public static string EnlistmentRoot { get; set; }
            public static string PathToGitRoot { get; set; }
            public static string BinaryFileNameExtension { get; set; }

            public static void Initialize()
            {
                CurrentDirectory = Path.GetFullPath(Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]));

                RepoToClone = @"https://gvfs.visualstudio.com/ci/_git/ForTests";
                Commitish = @"FunctionalTests/20180214";
                CommitId = "2797fbb8358bb2e0c12d6f3b42a60b43f7655edf";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    EnlistmentRoot = @"C:\Repos\ScalarFunctionalTests\enlistment";
                    PathToScalar = @"Scalar.exe";
                    PathToGitRoot = @"C:\Program Files\Git\cmd";
                    PathToBash = @"C:\Program Files\Git\bin\bash.exe";

                    ControlGitRepoRoot = @"C:\Repos\ScalarFunctionalTests\ControlRepo";
                    BinaryFileNameExtension = ".exe";
                }
                else
                {
                    string root = Path.Combine(
                        Environment.GetEnvironmentVariable("HOME"),
                        "Scalar.FT");
                    EnlistmentRoot = Path.Combine(root, "test");
                    ControlGitRepoRoot = Path.Combine(root, "control");
                    PathToScalar = "scalar";
                    PathToGitRoot = "/usr/local/bin";
                    PathToBash = "/bin/bash";
                    BinaryFileNameExtension = string.Empty;
                }
            }
        }
    }
}
