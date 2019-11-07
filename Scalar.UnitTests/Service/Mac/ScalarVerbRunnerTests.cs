using Moq;
using NUnit.Framework;
using Scalar.Common;
using Scalar.Common.Maintenance;
using Scalar.Service;
using Scalar.UnitTests.Mock.Common;
using Scalar.UnitTests.Mock.FileSystem;
using System.IO;

namespace Scalar.UnitTests.Service.Mac
{
    [TestFixture]
    public class ScalarVerbRunnerTests
    {
        private const int ExpectedActiveUserId = 502;
        private static readonly string ExpectedActiveRepoPath = Path.Combine(MockFileSystem.GetMockRoot(), "code", "repo2");

        private MockTracer tracer;
        private MockPlatform scalarPlatform;

        [SetUp]
        public void SetUp()
        {
            this.tracer = new MockTracer();
            this.scalarPlatform = (MockPlatform)ScalarPlatform.Instance;
            this.scalarPlatform.MockCurrentUser = ExpectedActiveUserId.ToString();
        }

        [TestCase]
        public void CallMaintenance_LaunchesVerbUsingCorrectArgs()
        {
            MaintenanceTasks.Task task = MaintenanceTasks.Task.FetchCommitsAndTrees;
            string taskVerbName = MaintenanceTasks.GetVerbTaskName(task);
            string executable = @"/bin/launchctl";
            string scalarBinPath = Path.Combine(this.scalarPlatform.Constants.ScalarBinDirectoryPath, this.scalarPlatform.Constants.ScalarExecutableName);
            string expectedArgs =
                $"asuser {ExpectedActiveUserId} {scalarBinPath} maintenance \"{ExpectedActiveRepoPath}\" --{ScalarConstants.VerbParameters.Maintenance.Task} {taskVerbName} --{ScalarConstants.VerbParameters.InternalUseOnly} {new InternalVerbParameters(startedByService: true).ToJson()}";

            Mock<ScalarVerbRunner.ScalarProcessLauncher> mountLauncherMock = new Mock<ScalarVerbRunner.ScalarProcessLauncher>(MockBehavior.Strict, this.tracer);
            mountLauncherMock.Setup(mp => mp.LaunchProcess(
                executable,
                expectedArgs,
                ExpectedActiveRepoPath))
                .Returns(new ProcessResult(output: string.Empty, errors: string.Empty, exitCode: 0));

            ScalarVerbRunner verbProcess = new ScalarVerbRunner(this.tracer, mountLauncherMock.Object);
            verbProcess.CallMaintenance(task, ExpectedActiveRepoPath, ExpectedActiveUserId);

            mountLauncherMock.VerifyAll();
        }
    }
}
