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
            runner.AddGlobalSetupIfNeeded("Scalar.FunctionalTests.GlobalSetup");

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

            string trace2Output = runner.GetCustomArgWithParam("--trace2-output");
            if (trace2Output != null)
            {
                Console.WriteLine($"Sending trace2 output to {trace2Output}");
                Environment.SetEnvironmentVariable("GIT_TRACE2_EVENT", trace2Output);
            }

            ScalarTestConfig.LocalCacheRoot = runner.GetCustomArgWithParam("--shared-scalar-cache-root");

            HashSet<string> includeCategories = new HashSet<string>();
            HashSet<string> excludeCategories = new HashSet<string>();

            // Run all GitRepoTests with sparse mode
            ScalarTestConfig.GitRepoTestsValidateWorkTree =
                new object[]
                {
                        new object[] { Settings.ValidateWorkingTreeMode.SparseMode },
                };

            if (runner.HasCustomArg("--full-suite"))
            {
                Console.WriteLine("Running the full suite of tests");

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
                excludeCategories.Add(Categories.MacTODO.NeedsScalarConfig);
                excludeCategories.Add(Categories.MacTODO.NeedsServiceVerb);
                excludeCategories.Add(Categories.MacTODO.TestNeedsToLockFile);
                excludeCategories.Add(Categories.WindowsOnly);
            }
            else
            {
                excludeCategories.Add(Categories.MacOnly);
            }

            // For now, run all of the tests not flagged as needing to be updated to work
            // with the non-virtualized solution
            excludeCategories.Add(Categories.NeedsUpdatesForNonVirtualizedMode);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                excludeCategories.Add(Categories.MacOnly);
            }

            ScalarTestConfig.RepoToClone =
                runner.GetCustomArgWithParam("--repo-to-clone")
                ?? Properties.Settings.Default.RepoToClone;

            RunBeforeAnyTests();
            Environment.ExitCode = runner.RunTests(includeCategories, excludeCategories);

            try
            {
                // Shutdown the watchman server now that the tests are complete.
                // Allows deleting the unwatched directories.
                ProcessHelper.Run("watchman", "shudown-server");
            }
            catch (Exception)
            {
                // This is non-critical, and watchman may not be installed.
            }

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
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolderOption.Create),
                    "Scalar",
                    "ProgramData",
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
