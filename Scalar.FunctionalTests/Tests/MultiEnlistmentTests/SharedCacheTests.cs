using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Should;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Scalar.FunctionalTests.Tests.MultiEnlistmentTests
{
    [TestFixture]
    public class SharedCacheTests : TestsWithMultiEnlistment
    {
        private const string WellKnownFile = "Readme.md";

        // This branch and commit sha should point to the same place.
        private const string WellKnownBranch = "FunctionalTests/20170602";
        private const string WellKnownCommitSha = "42eb6632beffae26893a3d6e1a9f48d652327c6f";

        private string localCachePath;
        private string localCacheParentPath;

        private FileSystemRunner fileSystem;

        public SharedCacheTests()
        {
            this.fileSystem = new SystemIORunner();
        }

        [SetUp]
        public void SetCacheLocation()
        {
            this.localCacheParentPath = Path.Combine(Properties.Settings.Default.EnlistmentRoot, "..", Guid.NewGuid().ToString("N"));
            this.localCachePath = Path.Combine(this.localCacheParentPath, ".customScalarCache");
        }

        [TestCase]
        public void ParallelDownloadsInSharedCache()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CloneEnlistment();
            ScalarFunctionalTestEnlistment enlistment2 = this.CloneEnlistment();
            ScalarFunctionalTestEnlistment enlistment3 = null;

            Task task1 = Task.Run(() => this.LoadBlobsViaGit(enlistment1));
            Task task2 = Task.Run(() => this.LoadBlobsViaGit(enlistment2));
            Task task3 = Task.Run(() => enlistment3 = this.CloneEnlistment());

            task1.Wait();
            task2.Wait();
            task3.Wait();

            task1.Exception.ShouldBeNull();
            task2.Exception.ShouldBeNull();
            task3.Exception.ShouldBeNull();

            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment1);
            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment2);
            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment3);
        }

        [TestCase]
        public void DownloadingACommitWithoutTreesDoesntBreakNextClone()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CloneEnlistment();
            GitProcess.Invoke(enlistment1.RepoRoot, "cat-file -s " + WellKnownCommitSha).ShouldEqual("293\n");

            ScalarFunctionalTestEnlistment enlistment2 = this.CloneEnlistment(WellKnownBranch);
        }

        [TestCase]
        public void GitObjectsRecreatedWhenDownloadingObjects()
        {
            ScalarFunctionalTestEnlistment enlistment = this.CloneEnlistment();

            // Find the current git objects root and ensure it's on disk
            string objectsRoot = ScalarHelpers.GetObjectsRootFromGitConfig(enlistment.RepoRoot);
            objectsRoot.ShouldBeADirectory(this.fileSystem);

            RepositoryHelpers.DeleteTestDirectory(objectsRoot);

            ScalarHelpers.GetObjectsRootFromGitConfig(enlistment.RepoRoot).ShouldEqual(objectsRoot);

            // Downloading objects should recreate the objects directory
            this.LoadBlobsViaGit(enlistment);

            objectsRoot.ShouldBeADirectory(this.fileSystem);

            // The alternates file shouldn't have changed
            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment);
        }

        [TestCase]
        [Category(Categories.MacTODO.NeedsServiceVerb)]
        public void SecondCloneSucceedsWithMissingTrees()
        {
            string newCachePath = Path.Combine(this.localCacheParentPath, ".customScalarCache2");
            ScalarFunctionalTestEnlistment enlistment1 = this.CreateNewEnlistment(localCacheRoot: newCachePath, skipFetchCommitsAndTrees: true);
            File.ReadAllText(Path.Combine(enlistment1.RepoRoot, WellKnownFile));
            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment1);

            // This Git command loads the commit and root tree for WellKnownCommitSha,
            // but does not download any more reachable objects.
            string command = "cat-file -p origin/" + WellKnownBranch + "^{tree}";
            ProcessResult result = GitHelpers.InvokeGitAgainstScalarRepo(enlistment1.RepoRoot, command);
            result.ExitCode.ShouldEqual(0, $"git {command} failed on {nameof(enlistment1)} with error: {result.Errors}");

            // If we did not properly check the failed checkout at this step, then clone will fail during checkout.
            ScalarFunctionalTestEnlistment enlistment2 = this.CreateNewEnlistment(localCacheRoot: newCachePath, branch: WellKnownBranch, skipFetchCommitsAndTrees: true);
            result = GitHelpers.InvokeGitAgainstScalarRepo(enlistment2.RepoRoot, command);
            result.ExitCode.ShouldEqual(0, $"git {command} failed on {nameof(enlistment2)} with error: {result.Errors}");
        }

        // Override OnTearDownEnlistmentsDeleted rathern than using [TearDown] as the enlistments need to be unmounted before
        // localCacheParentPath can be deleted (as the SQLite blob sizes database cannot be deleted while Scalar is mounted)
        protected override void OnTearDownEnlistmentsDeleted()
        {
            RepositoryHelpers.DeleteTestDirectory(this.localCacheParentPath);
        }

        private ScalarFunctionalTestEnlistment CloneEnlistment(string branch = null)
        {
            return this.CreateNewEnlistment(this.localCachePath, branch);
        }

        private void AlternatesFileShouldHaveGitObjectsRoot(ScalarFunctionalTestEnlistment enlistment)
        {
            string objectsRoot = ScalarHelpers.GetObjectsRootFromGitConfig(enlistment.RepoRoot);
            string alternatesFileContents = Path.Combine(enlistment.RepoRoot, ".git", "objects", "info", "alternates").ShouldBeAFile(this.fileSystem).WithContents();
            alternatesFileContents.ShouldEqual(objectsRoot);
        }

        private void LoadBlobsViaGit(ScalarFunctionalTestEnlistment enlistment)
        {
            // 'git rev-list --objects' will check for all objects' existence, which
            // triggers an object download on every missing blob.
            ProcessResult result = GitHelpers.InvokeGitAgainstScalarRepo(enlistment.RepoRoot, "rev-list --all --objects");
            result.ExitCode.ShouldEqual(0, result.Errors);
        }
    }
}
