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

        [TestCase, Order(2)]
        public void FetchStepBuildsIdxWhenMissingFromPrefetchPack()
        {
            string[] prefetchPacks = this.ReadPrefetchPackFileNames();
            prefetchPacks.Length.ShouldBeAtLeast(1, "There should be at least one prefetch pack");

            string idxPath = Path.ChangeExtension(prefetchPacks[0], ".idx");
            idxPath.ShouldBeAFile(this.fileSystem);
            File.SetAttributes(idxPath, FileAttributes.Normal);
            this.fileSystem.DeleteFile(idxPath);
            idxPath.ShouldNotExistOnDisk(this.fileSystem);

            // fetch should rebuild the missing idx
            this.Enlistment.RunVerb("fetch");

            idxPath.ShouldBeAFile(this.fileSystem);

            // All of the original prefetch packs should still be present
            string[] newPrefetchPacks = this.ReadPrefetchPackFileNames();
            newPrefetchPacks.ShouldContain(prefetchPacks, (item, expectedValue) => { return string.Equals(item, expectedValue); });
            this.AllPrefetchPacksShouldHaveIdx(newPrefetchPacks);
            this.TempPackRoot.ShouldBeADirectory(this.fileSystem).WithNoItems();
        }

        [TestCase, Order(3)]
        public void FetchStepCleansUpBadPrefetchPack()
        {
            string[] prefetchPacks = this.ReadPrefetchPackFileNames();
            long mostRecentPackTimestamp = this.GetMostRecentPackTimestamp(prefetchPacks);

            // Create a bad pack that is newer than the most recent pack
            string badContents = "BADPACK";
            string badPackPath = Path.Combine(this.PackRoot, $"{PrefetchPackPrefix}-{mostRecentPackTimestamp + 1}-{Guid.NewGuid().ToString("N")}.pack");
            this.fileSystem.WriteAllText(badPackPath, badContents);
            badPackPath.ShouldBeAFile(this.fileSystem).WithContents(badContents);

            // fetch should delete the bad pack
            this.Enlistment.RunVerb("fetch");

            badPackPath.ShouldNotExistOnDisk(this.fileSystem);

            // All of the original prefetch packs should still be present
            string[] newPrefetchPacks = this.ReadPrefetchPackFileNames();
            newPrefetchPacks.ShouldContain(prefetchPacks, (item, expectedValue) => { return string.Equals(item, expectedValue); });
            this.AllPrefetchPacksShouldHaveIdx(newPrefetchPacks);
            this.TempPackRoot.ShouldBeADirectory(this.fileSystem).WithNoItems();
        }

        [TestCase, Order(4)]
        public void FetchStepCleansUpOrIgnoresBadPrefetchPacksWithCaseDifferingNames()
        {
            string[] prefetchPacks = this.ReadPrefetchPackFileNames();
            long mostRecentPackTimestamp = this.GetMostRecentPackTimestamp(prefetchPacks);

            // Create two bad packs that are newer than the most recent pack
            // and have uppercase versions of the filename prefix and extension
            string badContents = "BADPACK";
            string badPackPath1 = Path.Combine(this.PackRoot, $"{PrefetchPackPrefix}-{mostRecentPackTimestamp + 1}-{Guid.NewGuid().ToString("N")}.PACK");
            string badPackPath2 = Path.Combine(this.PackRoot, $"{PrefetchPackPrefix.ToUpper()}-{mostRecentPackTimestamp + 2}-{Guid.NewGuid().ToString("N")}.pack");
            this.fileSystem.WriteAllText(badPackPath1, badContents);
            this.fileSystem.WriteAllText(badPackPath2, badContents);
            badPackPath1.ShouldBeAFile(this.fileSystem).WithContents(badContents);
            badPackPath2.ShouldBeAFile(this.fileSystem).WithContents(badContents);

            // fetch should delete the bad packs on Windows and Mac, and
            // ignore them on Linux due to the different case filenames
            this.Enlistment.RunVerb("fetch");

            if (FileSystemHelpers.CaseSensitiveFileSystem)
            {
                badPackPath1.ShouldBeAFile(this.fileSystem).WithContents(badContents);
                badPackPath2.ShouldBeAFile(this.fileSystem).WithContents(badContents);
            }
            else
            {
                badPackPath1.ShouldNotExistOnDisk(this.fileSystem);
                badPackPath2.ShouldNotExistOnDisk(this.fileSystem);
            }

            // All of the original prefetch packs should still be present
            string[] newPrefetchPacks = this.ReadPrefetchPackFileNames();
            newPrefetchPacks.ShouldContain(prefetchPacks, (item, expectedValue) => { return string.Equals(item, expectedValue); });
            this.AllPrefetchPacksShouldHaveIdx(newPrefetchPacks);
            this.TempPackRoot.ShouldBeADirectory(this.fileSystem).WithNoItems();
        }

        // WindowsOnly because the test depends on Windows-specific file sharing behavior
        [TestCase, Order(5)]
        [Category(Categories.WindowsOnly)]
        public void FetchStepFailsWhenItCannotRemoveABadPrefetchPack()
        {
            this.Enlistment.Unregister();

            string[] prefetchPacks = this.ReadPrefetchPackFileNames();
            long mostRecentPackTimestamp = this.GetMostRecentPackTimestamp(prefetchPacks);

            // Create a bad pack that is newer than the most recent pack
            string badContents = "BADPACK";
            string badPackPath = Path.Combine(this.PackRoot, $"{PrefetchPackPrefix}-{mostRecentPackTimestamp + 1}-{Guid.NewGuid().ToString("N")}.pack");
            this.fileSystem.WriteAllText(badPackPath, badContents);
            badPackPath.ShouldBeAFile(this.fileSystem).WithContents(badContents);

            // Open a handle to the bad pack that will prevent fetch-commits-and-trees from being able to delete it
            using (FileStream stream = new FileStream(badPackPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                this.Enlistment.RunVerb("fetch", failOnError: false);
            }

            badPackPath.ShouldBeAFile(this.fileSystem).WithContents(badContents);

            // After handle is closed fetching commits and trees should succeed
            this.Enlistment.RunVerb("fetch");

            badPackPath.ShouldNotExistOnDisk(this.fileSystem);

            string[] newPrefetchPacks = this.ReadPrefetchPackFileNames();
            newPrefetchPacks.ShouldContain(prefetchPacks, (item, expectedValue) => { return string.Equals(item, expectedValue); });
            this.AllPrefetchPacksShouldHaveIdx(newPrefetchPacks);
            this.TempPackRoot.ShouldBeADirectory(this.fileSystem).WithNoItems();
        }

        [TestCase, Order(6)]
        public void FetchStepCleansUpBadKeepFiles()
        {
            string[] prefetchPacks = this.ReadPrefetchPackFileNames();
            long mostRecentPackTimestamp = this.GetMostRecentPackTimestamp(prefetchPacks);

            // Create a bad keep file that is newer than the most recent pack
            string badContents = "BADKEEP";
            string badKeepPath = Path.Combine(this.PackRoot, $"{PrefetchPackPrefix}-{mostRecentPackTimestamp + 1}-{Guid.NewGuid().ToString("N")}.keep");
            this.fileSystem.WriteAllText(badKeepPath, badContents);
            badKeepPath.ShouldBeAFile(this.fileSystem).WithContents(badContents);

            // fetch should delete the bad keep file
            this.Enlistment.RunVerb("fetch");

            badKeepPath.ShouldNotExistOnDisk(this.fileSystem);

            // All of the original prefetch packs should still be present
            string[] newPrefetchPacks = this.ReadPrefetchPackFileNames();
            newPrefetchPacks.ShouldContain(prefetchPacks, (item, expectedValue) => { return string.Equals(item, expectedValue); });
            this.AllPrefetchPacksShouldHaveIdx(newPrefetchPacks);
            this.TempPackRoot.ShouldBeADirectory(this.fileSystem).WithNoItems();

            // There should be only a single keep file for the newest pack
            string[] keepFiles = this.ReadPrefetchKeepFileNames();
            keepFiles.Length.ShouldEqual(1, "Incorrect number of .keep files in pack directory");
            this.GetTimestamp(keepFiles[0]).ShouldEqual(mostRecentPackTimestamp, "Incorrect timestamp of .keep file in pack directory");
            Path.ChangeExtension(keepFiles[0], ".pack").ShouldBeAFile(this.fileSystem);
        }

        [TestCase, Order(7)]
        public void FetchStepCleansUpOrIgnoresBadKeepFilesWithCaseDifferingNames()
        {
            string[] prefetchPacks = this.ReadPrefetchPackFileNames();
            long mostRecentPackTimestamp = this.GetMostRecentPackTimestamp(prefetchPacks);

            // Create two bad keep files that are newer than the most recent pack
            // and have uppercase versions of the filename prefix and extension
            string badContents = "BADKEEP";
            string badKeepPath1 = Path.Combine(this.PackRoot, $"{PrefetchPackPrefix}-{mostRecentPackTimestamp + 1}-{Guid.NewGuid().ToString("N")}.KEEP");
            string badKeepPath2 = Path.Combine(this.PackRoot, $"{PrefetchPackPrefix.ToUpper()}-{mostRecentPackTimestamp + 2}-{Guid.NewGuid().ToString("N")}.keep");
            this.fileSystem.WriteAllText(badKeepPath1, badContents);
            this.fileSystem.WriteAllText(badKeepPath2, badContents);
            badKeepPath1.ShouldBeAFile(this.fileSystem).WithContents(badContents);
            badKeepPath2.ShouldBeAFile(this.fileSystem).WithContents(badContents);

            // fetch should delete the bad keep files on Windows and Mac, and
            // ignore the them on Linux due to the different case filenames
            this.Enlistment.RunVerb("fetch");

            if (FileSystemHelpers.CaseSensitiveFileSystem)
            {
                badKeepPath1.ShouldBeAFile(this.fileSystem).WithContents(badContents);
                badKeepPath2.ShouldBeAFile(this.fileSystem).WithContents(badContents);
            }
            else
            {
                badKeepPath1.ShouldNotExistOnDisk(this.fileSystem);
                badKeepPath2.ShouldNotExistOnDisk(this.fileSystem);
            }

            // All of the original prefetch packs should still be present
            string[] newPrefetchPacks = this.ReadPrefetchPackFileNames();
            newPrefetchPacks.ShouldContain(prefetchPacks, (item, expectedValue) => { return string.Equals(item, expectedValue); });
            this.AllPrefetchPacksShouldHaveIdx(newPrefetchPacks);
            this.TempPackRoot.ShouldBeADirectory(this.fileSystem).WithNoItems();

            // There should be only a single keep file for the newest pack
            string[] keepFiles = this.ReadPrefetchKeepFileNames();
            keepFiles.Length.ShouldEqual(1, "Incorrect number of .keep files in pack directory");
            this.GetTimestamp(keepFiles[0]).ShouldEqual(mostRecentPackTimestamp, "Incorrect timestamp of .keep file in pack directory");
            Path.ChangeExtension(keepFiles[0], ".pack").ShouldBeAFile(this.fileSystem);
        }

        [TestCase, Order(8)]
        public void FetchCommitsAndTreesCleansUpStaleTempPrefetchPacks()
        {
            this.Enlistment.Unregister();

            // Create stale packs and idxs  in the temp folder
            string stalePackContents = "StalePack";
            string stalePackPath = Path.Combine(this.TempPackRoot, $"{PrefetchPackPrefix}-123456-{Guid.NewGuid().ToString("N")}.pack");
            this.fileSystem.WriteAllText(stalePackPath, stalePackContents);
            stalePackPath.ShouldBeAFile(this.fileSystem).WithContents(stalePackContents);

            string staleIdxContents = "StaleIdx";
            string staleIdxPath = Path.ChangeExtension(stalePackPath, ".idx");
            this.fileSystem.WriteAllText(staleIdxPath, staleIdxContents);
            staleIdxPath.ShouldBeAFile(this.fileSystem).WithContents(staleIdxContents);

            string stalePackPath2 = Path.Combine(this.TempPackRoot, $"{PrefetchPackPrefix}-123457-{Guid.NewGuid().ToString("N")}.pack");
            this.fileSystem.WriteAllText(stalePackPath2, stalePackContents);
            stalePackPath2.ShouldBeAFile(this.fileSystem).WithContents(stalePackContents);

            string stalePack2TempIdx = Path.ChangeExtension(stalePackPath2, ".tempidx");
            this.fileSystem.WriteAllText(stalePack2TempIdx, staleIdxContents);
            stalePack2TempIdx.ShouldBeAFile(this.fileSystem).WithContents(staleIdxContents);

            // Create other unrelated file in the temp folder
            string otherFileContents = "Test file, don't delete me!";
            string otherFilePath = Path.Combine(this.TempPackRoot, "ReadmeAndDontDeleteMe.txt");
            this.fileSystem.WriteAllText(otherFilePath, otherFileContents);
            otherFilePath.ShouldBeAFile(this.fileSystem).WithContents(otherFileContents);

            this.Enlistment.RunVerb("fetch");

            // Validate stale prefetch packs are cleaned up
            Directory.GetFiles(this.TempPackRoot, $"{PrefetchPackPrefix}*.pack").ShouldBeEmpty("There should be no .pack files in the tempPack folder");
            Directory.GetFiles(this.TempPackRoot, $"{PrefetchPackPrefix}*.idx").ShouldBeEmpty("There should be no .idx files in the tempPack folder");
            Directory.GetFiles(this.TempPackRoot, $"{PrefetchPackPrefix}*.tempidx").ShouldBeEmpty("There should be no .tempidx files in the tempPack folder");

            // Validate other files are not impacted
            otherFilePath.ShouldBeAFile(this.fileSystem).WithContents(otherFileContents);
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

        private string[] ReadPrefetchKeepFileNames()
        {
            return Directory.GetFiles(this.PackRoot, $"{PrefetchPackPrefix}*.keep");
        }

        private long GetTimestamp(string preFetchPackName)
        {
            string filename = Path.GetFileName(preFetchPackName);

            // StringComparison.Ordinal because packs should be created using the case we expect
            filename.StartsWith(PrefetchPackPrefix, StringComparison.Ordinal)
                .ShouldBeTrue($"'{preFetchPackName}' does not start with '{PrefetchPackPrefix}'");

            string[] parts = filename.Split('-');
            long parsed;

            parts.Length.ShouldBeAtLeast(1, $"'{preFetchPackName}' has less parts ({parts.Length}) than expected (1)");
            long.TryParse(parts[1], out parsed).ShouldBeTrue($"Failed to parse long from '{parts[1]}'");
            return parsed;
        }

        private long GetMostRecentPackTimestamp(string[] prefetchPacks)
        {
            prefetchPacks.Length.ShouldBeAtLeast(1, "prefetchPacks should have at least one item");

            long mostRecentPackTimestamp = -1;
            foreach (string prefetchPack in prefetchPacks)
            {
                long timestamp = this.GetTimestamp(prefetchPack);
                if (timestamp > mostRecentPackTimestamp)
                {
                    mostRecentPackTimestamp = timestamp;
                }
            }

            mostRecentPackTimestamp.ShouldBeAtLeast(1, "Failed to find the most recent pack");
            return mostRecentPackTimestamp;
        }
    }
}
