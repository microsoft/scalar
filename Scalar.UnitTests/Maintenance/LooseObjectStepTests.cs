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
    public class LooseObjectStepTests
    {
        private MockTracer tracer;
        private MockGitProcess gitProcess;
        private ScalarContext context;

        private string MaintenanceCommand => $"-c pack.window=0 -c pack.depth=0 maintenance run --task=loose-objects";

        [TestCase]
        public void LooseObjectsIgnoreTimeRestriction()
        {
            this.TestSetup(DateTime.UtcNow);

            LooseObjectsStep step = new LooseObjectsStep(this.context, requireCacheLock: false, forceRun: true);
            step.Execute();

            this.tracer.StartActivityTracer.RelatedErrorEvents.Count.ShouldEqual(0);
            this.tracer.StartActivityTracer.RelatedWarningEvents.Count.ShouldEqual(0);
            List<string> commands = this.gitProcess.CommandsRun;
            commands.Count.ShouldEqual(1);
            commands[0].ShouldEqual(MaintenanceCommand);
        }

        [TestCase]
        public void LooseObjectsFailTimeRestriction()
        {
            this.TestSetup(DateTime.UtcNow);

            LooseObjectsStep step = new LooseObjectsStep(this.context, requireCacheLock: false, forceRun: false);
            step.Execute();

            this.tracer.StartActivityTracer.RelatedErrorEvents.Count.ShouldEqual(0);
            this.tracer.StartActivityTracer.RelatedWarningEvents.Count.ShouldEqual(1);
            List<string> commands = this.gitProcess.CommandsRun;
            commands.Count.ShouldEqual(0);
        }

        [TestCase]
        public void LooseObjectsPassTimeRestriction()
        {
            this.TestSetup(DateTime.UtcNow.AddDays(-7));

            Mock<GitProcessChecker> mockChecker = new Mock<GitProcessChecker>();
            mockChecker.Setup(checker => checker.GetRunningGitProcessIds())
                       .Returns(Array.Empty<int>());

            LooseObjectsStep step = new LooseObjectsStep(
                                            this.context,
                                            requireCacheLock: false,
                                            forceRun: false,
                                            gitProcessChecker: mockChecker.Object);
            step.Execute();

            mockChecker.Verify(checker => checker.GetRunningGitProcessIds(), Times.Once());

            this.tracer.StartActivityTracer.RelatedErrorEvents.Count.ShouldEqual(0);
            this.tracer.StartActivityTracer.RelatedWarningEvents.Count.ShouldEqual(0);
            List<string> commands = this.gitProcess.CommandsRun;
            commands.Count.ShouldEqual(1);
            commands[0].ShouldEqual(MaintenanceCommand);
        }

        [TestCase]
        public void LooseObjectsFailGitProcessIds()
        {
            this.TestSetup(DateTime.UtcNow.AddDays(-7));

            Mock<GitProcessChecker> mockChecker = new Mock<GitProcessChecker>();
            mockChecker.Setup(checker => checker.GetRunningGitProcessIds())
                       .Returns(new int[] { 1 });

            LooseObjectsStep step = new LooseObjectsStep(
                                            this.context,
                                            requireCacheLock: false,
                                            forceRun: false,
                                            gitProcessChecker: mockChecker.Object);
            step.Execute();

            mockChecker.Verify(checker => checker.GetRunningGitProcessIds(), Times.Once());

            this.tracer.StartActivityTracer.RelatedErrorEvents.Count.ShouldEqual(0);
            this.tracer.StartActivityTracer.RelatedWarningEvents.Count.ShouldEqual(1);
            List<string> commands = this.gitProcess.CommandsRun;
            commands.Count.ShouldEqual(0);
        }

        private void TestSetup(DateTime lastRun)
        {
            string lastRunTime = EpochConverter.ToUnixEpochSeconds(lastRun).ToString();

            // Create GitProcess
            this.gitProcess = new MockGitProcess();

            // Create enlistment using git process
            ScalarEnlistment enlistment = new MockScalarEnlistment(this.gitProcess);

            // Create a last run time file
            MockFile timeFile = new MockFile(Path.Combine(enlistment.GitObjectsRoot, "info", LooseObjectsStep.LooseObjectsLastRunFileName), lastRunTime);

            // Create info directory to hold last run time file
            MockDirectory infoRoot = new MockDirectory(Path.Combine(enlistment.GitObjectsRoot, "info"), null, new List<MockFile>() { timeFile });

            MockDirectory pack = new MockDirectory(
                enlistment.GitPackRoot,
                null,
                new List<MockFile>());

            // Create git objects directory
            MockDirectory gitObjectsRoot = new MockDirectory(enlistment.GitObjectsRoot, new List<MockDirectory>() { infoRoot, pack }, null);

            // Add object directory to file System
            List<MockDirectory> directories = new List<MockDirectory>() { gitObjectsRoot };
            PhysicalFileSystem fileSystem = new MockFileSystem(new MockDirectory(enlistment.EnlistmentRoot, directories, null));

            // Create and return Context
            this.tracer = new MockTracer();
            this.context = new ScalarContext(this.tracer, fileSystem, enlistment: enlistment);

            this.gitProcess.SetExpectedCommandResult(
                MaintenanceCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));
        }
    }
}
