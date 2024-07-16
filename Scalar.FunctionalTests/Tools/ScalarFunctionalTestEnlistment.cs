using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Tests;
using System;
using System.IO;

namespace Scalar.FunctionalTests.Tools
{
    public class ScalarFunctionalTestEnlistment
    {
        private const string GitRepoSrcDir = "repo";
        private const string LockHeldByGit = "Scalar Lock: Held by {0}";
        private const int SleepMSWaitingForStatusCheck = 100;
        private const int DefaultMaxWaitMSForStatusCheck = 5000;
        private static readonly string ZeroBackgroundOperations = "Background operations: 0" + Environment.NewLine;
        private readonly bool fullClone;

        private ScalarProcess scalarProcess;

        private ScalarFunctionalTestEnlistment(string pathToScalar, string enlistmentRoot, string repoUrl, string commitish, string localCacheRoot = null, bool fullClone = true, bool isScalarRepo = true)
        {
            this.EnlistmentRoot = enlistmentRoot;
            this.RepoUrl = repoUrl;
            this.Commitish = commitish;
            this.fullClone = fullClone;
            this.IsScalarRepo = isScalarRepo;

            if (isScalarRepo)
            {
                this.RepoRoot = Path.Combine(this.EnlistmentRoot, "src");
            }
            else
            {
                this.RepoRoot = this.EnlistmentRoot;
            }

            if (localCacheRoot == null)
            {
                if (!this.IsScalarRepo)
                {
                    localCacheRoot = Path.Combine(this.RepoRoot, ".git", "objects");
                }
                else if (ScalarTestConfig.NoSharedCache)
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

        public bool IsScalarRepo { get; }

        public string LocalCacheRoot { get; }

        public string RepoRoot { get; }

        public string ScalarLogsRoot
        {
            get { return Path.Combine(this.RepoRoot, ".git", "logs"); }
        }

        public string DiagnosticsRoot
        {
            get { return Path.Combine(this.EnlistmentRoot, ".scalarDiagnostics"); }
        }

        public string Commitish
        {
            get; private set;
        }

        public static ScalarFunctionalTestEnlistment CloneWithPerRepoCache(string pathToGvfs, bool skipFetchCommitsAndTrees)
        {
            string enlistmentRoot = ScalarFunctionalTestEnlistment.GetUniqueEnlistmentRoot();
            string localCache = ScalarFunctionalTestEnlistment.GetRepoSpecificLocalCacheRoot(enlistmentRoot);
            return Clone(pathToGvfs, enlistmentRoot, null, localCacheRoot: localCache, skipFetchCommitsAndTrees: skipFetchCommitsAndTrees);
        }

        public static ScalarFunctionalTestEnlistment Clone(
            string pathToScalar,
            string commitish = null,
            string localCacheRoot = null,
            bool skipFetchCommitsAndTrees = false,
            bool fullClone = true,
            string url = null)
        {
            string enlistmentRoot = ScalarFunctionalTestEnlistment.GetUniqueEnlistmentRoot();
            return Clone(pathToScalar, enlistmentRoot, commitish, localCacheRoot, skipFetchCommitsAndTrees, fullClone, url);
        }

        public static ScalarFunctionalTestEnlistment CloneEnlistmentWithSpacesInPath(string pathToScalar, string commitish = null)
        {
            string enlistmentRoot = ScalarFunctionalTestEnlistment.GetUniqueEnlistmentRootWithSpaces();
            string localCache = ScalarFunctionalTestEnlistment.GetRepoSpecificLocalCacheRoot(enlistmentRoot);
            return Clone(pathToScalar, enlistmentRoot, commitish, localCache);
        }

        public static string GetUniqueEnlistmentRoot()
        {
            return Path.Combine(Properties.Settings.Default.EnlistmentRoot, Guid.NewGuid().ToString("N").Substring(0, 20));
        }

        public static string GetUniqueEnlistmentRootWithSpaces()
        {
            return Path.Combine(Properties.Settings.Default.EnlistmentRoot, "test " + Guid.NewGuid().ToString("N").Substring(0, 15));
        }

        public static ScalarFunctionalTestEnlistment CloneGitRepo(string pathToScalar)
        {
            string enlistmentRoot = Path.Combine(GetUniqueEnlistmentRoot(), GitRepoSrcDir);

            ScalarFunctionalTestEnlistment enlistment = new ScalarFunctionalTestEnlistment(
                pathToScalar,
                enlistmentRoot,
                ScalarTestConfig.RepoToClone,
                Properties.Settings.Default.Commitish,
                ScalarTestConfig.LocalCacheRoot,
                isScalarRepo: false);

            try
            {
                enlistment.CloneGitRepo();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unhandled exception in {nameof(ScalarFunctionalTestEnlistment.Clone)}: " + e.ToString());
                TestResultsHelper.OutputScalarLogs(enlistment);
                throw;
            }

            return enlistment;
        }

        public string GetPackRoot(FileSystemRunner fileSystem)
        {
            return Path.Combine(ScalarHelpers.GetObjectsRootFromGitConfig(this.RepoRoot), "pack");
        }

        public void DeleteEnlistment()
        {
            // Technically, we no longer try to start Watchman in the Functional Test Suite.
            // However, on Linux (which doesn't currently support the Builtin FSMonitor), we
            // allow the test startup to fallback and use Watchman (rather than failing the
            // whole test).  So for now, we keep this code to unregister.
            //
            string watchmanLocation = ProcessHelper.GetProgramLocation("watchman");
            if (!string.IsNullOrEmpty(watchmanLocation))
            {
                try
                {
                    ProcessHelper.Run(Path.Combine(watchmanLocation, "watchman"), $"watch-del {this.RepoRoot}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete watch on {this.RepoRoot}. {ex.ToString()}");
                }
            }

            // Shutdown the Builtin FSMonitor, if present.  We don't know if the daemon is running
            // at all -- or whether test suite explicitly started -- or if it was implicitly started
            // by one of the Git commands that we invoked during test.  So just try to stop it and
            // ignore any errors.
            //
            GitProcess.InvokeProcess(this.RepoRoot, "fsmonitor--daemon stop");

            TestResultsHelper.OutputScalarLogs(this);
            RepositoryHelpers.DeleteTestDirectory(this.EnlistmentRoot);
        }

        public void Clone(bool skipFetchCommitsAndTrees)
        {
            this.scalarProcess.Clone(this.RepoUrl, this.Commitish, skipFetchCommitsAndTrees, fullClone: this.fullClone);
            this.InitializeConfig();
        }

        public void CloneGitRepo()
        {
            string workDir = Directory.GetParent(this.RepoRoot).FullName;
            Directory.CreateDirectory(workDir);
            GitProcess.Invoke(workDir, $"clone {this.RepoUrl} {GitRepoSrcDir}");
            this.InitializeConfig();
        }

        public string RunVerb(string task, long? batchSize = null, bool failOnError = true)
        {
            return this.scalarProcess.RunVerb(task, batchSize, failOnError);
        }

        public string RunMaintenanceTask(string task, string config = null)
        {
            return GitProcess.Invoke(this.RepoRoot, $"{config}maintenance run --task={task}");
        }

        public void Unregister()
        {
            this.scalarProcess.Unregister(this.EnlistmentRoot);
        }

        public void Repair(bool confirm)
        {
            this.scalarProcess.Repair(confirm);
        }

        public string Diagnose()
        {
            return this.scalarProcess.Diagnose();
        }

        public string Status(string trace = null)
        {
            return this.scalarProcess.Status(trace);
        }

        public string Register()
        {
            return this.scalarProcess.Register(this.EnlistmentRoot);
        }

        public string GetCacheServer()
        {
            return this.scalarProcess.CacheServer("--get");
        }

        public string SetCacheServer(string arg)
        {
            return this.scalarProcess.CacheServer("--set " + arg);
        }

        public void DeleteAll()
        {
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

        private void InitializeConfig()
        {
            GitProcess.Invoke(this.RepoRoot, "checkout " + this.Commitish);
            GitProcess.Invoke(this.RepoRoot, "branch --unset-upstream");
            GitProcess.Invoke(this.RepoRoot, "config core.abbrev 40");
            GitProcess.Invoke(this.RepoRoot, "config advice.sparseIndexExpanded false");
            GitProcess.Invoke(this.RepoRoot, "config user.name \"Functional Test User\"");
            GitProcess.Invoke(this.RepoRoot, "config user.email \"functional@test.com\"");
        }

        private static ScalarFunctionalTestEnlistment Clone(
            string pathToScalar,
            string enlistmentRoot,
            string commitish,
            string localCacheRoot,
            bool skipFetchCommitsAndTrees = false,
            bool fullClone = true,
            string url = null)
        {
            enlistmentRoot = enlistmentRoot ?? GetUniqueEnlistmentRoot();

            ScalarFunctionalTestEnlistment enlistment = new ScalarFunctionalTestEnlistment(
                pathToScalar,
                enlistmentRoot,
                url ?? ScalarTestConfig.RepoToClone,
                commitish ?? Properties.Settings.Default.Commitish,
                localCacheRoot ?? ScalarTestConfig.LocalCacheRoot,
                fullClone);

            try
            {
                enlistment.Clone(skipFetchCommitsAndTrees);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unhandled exception in {nameof(ScalarFunctionalTestEnlistment.Clone)}: " + e.ToString());
                TestResultsHelper.OutputScalarLogs(enlistment);
                throw;
            }

            return enlistment;
        }

        private static string GetRepoSpecificLocalCacheRoot(string enlistmentRoot)
        {
            return Path.Combine(enlistmentRoot, ".scalarCache");
        }
    }
}
