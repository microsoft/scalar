using Moq;
using NUnit.Framework;
using Scalar.Common;
using Scalar.Common.Maintenance;
using Scalar.Service;
using Scalar.Tests.Should;
using Scalar.UnitTests.Mock.Common;
using Scalar.UnitTests.Mock.FileSystem;
using System.IO;

namespace Scalar.UnitTests.Service.Mac
{
    [TestFixture]
    public class MacServiceTests
    {
        private const int ExpectedActiveUserId = 502;
        private const int ExpectedSessionId = 502;
        private static readonly string ExpectedActiveRepoPath = Path.Combine("mock:", "code", "repo2");
        private static readonly string ServiceDataLocation = Path.Combine("mock:", "registryDataFolder");

        private MockFileSystem fileSystem;
        private MockTracer tracer;
        private MockPlatform scalarPlatform;

        [SetUp]
        public void SetUp()
        {
            this.tracer = new MockTracer();
            this.fileSystem = new MockFileSystem(new MockDirectory(ServiceDataLocation, null, null));
            this.scalarPlatform = (MockPlatform)ScalarPlatform.Instance;
            this.scalarPlatform.MockCurrentUser = ExpectedActiveUserId.ToString();
        }

        [TestCase]
        public void RepoRegistryRemovesRegisteredRepoIfMissingFromDisk()
        {
            Mock<IScalarVerbRunner> repoMounterMock = new Mock<IScalarVerbRunner>(MockBehavior.Strict);

            this.fileSystem.DirectoryExists(ExpectedActiveRepoPath).ShouldBeFalse($"{ExpectedActiveRepoPath} should not exist");

            MaintenanceTasks.Task task = MaintenanceTasks.Task.FetchCommitsAndTrees;

            this.CreateTestRepos(ServiceDataLocation);
            RepoRegistry repoRegistry = new RepoRegistry(
                this.tracer,
                this.fileSystem,
                ServiceDataLocation,
                repoMounterMock.Object);

            repoRegistry.RunMaintenanceTaskForRepos(task, ExpectedActiveUserId.ToString(), ExpectedSessionId);
            repoMounterMock.VerifyAll();

            repoRegistry.ReadRegistry().ShouldNotContain(entry => entry.Key.Equals(ExpectedActiveRepoPath));
        }

        [TestCase]
        public void RepoRegistryCallsMaintenanceVerbOnlyForRegisteredRepos()
        {
            Mock<IScalarVerbRunner> repoMounterMock = new Mock<IScalarVerbRunner>(MockBehavior.Strict);

            this.fileSystem.CreateDirectory(ExpectedActiveRepoPath);

            MaintenanceTasks.Task task = MaintenanceTasks.Task.FetchCommitsAndTrees;
            repoMounterMock.Setup(mp => mp.CallMaintenance(task, ExpectedActiveRepoPath, ExpectedActiveUserId)).Returns(true);

            this.CreateTestRepos(ServiceDataLocation);

            RepoRegistry repoRegistry = new RepoRegistry(
                this.tracer,
                this.fileSystem,
                ServiceDataLocation,
                repoMounterMock.Object);

            repoRegistry.RunMaintenanceTaskForRepos(task, ExpectedActiveUserId.ToString(), ExpectedSessionId);

            repoMounterMock.VerifyAll();
        }

        [TestCase]
        public void MaintenanceVerbLaunchedUsingCorrectArgs()
        {
            MaintenanceTasks.Task task = MaintenanceTasks.Task.FetchCommitsAndTrees;
            string taskVerbName = MaintenanceTasks.GetVerbTaskName(task);
            string executable = @"/bin/launchctl";
            string scalarBinPath = Path.Combine(this.scalarPlatform.Constants.ScalarBinDirectoryPath, this.scalarPlatform.Constants.ScalarExecutableName);
            string expectedArgs =
                $"asuser {ExpectedActiveUserId} {scalarBinPath} maintenance \"{ExpectedActiveRepoPath}\" --{ScalarConstants.VerbParameters.Maintenance.Task} {taskVerbName} --{ScalarConstants.VerbParameters.InternalUseOnly} {new InternalVerbParameters(startedByService: true).ToJson()}";

            Mock<MacScalarVerbRunner.ScalarProcessLauncher> mountLauncherMock = new Mock<MacScalarVerbRunner.ScalarProcessLauncher>(MockBehavior.Strict, this.tracer);
            mountLauncherMock.Setup(mp => mp.LaunchProcess(
                executable,
                expectedArgs,
                ExpectedActiveRepoPath))
                .Returns(new ProcessResult(output: string.Empty, errors: string.Empty, exitCode: 0));

            MacScalarVerbRunner verbProcess = new MacScalarVerbRunner(this.tracer, mountLauncherMock.Object);
            verbProcess.CallMaintenance(task, ExpectedActiveRepoPath, ExpectedActiveUserId);

            mountLauncherMock.VerifyAll();
        }

        private void CreateTestRepos(string dataLocation)
        {
            string repo1 = Path.Combine("mock:", "code", "repo1");
            string repo2 = ExpectedActiveRepoPath;
            string repo3 = Path.Combine("mock:", "code", "repo3");
            string repo4 = Path.Combine("mock:", "code", "repo4");

            this.fileSystem.WriteAllText(
                Path.Combine(dataLocation, RepoRegistry.RegistryName),
                $@"1
                {{""EnlistmentRoot"":""{repo1.Replace("\\", "\\\\")}"",""OwnerSID"":502,""IsActive"":false}}
                {{""EnlistmentRoot"":""{repo2.Replace("\\", "\\\\")}"",""OwnerSID"":502,""IsActive"":true}}
                {{""EnlistmentRoot"":""{repo3.Replace("\\", "\\\\")}"",""OwnerSID"":501,""IsActive"":false}}
                {{""EnlistmentRoot"":""{repo4.Replace("\\", "\\\\")}"",""OwnerSID"":501,""IsActive"":true}}
                ");
        }
    }
}
