using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Properties;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Scalar.FunctionalTests.Tests.GitRepoPerFixture
{
    [Category(Categories.GitRepository)]
    public class RunVerbTests : TestsWithGitRepoPerFixture
    {
        private FileSystemRunner fileSystem;

        private string GitObjectRoot => Path.Combine(this.Enlistment.RepoRoot, ".git", "objects");
        private string CommitGraphChain => Path.Combine(this.GitObjectRoot, "info", "commit-graphs", "commit-graph-chain");
        private string PackRoot => Path.Combine(this.Enlistment.RepoRoot, ".git", "objects", "pack");

        public RunVerbTests()
        {
            this.fileSystem = new SystemIORunner();
        }

        [SetUp]
        public void Setup()
        {
            this.Enlistment.Unregister();
        }

        [TestCase]
        [Order(1)]
        public void CommitGraphStep()
        {
            this.fileSystem.FileExists(CommitGraphChain).ShouldBeFalse();
            this.Enlistment.RunVerb("commit-graph");
            this.fileSystem.FileExists(CommitGraphChain).ShouldBeTrue();
        }

        [TestCase]
        [Order(2)]
        public void PackfileMaintenanceStep()
        {
            this.GetPackSizes(out int packCount, out long maxSize, out long minSize, out long totalSize);
            minSize.ShouldNotEqual(0, "min size means empty pack-file?");

            GitProcess.InvokeProcess(
                this.Enlistment.RepoRoot,
                $"repack -adf --max-pack-size={totalSize / 4}");

            this.GetPackSizes(out int countAfterRepack, out maxSize, out minSize, out totalSize);
            minSize.ShouldNotEqual(0, "min size means empty pack-file?");

            this.Enlistment
                .RunVerb("pack-files", batchSize: totalSize - minSize + 1)
                .ShouldNotContain(false, "Skipping pack maintenance due to no .keep file.");

            this.GetPackSizes(out int countAfterStep, out maxSize, out minSize, out totalSize);
            minSize.ShouldNotEqual(0, "min size means empty pack-file?");

            // The new batch logic in Git depends on the number of pack-files
            // trying to pack everything except the biggest pack-file. If
            // there are only two packs, then no work is done.
            int expectAfterRerun;
            if (countAfterRepack == 2)
            {
                countAfterStep.ShouldEqual(countAfterRepack, nameof(countAfterStep));
                expectAfterRerun = countAfterRepack;
            }
            else
            {
                countAfterStep.ShouldEqual(countAfterRepack + 1, nameof(countAfterStep));
                expectAfterRerun = 1;
            }

            this.Enlistment
                .RunVerb("pack-files", batchSize: totalSize - minSize + 1)
                .ShouldNotContain(false, "Skipping pack maintenance due to no .keep file.");

            this.GetPackSizes(out int countAfterStep2, out maxSize, out minSize, out totalSize);
            minSize.ShouldNotEqual(0, "min size means empty pack-file?");
            countAfterStep2.ShouldEqual(expectAfterRerun, nameof(countAfterStep2));
        }

        [TestCase]
        [Order(3)]
        public void LooseObjectsStep()
        {
            // Create loose objects using a Git command:
            GitProcess.Invoke(this.Enlistment.RepoRoot, "commit -mtest --allow-empty");

            this.GetLooseObjectFiles().Count.ShouldBeAtLeast(1);
            this.GetPackSizes(out int countBeforeStep, out _, out long minSize, out _);
            minSize.ShouldNotEqual(0, "min size means empty pack-file?");

            // This step packs the loose object into a pack.
            this.Enlistment.RunVerb("loose-objects");
            this.GetPackSizes(out int countAfterStep1, out _, out minSize, out _);
            minSize.ShouldNotEqual(0, "min size means empty pack-file?");
            this.GetLooseObjectFiles().Count.ShouldBeAtLeast(1);
            countAfterStep1.ShouldEqual(countBeforeStep + 1, "First step should create a pack");

            // This step deletes the loose object that is already in a pack
            this.Enlistment.RunVerb("loose-objects");
            this.GetPackSizes(out int countAfterStep2, out _, out minSize, out _);
            minSize.ShouldNotEqual(0, "min size means empty pack-file?");
            this.GetLooseObjectFiles().Count.ShouldEqual(0);
            countAfterStep2.ShouldEqual(countAfterStep1, "Second step should not create a pack");
        }

        [TestCase]
        [Order(4)]
        public void FetchStep()
        {
            string refsRoot = Path.Combine(this.Enlistment.RepoRoot, ".git", "refs");
            string refsHeads = Path.Combine(refsRoot, "heads");
            string refsRemotesOrigin = Path.Combine(refsRoot, "remotes", "origin");
            string refsPrefetch = Path.Combine(refsRoot, "prefetch");
            string refsPrefetchOriginFake = Path.Combine(refsPrefetch, "origin", "fake");
            string packedRefs = Path.Combine(this.Enlistment.RepoRoot, ".git", "packed-refs");

            // Removing refs makes the next fetch need to download a new pack
            this.fileSystem.DeleteDirectory(refsHeads);
            this.fileSystem.DeleteDirectory(refsRemotesOrigin);
            this.fileSystem.DeleteDirectory(this.PackRoot);
            this.fileSystem.CreateDirectory(this.PackRoot);
            if (this.fileSystem.FileExists(packedRefs))
            {
                this.fileSystem.DeleteFile(packedRefs);
            }

            this.Enlistment.RunVerb("fetch");

            this.GetPackSizes(out int countAfterFetch, out _, out _, out _);

            countAfterFetch.ShouldEqual(1, "fetch should download one pack");

            this.fileSystem.DirectoryExists(refsPrefetch).ShouldBeTrue("background fetch should have created refs/prefetch/*");
            this.fileSystem.DirectoryExists(refsHeads).ShouldBeFalse("background fetch should not have created refs/heads/*");
            this.fileSystem.DirectoryExists(refsRemotesOrigin).ShouldBeFalse("background fetch should not have created refs/remotes/origin/*");

            // This is the SHA-1 for the main branch
            string sha1 = Settings.Default.CommitId;
            this.fileSystem.WriteAllText(refsPrefetchOriginFake, sha1);

            this.Enlistment.RunVerb("fetch");

            this.fileSystem.DirectoryExists(refsHeads).ShouldBeFalse("background fetch should not have created refs/heads/*");
            this.fileSystem.DirectoryExists(refsRemotesOrigin).ShouldBeFalse("background fetch should not have created refs/remotes/origin/*");

            this.fileSystem.FileExists(refsPrefetchOriginFake).ShouldBeFalse("background fetch should clear deleted refs from refs/prefetch");

            this.GetPackSizes(out int countAfterFetch2, out _, out _, out _);
            countAfterFetch2.ShouldEqual(1, "sceond fetch should not download a pack");
        }

        [TestCase]
        [Order(5)]
        public void AllSteps()
        {
            this.Enlistment.RunVerb("all");
        }

        [TestCase]
        [Order(6)]
        [Category(Categories.WindowsOnly)]
        public void ServiceLogFallback()
        {
            string logsRoot = Path.Combine(this.Enlistment.RepoRoot, ".git", "logs");
            string serviceLogFile = Path.Combine(logsRoot, "scalar_maintenance_service.log");

            using (Stream stream = File.OpenWrite(serviceLogFile))
            {
                int numLogsBefore = Directory.GetFiles(logsRoot, "*.log").Length;

                this.Enlistment.RunVerb("config", asService: true);

                int numLogsAfter = Directory.GetFiles(logsRoot, "*.log").Length;

                numLogsAfter.ShouldEqual(numLogsBefore + 1, $"`scalar run config` should have created new log file");
            }
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
        private List<string> GetLooseObjectFiles()
        {
            List<string> looseObjectFiles = new List<string>();
            foreach (string directory in Directory.GetDirectories(this.GitObjectRoot))
            {
                // Check if the directory is 2 letter HEX
                if (Regex.IsMatch(directory, @"[/\\][0-9a-fA-F]{2}$"))
                {
                    string[] files = Directory.GetFiles(directory);
                    looseObjectFiles.AddRange(files);
                }
            }

            return looseObjectFiles;
        }
    }
}
