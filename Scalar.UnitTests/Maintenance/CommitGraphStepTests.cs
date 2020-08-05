using NUnit.Framework;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Maintenance;
using Scalar.Tests.Should;
using Scalar.UnitTests.Mock.Common;
using Scalar.UnitTests.Mock.FileSystem;
using Scalar.UnitTests.Mock.Git;
using System.Collections.Generic;

namespace Scalar.UnitTests.Maintenance
{
    [TestFixture]
    public class CommitGraphStepTests
    {
        private MockTracer tracer;
        private MockGitProcess gitProcess;
        private ScalarContext context;

        private string MaintenanceCommand => $"maintenance run --task=commit-graph";

        [TestCase]
        public void WriteGraphWithPacks()
        {
            this.TestSetup();

            this.gitProcess.SetExpectedCommandResult(
                this.MaintenanceCommand,
                () => new GitProcess.Result(string.Empty, string.Empty, GitProcess.Result.SuccessCode));

            CommitGraphStep step = new CommitGraphStep(this.context, requireObjectCacheLock: false);
            step.Execute();

            this.tracer.RelatedInfoEvents.Count.ShouldEqual(0);

            List<string> commands = this.gitProcess.CommandsRun;

            commands.Count.ShouldEqual(1);
            commands[0].ShouldEqual(this.MaintenanceCommand);
        }

        private void TestSetup()
        {
            this.gitProcess = new MockGitProcess();

            // Create enlistment using git process
            ScalarEnlistment enlistment = new MockScalarEnlistment(this.gitProcess);

            PhysicalFileSystem fileSystem = new MockFileSystem(new MockDirectory(enlistment.EnlistmentRoot, null, null));

            // Create and return Context
            this.tracer = new MockTracer();
            this.context = new ScalarContext(this.tracer, fileSystem, enlistment: enlistment);
        }
    }
}
