using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Should;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System;
using System.IO;
using System.Linq;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    [NonParallelizable]
    public class FetchStepTests : TestsWithEnlistmentPerFixture
    {
        private const string FetchCommitsAndTreesLock = "fetch-commits-trees.lock";

        private FileSystemRunner fileSystem;

        public FetchStepTests()
        {
            this.fileSystem = new SystemIORunner();
        }

        [TestCase]
        [Category(Categories.MacTODO.TestNeedsToLockFile)]
        public void FetchStepCleansUpStaleFetchLock()
        {
            this.Enlistment.FetchStep();
            string fetchCommitsLockFile = Path.Combine(
                ScalarHelpers.GetObjectsRootFromGitConfig(this.Enlistment.RepoRoot),
                "pack",
                FetchCommitsAndTreesLock);
            fetchCommitsLockFile.ShouldNotExistOnDisk(this.fileSystem);
            this.fileSystem.WriteAllText(fetchCommitsLockFile, this.Enlistment.EnlistmentRoot);
            fetchCommitsLockFile.ShouldBeAFile(this.fileSystem);

            this.fileSystem
                .EnumerateDirectory(this.Enlistment.GetPackRoot(this.fileSystem))
                .Split()
                .Where(file => string.Equals(Path.GetExtension(file), ".keep", StringComparison.OrdinalIgnoreCase))
                .Count()
                .ShouldEqual(1, "Incorrect number of .keep files in pack directory");

            this.Enlistment.FetchStep();
            fetchCommitsLockFile.ShouldNotExistOnDisk(this.fileSystem);
        }
    }
}
