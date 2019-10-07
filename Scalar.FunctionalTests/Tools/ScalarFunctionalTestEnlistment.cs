using Newtonsoft.Json.Linq;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Should;
using Scalar.FunctionalTests.Tests;
using Scalar.Tests.Should;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Scalar.FunctionalTests.Tools
{
    public class ScalarFunctionalTestEnlistment
    {
        private const string LockHeldByGit = "Scalar Lock: Held by {0}";
        private const int SleepMSWaitingForStatusCheck = 100;
        private const int DefaultMaxWaitMSForStatusCheck = 5000;
        private static readonly string ZeroBackgroundOperations = "Background operations: 0" + Environment.NewLine;
        private readonly bool fullClone;

        private ScalarProcess scalarProcess;

        private ScalarFunctionalTestEnlistment(string pathToScalar, string enlistmentRoot, string repoUrl, string commitish, string localCacheRoot = null, bool fullClone = true)
        {
            this.EnlistmentRoot = enlistmentRoot;
            this.RepoUrl = repoUrl;
            this.Commitish = commitish;
            this.fullClone = fullClone;

            if (localCacheRoot == null)
            {
                if (ScalarTestConfig.NoSharedCache)
                {
                    // eg C:\Repos\ScalarFunctionalTests\enlistment\7942ca69d7454acbb45ea39ef5be1d15\.scalar\.scalarCache
                    localCacheRoot = GetRepoSpecificLocalCacheRoot(enlistmentRoot);
                }
                else
                {
                    // eg C:\Repos\ScalarFunctionalTests\.scalarCache
                    // Ensures the general cache is not cleaned up between test runs
                    localCacheRoot = Path.Combine(Properties.Settings.Default.EnlistmentRoot, "..", ".scalarCache");
                }
            }

            this.LocalCacheRoot = localCacheRoot;
            this.scalarProcess = new ScalarProcess(pathToScalar, this.EnlistmentRoot, this.LocalCacheRoot);
        }

        public string EnlistmentRoot
        {
            get; private set;
        }

        public string RepoUrl
        {
            get; private set;
        }

        public string LocalCacheRoot { get; }

        public string RepoRoot
        {
            get { return Path.Combine(this.EnlistmentRoot, "src"); }
        }

        public string DotScalarRoot
        {
            get { return Path.Combine(this.EnlistmentRoot, ScalarTestConfig.DotScalarRoot); }
        }

        public string ScalarLogsRoot
        {
            get { return Path.Combine(this.DotScalarRoot, "logs"); }
        }

        public string DiagnosticsRoot
        {
            get { return Path.Combine(this.DotScalarRoot, "diagnostics"); }
        }

        public string Commitish
        {
            get; private set;
        }

        public static ScalarFunctionalTestEnlistment CloneAndMountWithPerRepoCache(string pathToGvfs, bool skipPrefetch)
        {
            string enlistmentRoot = ScalarFunctionalTestEnlistment.GetUniqueEnlistmentRoot();
            string localCache = ScalarFunctionalTestEnlistment.GetRepoSpecificLocalCacheRoot(enlistmentRoot);
            return CloneAndMount(pathToGvfs, enlistmentRoot, null, localCacheRoot: localCache, skipPrefetch: skipPrefetch);
        }

        public static ScalarFunctionalTestEnlistment CloneAndMount(
            string pathToGvfs,
            string commitish = null,
            string localCacheRoot = null,
            bool skipPrefetch = false,
            bool fullClone = true)
        {
            string enlistmentRoot = ScalarFunctionalTestEnlistment.GetUniqueEnlistmentRoot();
            return CloneAndMount(pathToGvfs, enlistmentRoot, commitish, localCacheRoot, skipPrefetch, fullClone);
        }

        public static ScalarFunctionalTestEnlistment CloneAndMountEnlistmentWithSpacesInPath(string pathToGvfs, string commitish = null)
        {
            string enlistmentRoot = ScalarFunctionalTestEnlistment.GetUniqueEnlistmentRootWithSpaces();
            string localCache = ScalarFunctionalTestEnlistment.GetRepoSpecificLocalCacheRoot(enlistmentRoot);
            return CloneAndMount(pathToGvfs, enlistmentRoot, commitish, localCache);
        }

        public static string GetUniqueEnlistmentRoot()
        {
            return Path.Combine(Properties.Settings.Default.EnlistmentRoot, Guid.NewGuid().ToString("N").Substring(0, 20));
        }

        public static string GetUniqueEnlistmentRootWithSpaces()
        {
            return Path.Combine(Properties.Settings.Default.EnlistmentRoot, "test " + Guid.NewGuid().ToString("N").Substring(0, 15));
        }

        public string GetPackRoot(FileSystemRunner fileSystem)
        {
            return Path.Combine(ScalarHelpers.GetObjectsRootFromGitConfig(this.RepoRoot), "pack");
        }

        public void DeleteEnlistment()
        {
            TestResultsHelper.OutputScalarLogs(this);
            RepositoryHelpers.DeleteTestDirectory(this.EnlistmentRoot);
        }

        public void CloneAndMount(bool skipPrefetch)
        {
            this.scalarProcess.Clone(this.RepoUrl, this.Commitish, skipPrefetch, fullClone: this.fullClone);

            GitProcess.Invoke(this.RepoRoot, "checkout " + this.Commitish);
            GitProcess.Invoke(this.RepoRoot, "branch --unset-upstream");
            GitProcess.Invoke(this.RepoRoot, "config core.abbrev 40");
            GitProcess.Invoke(this.RepoRoot, "config user.name \"Functional Test User\"");
            GitProcess.Invoke(this.RepoRoot, "config user.email \"functional@test.com\"");

            // If this repository has a .gitignore file in the root directory, force it to be
            // hydrated. This is because if the GitStatusCache feature is enabled, it will run
            // a "git status" command asynchronously, which will hydrate the .gitignore file
            // as it reads the ignore rules. Hydrate this file here so that it is consistently
            // hydrated and there are no race conditions depending on when / if it is hydrated
            // as part of an asynchronous status scan to rebuild the GitStatusCache.
            string rootGitIgnorePath = Path.Combine(this.RepoRoot, ".gitignore");
            if (File.Exists(rootGitIgnorePath))
            {
                File.ReadAllBytes(rootGitIgnorePath);
            }
        }

        public void MountScalar()
        {
            this.scalarProcess.Mount();
        }

        public bool TryMountScalar()
        {
            string output;
            return this.TryMountScalar(out output);
        }

        public bool TryMountScalar(out string output)
        {
            return this.scalarProcess.TryMount(out output);
        }

        public string Prefetch(bool failOnError = true, string standardInput = null)
        {
            return this.scalarProcess.Prefetch(failOnError, standardInput);
        }

        public void Repair(bool confirm)
        {
            this.scalarProcess.Repair(confirm);
        }

        public string Diagnose()
        {
            return this.scalarProcess.Diagnose();
        }

        public string LooseObjectStep()
        {
            return this.scalarProcess.LooseObjectStep();
        }

        public string PackfileMaintenanceStep(long? batchSize = null)
        {
            return this.scalarProcess.PackfileMaintenanceStep(batchSize);
        }

        public string PostFetchStep()
        {
            return this.scalarProcess.PostFetchStep();
        }

        public string Status(string trace = null)
        {
            return this.scalarProcess.Status(trace);
        }

        public bool WaitForBackgroundOperations(int maxWaitMilliseconds = DefaultMaxWaitMSForStatusCheck)
        {
            return this.WaitForStatus(maxWaitMilliseconds, ZeroBackgroundOperations).ShouldBeTrue("Background operations failed to complete.");
        }

        public bool WaitForLock(string lockCommand, int maxWaitMilliseconds = DefaultMaxWaitMSForStatusCheck)
        {
            return this.WaitForStatus(maxWaitMilliseconds, string.Format(LockHeldByGit, lockCommand));
        }

        public void UnmountScalar()
        {
            this.scalarProcess.Unmount();
        }

        public string GetCacheServer()
        {
            return this.scalarProcess.CacheServer("--get");
        }

        public string SetCacheServer(string arg)
        {
            return this.scalarProcess.CacheServer("--set " + arg);
        }

        public void UnmountAndDeleteAll()
        {
            this.UnmountScalar();
            this.DeleteEnlistment();
        }

        public string GetSourcePath(string path)
        {
            // Replace '/' with Path.DirectorySeparatorChar to ensure that any
            // Git paths are converted to system paths
            return Path.Combine(this.RepoRoot, path.Replace('/', Path.DirectorySeparatorChar));
        }

        public string GetSourcePath(params string[] pathParts)
        {
            return Path.Combine(this.RepoRoot, Path.Combine(pathParts));
        }

        public string GetObjectPathTo(string objectHash)
        {
            return Path.Combine(
                this.RepoRoot,
                TestConstants.DotGit.Objects.Root,
                objectHash.Substring(0, 2),
                objectHash.Substring(2));
        }

        private static ScalarFunctionalTestEnlistment CloneAndMount(string pathToGvfs, string enlistmentRoot, string commitish, string localCacheRoot, bool skipPrefetch = false, bool fullClone = true)
        {
            ScalarFunctionalTestEnlistment enlistment = new ScalarFunctionalTestEnlistment(
                pathToGvfs,
                enlistmentRoot ?? GetUniqueEnlistmentRoot(),
                ScalarTestConfig.RepoToClone,
                commitish ?? Properties.Settings.Default.Commitish,
                localCacheRoot ?? ScalarTestConfig.LocalCacheRoot,
                fullClone);

            try
            {
                enlistment.CloneAndMount(skipPrefetch);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unhandled exception in CloneAndMount: " + e.ToString());
                TestResultsHelper.OutputScalarLogs(enlistment);
                throw;
            }

            return enlistment;
        }

        private static string GetRepoSpecificLocalCacheRoot(string enlistmentRoot)
        {
            return Path.Combine(enlistmentRoot, ScalarTestConfig.DotScalarRoot, ".scalarCache");
        }

        private bool WaitForStatus(int maxWaitMilliseconds, string statusShouldContain)
        {
            string status = null;
            int totalWaitMilliseconds = 0;
            while (totalWaitMilliseconds <= maxWaitMilliseconds && (status == null || !status.Contains(statusShouldContain)))
            {
                Thread.Sleep(SleepMSWaitingForStatusCheck);
                status = this.Status();
                totalWaitMilliseconds += SleepMSWaitingForStatusCheck;
            }

            return totalWaitMilliseconds <= maxWaitMilliseconds;
        }
    }
}
