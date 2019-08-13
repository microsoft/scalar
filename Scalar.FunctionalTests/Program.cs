using Scalar.FunctionalTests.Properties;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Scalar.FunctionalTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Properties.Settings.Default.Initialize();
            NUnitRunner runner = new NUnitRunner(args);

            if (runner.HasCustomArg("--no-shared-scalar-cache"))
            {
                Console.WriteLine("Running without a shared git object cache");
                ScalarTestConfig.NoSharedCache = true;
            }

            if (runner.HasCustomArg("--test-scalar-on-path"))
            {
                Console.WriteLine("Running tests against Scalar on path");
                ScalarTestConfig.TestScalarOnPath = true;
            }

            ScalarTestConfig.LocalCacheRoot = runner.GetCustomArgWithParam("--shared-scalar-cache-root");

            HashSet<string> includeCategories = new HashSet<string>();
            HashSet<string> excludeCategories = new HashSet<string>();

            if (runner.HasCustomArg("--full-suite"))
            {
                Console.WriteLine("Running the full suite of tests");

                List<object[]> modes = new List<object[]>();
                foreach (Settings.ValidateWorkingTreeMode mode in Enum.GetValues(typeof(Settings.ValidateWorkingTreeMode)))
                {
                    modes.Add(new object[] { mode });
                }

                ScalarTestConfig.GitRepoTestsValidateWorkTree = modes.ToArray();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ScalarTestConfig.FileSystemRunners = FileSystemRunners.FileSystemRunner.AllWindowsRunners;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    ScalarTestConfig.FileSystemRunners = FileSystemRunners.FileSystemRunner.AllMacRunners;
                }
            }
            else
            {
                Settings.ValidateWorkingTreeMode validateMode = Settings.ValidateWorkingTreeMode.Full;

                if (runner.HasCustomArg("--sparse-mode"))
                {
                    validateMode = Settings.ValidateWorkingTreeMode.SparseMode;

                    // Only test the git commands in sparse mode for splitting out tests in builds
                    includeCategories.Add(Categories.GitCommands);
                }

                ScalarTestConfig.GitRepoTestsValidateWorkTree =
                    new object[]
                    {
                        new object[] { validateMode },
                    };

                if (runner.HasCustomArg("--extra-only"))
                {
                    Console.WriteLine("Running only the tests marked as ExtraCoverage");
                    includeCategories.Add(Categories.ExtraCoverage);
                }
                else
                {
                    excludeCategories.Add(Categories.ExtraCoverage);
                }

                ScalarTestConfig.FileSystemRunners = FileSystemRunners.FileSystemRunner.DefaultRunners;
            }

            if (runner.HasCustomArg("--windows-only"))
            {
                includeCategories.Add(Categories.WindowsOnly);

                // RunTests unions all includeCategories.  Remove ExtraCoverage to
                // ensure that we only run tests flagged as WindowsOnly
                includeCategories.Remove(Categories.ExtraCoverage);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                excludeCategories.Add(Categories.MacTODO.NeedsNewFolderCreateNotification);
                excludeCategories.Add(Categories.MacTODO.NeedsScalarConfig);
                excludeCategories.Add(Categories.MacTODO.NeedsDehydrate);
                excludeCategories.Add(Categories.MacTODO.NeedsServiceVerb);
                excludeCategories.Add(Categories.MacTODO.NeedsStatusCache);
                excludeCategories.Add(Categories.MacTODO.TestNeedsToLockFile);
                excludeCategories.Add(Categories.WindowsOnly);
            }
            else
            {
                excludeCategories.Add(Categories.MacOnly);
            }

            // For now, run all of the tests not flagged as needing to be updated to work
            // with the non-virtualized solution
            includeCategories.Clear();
            excludeCategories.Clear();
            excludeCategories.Add(Categories.NeedsUpdatesForNonVirtualizedMode);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                excludeCategories.Add(Categories.MacOnly);
            }

            ScalarTestConfig.DotScalarRoot = ".scalar";

            ScalarTestConfig.RepoToClone =
                runner.GetCustomArgWithParam("--repo-to-clone")
                ?? Properties.Settings.Default.RepoToClone;

            RunBeforeAnyTests();
            Environment.ExitCode = runner.RunTests(includeCategories, excludeCategories);

            if (Debugger.IsAttached)
            {
                Console.WriteLine("Tests completed. Press Enter to exit.");
                Console.ReadLine();
            }
        }

        private static void RunBeforeAnyTests()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ScalarServiceProcess.InstallService();

                string statusCacheVersionTokenPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.Create),
                    "Scalar",
                    "Scalar.Service",
                    "EnableGitStatusCacheToken.dat");

                if (!File.Exists(statusCacheVersionTokenPath))
                {
                    File.WriteAllText(statusCacheVersionTokenPath, string.Empty);
                }
            }
        }
    }
}
