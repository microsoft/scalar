using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Should;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System;
using System.IO;
using System.Linq;
using System.Threading;

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
            this.PostFetchStepShouldComplete();
            string prefetchCommitsLockFile = Path.Combine(this.Enlistment.GetObjectRoot(this.fileSystem), "pack", PrefetchCommitsAndTreesLock);
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
            this.PostFetchStepShouldComplete();
            prefetchCommitsLockFile.ShouldNotExistOnDisk(this.fileSystem);
        }

        private void PostFetchStepShouldComplete()
        {
            string objectDir = this.Enlistment.GetObjectRoot(this.fileSystem);
            string objectCacheLock = Path.Combine(objectDir, "git-maintenance-step.lock");

            // Wait first, to hopefully ensure the background thread has
            // started before we check for the lock file.
            do
            {
                Thread.Sleep(500);
            }
            while (this.fileSystem.FileExists(objectCacheLock));

            // A commit graph is not always generated, but if it is, then we want to ensure it is in a good state
            if (this.fileSystem.FileExists(Path.Combine(objectDir, "info", "commit-graphs", "commit-graph-chain")))
            {
                ProcessResult graphResult = GitProcess.InvokeProcess(this.Enlistment.RepoRoot, "commit-graph verify --shallow --object-dir=\"" + objectDir + "\"");
                graphResult.ExitCode.ShouldEqual(0);
            }
        }
    }
}
