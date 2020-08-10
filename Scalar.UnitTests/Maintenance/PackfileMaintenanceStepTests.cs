using Moq;
using NUnit.Framework;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Maintenance;
using Scalar.Tests.Should;
using Scalar.UnitTests.Mock.Common;
using Scalar.UnitTests.Mock.FileSystem;
using Scalar.UnitTests.Mock.Git;
using System;
using System.Collections.Generic;
using System.IO;

namespace Scalar.UnitTests.Maintenance
{
    [TestFixture]
    public class PackfileMaintenanceStepTests
    {
        private const string StaleIdxName = "pack-stale.idx";
        private const string KeepName = "pack-3.keep";
        private MockTracer tracer;
        private MockGitProcess gitProcess;
        private ScalarContext context;
        private string MaintenanceCommand => $"-c core.multiPackIndex=true -c pack.threads=1 -c repack.packKeptObjects=true maintenance run --task=incremental-repack";

        [TestCase]
        public void PackfileMaintenanceIgnoreTimeRestriction()
        {
            this.TestSetup(DateTime.UtcNow);

            PackfileMaintenanceStep step = new PackfileMaintenanceStep(this.context, requireObjectCacheLock: false, forceRun: true);
            step.Execute();

            this.tracer.StartActivityTracer.RelatedErrorEvents.Count.ShouldEqual(0);
            this.tracer.StartActivityTracer.RelatedWarningEvents.Count.ShouldEqual(0);
            List<string> commands = this.gitProcess.CommandsRun;
            commands.Count.ShouldEqual(1);
            commands[0].ShouldEqual(this.MaintenanceCommand);
        }

        [TestCase]
        public void PackfileMaintenanceFailTimeRestriction()
        {
            this.TestSetup(DateTime.UtcNow);

            PackfileMaintenanceStep step = new PackfileMaintenanceStep(this.context, requireObjectCacheLock: false, forceRun: false);
            step.Execute();

            this.tracer.StartActivityTracer.RelatedErrorEvents.Count.ShouldEqual(0);
            this.tracer.StartActivityTracer.RelatedWarningEvents.Count.ShouldEqual(1);
            List<string> commands = this.gitProcess.CommandsRun;
            commands.Count.ShouldEqual(0);
        }

        [TestCase]
        public void PackfileMaintenancePassTimeRestriction()
        {
            this.TestSetup(DateTime.UtcNow.AddDays(-1));

            Mock<GitProcessChecker> mockChecker = new Mock<GitProcessChecker>();
            mockChecker.Setup(checker => checker.GetRunningGitProcessIds())
                       .Returns(Array.Empty<int>());

            PackfileMaintenanceStep step = new PackfileMaintenanceStep(
                                                    this.context,
                                                    requireObjectCacheLock: false,
                                                    forceRun: false,
                                                    gitProcessChecker: mockChecker.Object);

            step.Execute();

            mockChecker.Verify(checker => checker.GetRunningGitProcessIds(), Times.Once());

            this.tracer.StartActivityTracer.RelatedErrorEvents.Count.ShouldEqual(0);
            this.tracer.StartActivityTracer.RelatedWarningEvents.Count.ShouldEqual(0);
            List<string> commands = this.gitProcess.CommandsRun;
            commands.Count.ShouldEqual(1);
            commands[0].ShouldEqual(this.MaintenanceCommand);
        }

        [TestCase]
        public void PackfileMaintenanceFailGitProcessIds()
        {
            this.TestSetup(DateTime.UtcNow.AddDays(-1));

            Mock<GitProcessChecker> mockChecker = new Mock<GitProcessChecker>();
            mockChecker.Setup(checker => checker.GetRunningGitProcessIds())
                       .Returns(new int[] { 1 });

            PackfileMaintenanceStep step = new PackfileMaintenanceStep(
                                                    this.context,
                                                    requireObjectCacheLock: false,
                                                    forceRun: false,
                                                    gitProcessChecker: mockChecker.Object);

            step.Execute();

            mockChecker.Verify(checker => checker.GetRunningGitProcessIds(), Times.Once());

            this.tracer.StartActivityTracer.RelatedErrorEvents.Count.ShouldEqual(0);
            this.tracer.StartActivityTracer.RelatedWarningEvents.Count.ShouldEqual(1);
            List<string> commands = this.gitProcess.CommandsRun;
            commands.Count.ShouldEqual(0);
        }

