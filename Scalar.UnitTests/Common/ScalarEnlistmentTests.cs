using NUnit.Framework;
using Scalar.Common;
using Scalar.Common.Git;
using Scalar.Tests.Should;
using Scalar.UnitTests.Mock.Git;

namespace Scalar.UnitTests.Common
{
    [TestFixture]
    public class ScalarEnlistmentTests
    {
        private const string EnlistmentId = "520dcf634ce34065a06abaa4010a256f";

        [TestCase]
        public void CanGetEnlistmentId()
        {
            TestScalarEnlistment enlistment = new TestScalarEnlistment();
            enlistment.GetEnlistmentId().ShouldEqual(EnlistmentId);
        }

        private class TestScalarEnlistment : ScalarEnlistment
        {
            private MockGitProcess gitProcess;

            public TestScalarEnlistment()
                : base("mock:\\path", "mock://repoUrl", "mock:\\git", authentication: null)
            {
                this.gitProcess = new MockGitProcess();
                this.gitProcess.SetExpectedCommandResult(
                    "config --local scalar.enlistment-id",
                    () => new GitProcess.Result(EnlistmentId, string.Empty, GitProcess.Result.SuccessCode));
            }

            public override GitProcess CreateGitProcess()
            {
                return this.gitProcess;
            }
        }
    }
}
