using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Properties;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System;
using System.IO;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixtureSource(typeof(TestsWithEnlistmentPerFixture), nameof(TestsWithEnlistmentPerFixture.MaintenanceMode))]
    [Category(Categories.Maintenance)]
    public class CommitGraphStepTests : TestsWithEnlistmentPerFixture
    {
        private FileSystemRunner fileSystem;
        private Settings.MaintenanceMode maintenanceMode;

        // Set forcePerRepoObjectCache to true to avoid any of the tests inadvertently corrupting
        // the cache
        public CommitGraphStepTests(Settings.MaintenanceMode maintenanceMode)
            : base(forcePerRepoObjectCache: true, fullClone: false)
        {
            this.fileSystem = new SystemIORunner();
            this.maintenanceMode = maintenanceMode;
        }

        private string GitObjectRoot => ScalarHelpers.GetObjectsRootFromGitConfig(this.Enlistment.RepoRoot);
        private string CommitGraphsRoot => Path.Combine(this.GitObjectRoot, "info", "commit-graphs");
        private string CommitGraphsChain => Path.Combine(this.CommitGraphsRoot, "commit-graph-chain");

        [TestCase, Order(1)]
        public void CreateCommitGraphWhenMissing()
        {
            RepositoryHelpers.DeleteTestDirectory(this.CommitGraphsRoot);

            this.RunCommitGraphTask();

            this.fileSystem
                .FileExists(this.CommitGraphsChain)
                .ShouldBeTrue($"{this.CommitGraphsChain} does not exist");

            string chain = this.fileSystem.ReadAllText(this.CommitGraphsChain);

            string[] lines = chain.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            lines.Length.ShouldEqual(1, $"Too many lines in '{chain}'");

            string graphFileName = Path.Combine(this.CommitGraphsRoot, $"graph-{lines[0]}.graph");
            this.fileSystem
                .FileExists(graphFileName)
                .ShouldBeTrue($"{graphFileName} does not exist");
        }

        [TestCase, Order(2)]
        public void CleansUpOphanedLockFiles()
        {
            string graphLockPath = this.CommitGraphsChain + ".lock";

            if (!this.fileSystem.DirectoryExists(this.CommitGraphsRoot))
            {
                this.fileSystem.CreateDirectory(this.CommitGraphsRoot);
            }

            this.fileSystem.CreateEmptyFile(graphLockPath);

            this.RunCommitGraphTask();

            this.fileSystem.FileExists(graphLockPath)
                           .ShouldEqual(this.maintenanceMode == Settings.MaintenanceMode.Git, nameof(graphLockPath));
            this.fileSystem.FileExists(this.CommitGraphsChain).ShouldBeTrue(nameof(this.CommitGraphsChain));
        }

        private void RunCommitGraphTask()
        {
            if (this.maintenanceMode == Settings.MaintenanceMode.Scalar)
            {
                this.Enlistment.RunVerb("commit-graph");
            }
            else if (this.maintenanceMode == Settings.MaintenanceMode.Git)
            {
                this.Enlistment.RunMaintenanceTask("commit-graph");
            }
        }
    }
}