        [TestCase]
        public void CountPackFiles()
        {
            this.TestSetup(DateTime.UtcNow);

            PackfileMaintenanceStep step = new PackfileMaintenanceStep(this.context, requireObjectCacheLock: false, forceRun: true);

            step.GetPackFilesInfo(out int count, out long size, out long secondSmallestSize, out bool hasKeep);
            count.ShouldEqual(3);
            size.ShouldEqual(11);
            secondSmallestSize.ShouldEqual(3);
            hasKeep.ShouldEqual(true);

            this.context.FileSystem.DeleteFile(Path.Combine(this.context.Enlistment.GitPackRoot, KeepName));

            step.GetPackFilesInfo(out count, out size, out secondSmallestSize, out hasKeep);
            count.ShouldEqual(3);
            size.ShouldEqual(11);
            secondSmallestSize.ShouldEqual(3);
            hasKeep.ShouldEqual(false);
        }

        [TestCase]
        public void CleanStaleIdxFiles()
        {
            this.TestSetup(DateTime.UtcNow);

            PackfileMaintenanceStep step = new PackfileMaintenanceStep(this.context, requireObjectCacheLock: false, forceRun: true);

            List<string> staleIdx = step.CleanStaleIdxFiles(out int numDeletionBlocked);

            staleIdx.Count.ShouldEqual(1);
            staleIdx[0].ShouldEqual(StaleIdxName);

            this.context
                .FileSystem
                .FileExists(Path.Combine(this.context.Enlistment.GitPackRoot, StaleIdxName))
                .ShouldBeFalse();
        }

        private void TestSetup(DateTime lastRun, bool failOnVerify = false)
        {
            string lastRunTime = EpochConverter.ToUnixEpochSeconds(lastRun).ToString();

            this.gitProcess = new MockGitProcess();

            // Create enlistment using git process
            ScalarEnlistment enlistment = new MockScalarEnlistment(this.gitProcess);

            // Create a last run time file
            MockFile timeFile = new MockFile(Path.Combine(enlistment.GitObjectsRoot, "info", PackfileMaintenanceStep.PackfileLastRunFileName), lastRunTime);

            // Create info directory to hold last run time file
            MockDirectory info = new MockDirectory(
                Path.Combine(enlistment.GitObjectsRoot, "info"),
                null,
                new List<MockFile>() { timeFile });

            // Create pack info
            MockDirectory pack = new MockDirectory(
                enlistment.GitPackRoot,
                null,
                new List<MockFile>()
                {
                    new MockFile(Path.Combine(enlistment.GitPackRoot, "pack-1.pack"), "one"),
                    new MockFile(Path.Combine(enlistment.GitPackRoot, "pack-1.idx"), "1"),
                    new MockFile(Path.Combine(enlistment.GitPackRoot, "pack-2.pack"), "two"),
                    new MockFile(Path.Combine(enlistment.GitPackRoot, "pack-2.idx"), "2"),
                    new MockFile(Path.Combine(enlistment.GitPackRoot, "pack-3.pack"), "three"),
                    new MockFile(Path.Combine(enlistment.GitPackRoot, "pack-3.idx"), "3"),
                    new MockFile(Path.Combine(enlistment.GitPackRoot, KeepName), string.Empty),
                    new MockFile(Path.Combine(enlistment.GitPackRoot, StaleIdxName), "4"),
                });

            // Create git objects directory
            MockDirectory gitObjectsRoot = new MockDirectory(enlistment.GitObjectsRoot, new List<MockDirectory>() { info, pack }, null);

            // Add object directory to file System
            List<MockDirectory> directories = new List<MockDirectory>() { gitObjectsRoot };
            PhysicalFileSystem fileSystem = new MockFileSystem(new MockDirectory(enlistment.EnlistmentRoot, directories, null));

            // Create and return Context
            this.tracer = new MockTracer();
            this.context = new ScalarContext(this.tracer, fileSystem, enlistment);

            this.gitProcess.SetExpectedCommandResult(
                this.MaintenanceCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));
        }
    }
}
