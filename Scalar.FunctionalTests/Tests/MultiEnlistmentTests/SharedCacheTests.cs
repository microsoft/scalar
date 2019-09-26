using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Should;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Scalar.FunctionalTests.Tests.MultiEnlistmentTests
{
    [TestFixture]
    [Category(Categories.ExtraCoverage)]
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
        public void SecondCloneDoesNotDownloadAdditionalObjects()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CloneAndMountEnlistment();
            File.ReadAllText(Path.Combine(enlistment1.RepoRoot, WellKnownFile));

            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment1);

            string[] allObjects = Directory.EnumerateFiles(enlistment1.LocalCacheRoot, "*", SearchOption.AllDirectories).ToArray();

            ScalarFunctionalTestEnlistment enlistment2 = this.CloneAndMountEnlistment();
            File.ReadAllText(Path.Combine(enlistment2.RepoRoot, WellKnownFile));

            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment2);

            enlistment2.LocalCacheRoot.ShouldEqual(enlistment1.LocalCacheRoot, "Sanity: Local cache roots are expected to match.");
            Directory.EnumerateFiles(enlistment2.LocalCacheRoot, "*", SearchOption.AllDirectories)
                .ShouldMatchInOrder(allObjects);
        }

        [TestCase]
        [Category(Categories.MacTODO.NeedsServiceVerb)]
        public void CloneCleansUpStaleMetadataLock()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CloneAndMountEnlistment();
            string metadataLockPath = Path.Combine(this.localCachePath, "mapping.dat.lock");
            metadataLockPath.ShouldNotExistOnDisk(this.fileSystem);
            this.fileSystem.WriteAllText(metadataLockPath, enlistment1.EnlistmentRoot);
            metadataLockPath.ShouldBeAFile(this.fileSystem);

            ScalarFunctionalTestEnlistment enlistment2 = this.CloneAndMountEnlistment();
            metadataLockPath.ShouldNotExistOnDisk(this.fileSystem);

            enlistment1.Status().ShouldContain("Mount status: Ready");
            enlistment2.Status().ShouldContain("Mount status: Ready");
        }

        [TestCase]
        public void ParallelDownloadsInSharedCache()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CloneAndMountEnlistment();
            ScalarFunctionalTestEnlistment enlistment2 = this.CloneAndMountEnlistment();
            ScalarFunctionalTestEnlistment enlistment3 = null;

            Task task1 = Task.Run(() => this.LoadBlobsViaGit(enlistment1));
            Task task2 = Task.Run(() => this.LoadBlobsViaGit(enlistment2));
            Task task3 = Task.Run(() => enlistment3 = this.CloneAndMountEnlistment());

            task1.Wait();
            task2.Wait();
            task3.Wait();

            task1.Exception.ShouldBeNull();
            task2.Exception.ShouldBeNull();
            task3.Exception.ShouldBeNull();

            enlistment1.Status().ShouldContain("Mount status: Ready");
            enlistment2.Status().ShouldContain("Mount status: Ready");
            enlistment3.Status().ShouldContain("Mount status: Ready");

            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment1);
            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment2);
            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment3);
        }

        [TestCase]
        public void DeleteObjectsCacheAndCacheMappingBeforeMount()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CloneAndMountEnlistment();
            ScalarFunctionalTestEnlistment enlistment2 = this.CloneAndMountEnlistment();

            enlistment1.UnmountScalar();

            string objectsRoot = ScalarHelpers.GetPersistedGitObjectsRoot(enlistment1.DotScalarRoot).ShouldNotBeNull();
            objectsRoot.ShouldBeADirectory(this.fileSystem);
            RepositoryHelpers.DeleteTestDirectory(objectsRoot);

            string metadataPath = Path.Combine(this.localCachePath, "mapping.dat");
            metadataPath.ShouldBeAFile(this.fileSystem);
            this.fileSystem.DeleteFile(metadataPath);

            enlistment1.MountScalar();

            Task task1 = Task.Run(() => this.LoadBlobsViaGit(enlistment1));
            Task task2 = Task.Run(() => this.LoadBlobsViaGit(enlistment2));
            task1.Wait();
            task2.Wait();
            task1.Exception.ShouldBeNull();
            task2.Exception.ShouldBeNull();

            enlistment1.Status().ShouldContain("Mount status: Ready");
            enlistment2.Status().ShouldContain("Mount status: Ready");

            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment1);
            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment2);
        }

        [TestCase]
        public void DownloadingACommitWithoutTreesDoesntBreakNextClone()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CloneAndMountEnlistment();
            GitProcess.Invoke(enlistment1.RepoRoot, "cat-file -s " + WellKnownCommitSha).ShouldEqual("293\n");

            ScalarFunctionalTestEnlistment enlistment2 = this.CloneAndMountEnlistment(WellKnownBranch);
            enlistment2.Status().ShouldContain("Mount status: Ready");
        }

        [TestCase]
        public void MountReusesLocalCacheKeyWhenGitObjectsRootDeleted()
        {
            ScalarFunctionalTestEnlistment enlistment = this.CloneAndMountEnlistment();

            enlistment.UnmountScalar();

            // Find the current git objects root and ensure it's on disk
            string objectsRoot = ScalarHelpers.GetPersistedGitObjectsRoot(enlistment.DotScalarRoot).ShouldNotBeNull();
            objectsRoot.ShouldBeADirectory(this.fileSystem);

            string mappingFilePath = Path.Combine(enlistment.LocalCacheRoot, "mapping.dat");
            string mappingFileContents = this.fileSystem.ReadAllText(mappingFilePath);
            mappingFileContents.Length.ShouldNotEqual(0, "mapping.dat should not be empty");

            // Delete the git objects root folder, mount should re-create it and the mapping.dat file should not change
            RepositoryHelpers.DeleteTestDirectory(objectsRoot);

            enlistment.MountScalar();

            ScalarHelpers.GetPersistedGitObjectsRoot(enlistment.DotScalarRoot).ShouldEqual(objectsRoot);
            objectsRoot.ShouldBeADirectory(this.fileSystem);
            mappingFilePath.ShouldBeAFile(this.fileSystem).WithContents(mappingFileContents);

            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment);
        }

        [TestCase]
        public void MountUsesNewLocalCacheKeyWhenLocalCacheDeleted()
        {
            ScalarFunctionalTestEnlistment enlistment = this.CloneAndMountEnlistment();

            enlistment.UnmountScalar();

            // Find the current git objects root and ensure it's on disk
            string objectsRoot = ScalarHelpers.GetPersistedGitObjectsRoot(enlistment.DotScalarRoot).ShouldNotBeNull();
            objectsRoot.ShouldBeADirectory(this.fileSystem);

            string mappingFilePath = Path.Combine(enlistment.LocalCacheRoot, "mapping.dat");
            string mappingFileContents = this.fileSystem.ReadAllText(mappingFilePath);
            mappingFileContents.Length.ShouldNotEqual(0, "mapping.dat should not be empty");

            // Delete the local cache folder, mount should re-create it and generate a new mapping file and local cache key
            RepositoryHelpers.DeleteTestDirectory(enlistment.LocalCacheRoot);

            enlistment.MountScalar();

            // Mount should recreate the local cache root
            enlistment.LocalCacheRoot.ShouldBeADirectory(this.fileSystem);

            // Determine the new local cache key
            string newMappingFileContents = mappingFilePath.ShouldBeAFile(this.fileSystem).WithContents();
            const int GuidStringLength = 32;
            string mappingFileKey = "A {\"Key\":\"https://scalar.visualstudio.com/ci/_git/fortests\",\"Value\":\"";
            int localKeyIndex = newMappingFileContents.IndexOf(mappingFileKey);
            string newCacheKey = newMappingFileContents.Substring(localKeyIndex + mappingFileKey.Length, GuidStringLength);

            // Validate the new objects root is on disk and uses the new key
            objectsRoot.ShouldNotExistOnDisk(this.fileSystem);
            string newObjectsRoot = ScalarHelpers.GetPersistedGitObjectsRoot(enlistment.DotScalarRoot);
            newObjectsRoot.ShouldNotEqual(objectsRoot);
            newObjectsRoot.ShouldContain(newCacheKey);
            newObjectsRoot.ShouldBeADirectory(this.fileSystem);

            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment);
        }

        [TestCase]
        public void SecondCloneSucceedsWithMissingTrees()
        {
            string newCachePath = Path.Combine(this.localCacheParentPath, ".customScalarCache2");
            ScalarFunctionalTestEnlistment enlistment1 = this.CreateNewEnlistment(localCacheRoot: newCachePath, skipPrefetch: true);
            File.ReadAllText(Path.Combine(enlistment1.RepoRoot, WellKnownFile));
            this.AlternatesFileShouldHaveGitObjectsRoot(enlistment1);

            // This Git command loads the commit and root tree for WellKnownCommitSha,
            // but does not download any more reachable objects.
            string command = "cat-file -p origin/" + WellKnownBranch + "^{tree}";
            ProcessResult result = GitHelpers.InvokeGitAgainstScalarRepo(enlistment1.RepoRoot, command);
            result.ExitCode.ShouldEqual(0, $"git {command} failed on {nameof(enlistment1)} with error: {result.Errors}");

            // If we did not properly check the failed checkout at this step, then clone will fail during checkout.
            ScalarFunctionalTestEnlistment enlistment2 = this.CreateNewEnlistment(localCacheRoot: newCachePath, branch: WellKnownBranch, skipPrefetch: true);
            result = GitHelpers.InvokeGitAgainstScalarRepo(enlistment2.RepoRoot, command);
            result.ExitCode.ShouldEqual(0, $"git {command} failed on {nameof(enlistment2)} with error: {result.Errors}");
        }

        // Override OnTearDownEnlistmentsDeleted rathern than using [TearDown] as the enlistments need to be unmounted before
        // localCacheParentPath can be deleted (as the SQLite blob sizes database cannot be deleted while Scalar is mounted)
        protected override void OnTearDownEnlistmentsDeleted()
        {
            RepositoryHelpers.DeleteTestDirectory(this.localCacheParentPath);
        }

        private ScalarFunctionalTestEnlistment CloneAndMountEnlistment(string branch = null)
        {
            return this.CreateNewEnlistment(this.localCachePath, branch);
        }

        private void AlternatesFileShouldHaveGitObjectsRoot(ScalarFunctionalTestEnlistment enlistment)
        {
            string objectsRoot = ScalarHelpers.GetPersistedGitObjectsRoot(enlistment.DotScalarRoot);
            string alternatesFileContents = Path.Combine(enlistment.RepoRoot, ".git", "objects", "info", "alternates").ShouldBeAFile(this.fileSystem).WithContents();
            alternatesFileContents.ShouldEqual(objectsRoot);
        }

        private void LoadBlobsViaGit(ScalarFunctionalTestEnlistment enlistment)
        {
            // 'git rev-list --objects' will check for all objects' existence, which
            // triggers an object download on every missing blob.
            ProcessResult result = GitHelpers.InvokeGitAgainstScalarRepo(enlistment.RepoRoot, "rev-list --all --objects");
            result.ExitCode.ShouldEqual(0);
        }
    }
}
