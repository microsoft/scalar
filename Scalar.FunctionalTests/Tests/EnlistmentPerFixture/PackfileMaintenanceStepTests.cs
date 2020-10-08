using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    public class PackfileMaintenanceStepTests : TestsWithEnlistmentPerFixture
    {
        private const long BatchSizeForNoRepack = 1024 * 1024 * 1024L;
        private FileSystemRunner fileSystem;

        // Set forcePerRepoObjectCache to true to avoid any of the tests inadvertently corrupting
        // the cache
        public PackfileMaintenanceStepTests()
            : base(forcePerRepoObjectCache: true)
        {
            this.fileSystem = new SystemIORunner();
        }

        private string GitObjectRoot => ScalarHelpers.GetObjectsRootFromGitConfig(this.Enlistment.RepoRoot);
        private string PackRoot => this.Enlistment.GetPackRoot(this.fileSystem);

        [TestCase, Order(1)]
        public void RepackAllToOnePack()
        {
            this.GetPackSizes(
                    out int beforeFetchCommitsAndTreesPackCount,
                    out long maxSize,
                    out long minSize,
                    out long totalSize);

            // Cannot be sure of the count, but there should be two from the inital clone
            beforeFetchCommitsAndTreesPackCount.ShouldBeAtLeast(2);

            // Create a multi-pack-index that covers the prefetch packs
            // (The pack-files job creates a multi-pack-index only after a fetch-commits-and-trees)
            GitProcess.InvokeProcess(
                this.Enlistment.RepoRoot,
                $"multi-pack-index write --object-dir={this.GitObjectRoot}");

            // Run the step to ensure we don't have any packs that will be expired during the repack step.
            // Use a non-standard, but large batchSize to avoid logic that resizes the batchSize.
            this.Enlistment.RunVerb("pack-files", batchSize: BatchSizeForNoRepack);

            this.GetPackSizes(out int afterPrefetchPackCount, out maxSize, out minSize, out totalSize);

            // Cannot be sure of the count, as the fetch-commits-and-trees uses parallel threads to get multiple packs
            afterPrefetchPackCount.ShouldBeAtLeast(2);

            // Batch size 0 tells Git to repack everything into a single pack!
            this.Enlistment.RunVerb("pack-files", batchSize: 0);
        }

        [TestCase, Order(2)]
        [Category(Categories.WindowsOnly)]
        public void ExpireAllButOneAndKeep()
        {
            string prefetchPack = Directory.GetFiles(this.PackRoot, "prefetch-*.pack")
                                           .FirstOrDefault();

            prefetchPack.ShouldNotBeNull();

            // We should expire all packs except the one we just created,
            // and the prefetch pack which is marked as ".keep"
            this.Enlistment.RunVerb("pack-files", batchSize: BatchSizeForNoRepack);

            List<string> packsAfter = this.GetPackfiles();

            packsAfter.Count.ShouldEqual(2, $"incorrect number of packs after final expire step: {packsAfter.Count}");
            packsAfter.Contains(prefetchPack).ShouldBeTrue($"packsAfter does not contain prefetch pack ({prefetchPack})");
        }

        private List<string> GetPackfiles()
        {
            return Directory.GetFiles(this.PackRoot, "*.pack").ToList();
        }

        private void GetPackSizes(out int packCount, out long maxSize, out long minSize, out long totalSize)
        {
            totalSize = 0;
            maxSize = 0;
            minSize = long.MaxValue;
            packCount = 0;

            foreach (string file in this.GetPackfiles())
            {
                packCount++;
                long size = new FileInfo(Path.Combine(this.PackRoot, file)).Length;
                totalSize += size;

                if (size > maxSize)
                {
                    maxSize = size;
                }

                if (size < minSize)
                {
                    minSize = size;
                }
            }
        }
    }
}
