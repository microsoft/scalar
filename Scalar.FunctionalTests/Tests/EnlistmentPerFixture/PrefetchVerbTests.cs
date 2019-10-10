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
    public class PrefetchVerbTests : TestsWithEnlistmentPerFixture
    {
        private const string PrefetchCommitsAndTreesLock = "prefetch-commits-trees.lock";

        private FileSystemRunner fileSystem;

        public PrefetchVerbTests()
        {
            this.fileSystem = new SystemIORunner();
        }

        [TestCase]
        [Category(Categories.MacTODO.TestNeedsToLockFile)]
        public void PrefetchCleansUpStalePrefetchLock()
        {
            this.Enlistment.Prefetch();
            string prefetchCommitsLockFile = Path.Combine(
                ScalarHelpers.GetObjectsRootFromGitConfig(this.Enlistment.RepoRoot),
                "pack",
                PrefetchCommitsAndTreesLock);
            prefetchCommitsLockFile.ShouldNotExistOnDisk(this.fileSystem);
            this.fileSystem.WriteAllText(prefetchCommitsLockFile, this.Enlistment.EnlistmentRoot);
            prefetchCommitsLockFile.ShouldBeAFile(this.fileSystem);

            this.fileSystem
                .EnumerateDirectory(this.Enlistment.GetPackRoot(this.fileSystem))
                .Split()
                .Where(file => string.Equals(Path.GetExtension(file), ".keep", StringComparison.OrdinalIgnoreCase))
                .Count()
                .ShouldEqual(1, "Incorrect number of .keep files in pack directory");

            this.Enlistment.Prefetch();
            prefetchCommitsLockFile.ShouldNotExistOnDisk(this.fileSystem);
        }
    }
}
