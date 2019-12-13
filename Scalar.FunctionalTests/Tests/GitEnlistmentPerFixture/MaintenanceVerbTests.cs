using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Scalar.FunctionalTests.Tests.GitEnlistmentPerFixture
{
    [Category(Categories.GitRepository)]
    public class MaintenanceVerbTests : TestsWithGitEnlistmentPerFixture
    {
        private FileSystemRunner fileSystem;
        private string GitObjectRoot => Path.Combine(this.Enlistment.RepoRoot, ".git", "objects");
        private string PackRoot => Path.Combine(this.Enlistment.RepoRoot, ".git", "objects", "pack");

        public MaintenanceVerbTests()
        {
            this.fileSystem = new SystemIORunner();
        }

        [TestCase]
        [Order(1)]
        public void CommitGraphStep()
        {
            this.fileSystem.FileExists(Path.Combine(this.Enlistment.RepoRoot, ".git", "objects", "info", "commit-graphs", "commit-graph-chain"))
                           .ShouldBeFalse();
            this.Enlistment.CommitGraphStep();
            this.fileSystem.FileExists(Path.Combine(this.Enlistment.RepoRoot, ".git", "objects", "info", "commit-graphs", "commit-graph-chain"))
                           .ShouldBeTrue();
        }

        [TestCase]
        [Order(2)]
        public void PackfileMaintenanceStep()
        {
            this.GetPackSizes(out int packCount, out long maxSize, out long minSize, out long totalSize);
            GitProcess.InvokeProcess(
                this.Enlistment.RepoRoot,
                $"repack -adf --max-pack-size={totalSize / 4}");

            this.GetPackSizes(out int countAfterRepack, out maxSize, out minSize, out totalSize);

            this.Enlistment
                .PackfileMaintenanceStep(batchSize: totalSize - minSize + 1)
                .ShouldNotContain(false, "Skipping pack maintenance due to no .keep file.");

            this.GetPackSizes(out int countAfterStep, out maxSize, out minSize, out totalSize);

            countAfterStep.ShouldEqual(countAfterRepack + 1, nameof(countAfterStep));

            this.Enlistment
                .PackfileMaintenanceStep(batchSize: totalSize - minSize + 1)
                .ShouldNotContain(false, "Skipping pack maintenance due to no .keep file.");

            this.GetPackSizes(out int countAfterStep2, out maxSize, out minSize, out totalSize);
            countAfterStep2.ShouldEqual(1, nameof(countAfterStep2));
        }

        [TestCase]
        [Order(3)]
        public void LooseObjectsStep()
        {
            // Create loose objects using a Git command:
            GitProcess.Invoke(this.Enlistment.RepoRoot, "commit -mtest --allow-empty");

            this.GetLooseObjectFiles().Count.ShouldBeAtLeast(1);

            // This step packs the loose object into a pack.
            this.Enlistment.LooseObjectStep();
            this.GetLooseObjectFiles().Count.ShouldBeAtLeast(1);

            // This step deletes the loose object that is already in a pack
            this.Enlistment.LooseObjectStep();
            this.GetLooseObjectFiles().Count.ShouldEqual(0);
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
