using NUnit.Framework;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Maintenance;
using Scalar.Common.Tracing;
using Scalar.Tests.Should;
using Scalar.UnitTests.Category;
using Scalar.UnitTests.Mock.Common;
using Scalar.UnitTests.Mock.FileSystem;

namespace Scalar.UnitTests.Maintenance
{
    [TestFixture]
    public class GitMaintenanceStepTests
    {
        private ScalarContext context;

        public enum WhenToStop
        {
            Never,
            BeforeGitCommand,
            DuringGitCommand
        }

        [TestCase]
        public void GitMaintenanceStepRunsGitAction()
        {
            this.TestSetup();

            CheckMethodStep step = new CheckMethodStep(this.context, WhenToStop.Never);
            step.Execute();

            step.SawWorkInvoked.ShouldBeTrue();
            step.SawEndOfMethod.ShouldBeTrue();
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void GitMaintenanceStepSkipsGitActionAfterStop()
        {
            this.TestSetup();

            CheckMethodStep step = new CheckMethodStep(this.context, WhenToStop.Never);

            step.Stop();
            step.Execute();

            step.SawWorkInvoked.ShouldBeFalse();
            step.SawEndOfMethod.ShouldBeFalse();
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void GitMaintenanceStepSkipsRunGitCommandAfterStop()
        {
            this.TestSetup();

            CheckMethodStep step = new CheckMethodStep(this.context, WhenToStop.BeforeGitCommand);

            step.Execute();

            step.SawWorkInvoked.ShouldBeFalse();
            step.SawEndOfMethod.ShouldBeFalse();
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void GitMaintenanceStepThrowsIfStoppedDuringGitCommand()
        {
            this.TestSetup();
            CheckMethodStep step = new CheckMethodStep(this.context, WhenToStop.DuringGitCommand);

            step.Execute();

            step.SawWorkInvoked.ShouldBeTrue();
            step.SawEndOfMethod.ShouldBeFalse();
        }

        private void TestSetup()
        {
            ITracer tracer = new MockTracer();
            ScalarEnlistment enlistment = new MockScalarEnlistment();
            PhysicalFileSystem fileSystem = new MockFileSystem(new MockDirectory(enlistment.EnlistmentRoot, null, null));

            this.context = new ScalarContext(tracer, fileSystem, enlistment);
        }

        public class CheckMethodStep : GitMaintenanceStep
        {
            private WhenToStop when;

            public CheckMethodStep(ScalarContext context, WhenToStop when)
                : base(context, requireObjectCacheLock: true)
            {
                this.when = when;
            }

            public bool SawWorkInvoked { get; set; }
            public bool SawEndOfMethod { get; set; }

            public override string Area => "CheckMethodStep";

            protected override void PerformMaintenance()
            {
                if (this.when == WhenToStop.BeforeGitCommand)
                {
                    this.Stop();
                }

                this.RunGitCommand(
                    process =>
                    {
                        this.SawWorkInvoked = true;

                        if (this.when == WhenToStop.DuringGitCommand)
                        {
                            this.Stop();
                        }

                        return null;
                    },
                    nameof(this.SawWorkInvoked));

                this.SawEndOfMethod = true;
            }
        }
    }
}
