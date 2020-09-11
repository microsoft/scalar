using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Scalar.FunctionalTests
{
    public static class ScalarTestConfig
    {
        public static string RepoToClone { get; set; }

        public static bool NoSharedCache { get; set; }

        public static string LocalCacheRoot { get; set; }

        public static string DefaultLocalCacheRoot {
            get
            {
                string homeDirectory = null;
                string cachePath = ".scalarCache";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    homeDirectory = Path.GetPathRoot(Properties.Settings.Default.EnlistmentRoot);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    homeDirectory = Environment.GetEnvironmentVariable("HOME");
                }
                else
                {
                    // On Linux we use a local cache path per the XDG Base Directory Specification.
                    homeDirectory = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
                    if (!string.IsNullOrEmpty(homeDirectory))
                    {
                        cachePath = "scalar";
                    }
                    else
                    {
                        homeDirectory = Environment.GetEnvironmentVariable("HOME");
                        cachePath = Path.Combine(".cache", "scalar");
                    }

                }

                return Path.Combine(homeDirectory, cachePath);
            }
        }

        public static object[] FileSystemRunners { get; set; }

        public static object[] GitRepoTestsValidateWorkTree { get; set; }

        public static bool TestGitOnPath { get; set; }

        public static string PathToGit
        {
            get
            {
                string gitBinaryFileName = "git" + Properties.Settings.Default.BinaryFileNameExtension;
                return
                    TestGitOnPath ?
                    gitBinaryFileName :
                    Path.Combine(Properties.Settings.Default.PathToGitRoot, gitBinaryFileName);
            }
        }

        public static bool TestScalarOnPath { get; set; }

        public static string PathToScalar
        {
            get
            {
                return
                    TestScalarOnPath ?
                    Properties.Settings.Default.PathToScalar :
                    Path.Combine(Properties.Settings.Default.CurrentDirectory, Properties.Settings.Default.PathToScalar);
            }
        }
    }
}
