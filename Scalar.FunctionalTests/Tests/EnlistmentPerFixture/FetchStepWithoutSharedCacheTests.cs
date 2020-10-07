using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Should;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System;
using System.IO;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    public class FetchStepWithoutSharedCacheTests : TestsWithEnlistmentPerFixture
    {
        private const string PrefetchPackPrefix = "prefetch";
        private const string TempPackFolder = "tempPacks";

        private FileSystemRunner fileSystem;

        // Set forcePerRepoObjectCache to true to avoid any of the tests inadvertently corrupting
        // the cache
        public FetchStepWithoutSharedCacheTests()
            : base(forcePerRepoObjectCache: true, skipFetchCommitsAndTreesDuringClone: true)
        {
            this.fileSystem = new SystemIORunner();
        }

        private string PackRoot
        {
            get
            {
                return this.Enlistment.GetPackRoot(this.fileSystem);
            }
        }

        private string TempPackRoot
        {
            get
            {
                return Path.Combine(this.PackRoot, TempPackFolder);
            }
        }

        [TestCase, Order(1)]
        public void FetchStepCommitsToEmptyCache()
        {
            this.Enlistment.RunVerb("fetch");

            // Verify prefetch pack(s) are in packs folder and have matching idx file
            string[] prefetchPacks = this.ReadPrefetchPackFileNames();
            this.AllPrefetchPacksShouldHaveIdx(prefetchPacks);

            // Verify tempPacks is empty
            this.TempPackRoot.ShouldBeADirectory(this.fileSystem).WithNoItems();
        }

        private void PackShouldHaveIdxFile(string pathPath)
        {
            string idxPath = Path.ChangeExtension(pathPath, ".idx");
            idxPath.ShouldBeAFile(this.fileSystem).WithContents().Length.ShouldBeAtLeast(1, $"{idxPath} is unexepectedly empty");
        }

        private void AllPrefetchPacksShouldHaveIdx(string[] prefetchPacks)
        {
            prefetchPacks.Length.ShouldBeAtLeast(1, "There should be at least one prefetch pack");

            foreach (string prefetchPack in prefetchPacks)
            {
                this.PackShouldHaveIdxFile(prefetchPack);
            }
        }

        private string[] ReadPrefetchPackFileNames()
        {
            return Directory.GetFiles(this.PackRoot, $"{PrefetchPackPrefix}*.pack");
        }
    }
}
