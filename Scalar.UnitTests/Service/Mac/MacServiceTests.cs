using Moq;
using NUnit.Framework;
using Scalar.Common;
using Scalar.Common.NamedPipes;
using Scalar.Service;
using Scalar.Service.Handlers;
using Scalar.UnitTests.Mock.Common;
using Scalar.UnitTests.Mock.FileSystem;
using System.IO;

namespace Scalar.UnitTests.Service.Mac
{
    [TestFixture]
    public class MacServiceTests
    {
        private const string ScalarServiceName = "Scalar.Service";
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
        public void ServiceStartTriggersAutoMountForCurrentUser()
        {
            Mock<IRepoRegistry> repoRegistry = new Mock<IRepoRegistry>(MockBehavior.Strict);
            repoRegistry.Setup(r => r.AutoMountRepos(ExpectedActiveUserId.ToString(), ExpectedSessionId));
            repoRegistry.Setup(r => r.TraceStatus());

            MacScalarService service = new MacScalarService(
                this.tracer,
                serviceName: null,
                repoRegistry: repoRegistry.Object);

            service.Run();

            repoRegistry.VerifyAll();
        }

        [TestCase]
        public void RepoRegistryMountsOnlyRegisteredRepos()
        {
            Mock<IRepoMounter> repoMounterMock = new Mock<IRepoMounter>(MockBehavior.Strict);
            Mock<INotificationHandler> notificationHandlerMock = new Mock<INotificationHandler>(MockBehavior.Strict);

            repoMounterMock.Setup(mp => mp.MountRepository(ExpectedActiveRepoPath, ExpectedActiveUserId)).Returns(true);
            notificationHandlerMock.Setup(nh => nh.SendNotification(
                It.Is<NamedPipeMessages.Notification.Request>(rq => rq.Id == NamedPipeMessages.Notification.Request.Identifier.AutomountStart)));

            this.CreateTestRepos(ServiceDataLocation);

            RepoRegistry repoRegistry = new RepoRegistry(
                this.tracer,
                this.fileSystem,
                ServiceDataLocation,
                repoMounterMock.Object,
                notificationHandlerMock.Object);

            repoRegistry.AutoMountRepos(ExpectedActiveUserId.ToString(), ExpectedSessionId);

            repoMounterMock.VerifyAll();
            notificationHandlerMock.VerifyAll();
        }

        [TestCase]
        public void MountProcessLaunchedUsingCorrectArgs()
        {
            string executable = @"/bin/launchctl";
            string scalarBinPath = Path.Combine(this.scalarPlatform.Constants.ScalarBinDirectoryPath, this.scalarPlatform.Constants.ScalarExecutableName);
            string expectedArgs = $"asuser {ExpectedActiveUserId} {scalarBinPath} mount {ExpectedActiveRepoPath}";

            Mock<MacScalarMountProcess.MountLauncher> mountLauncherMock = new Mock<MacScalarMountProcess.MountLauncher>(MockBehavior.Strict, this.tracer);
            mountLauncherMock.Setup(mp => mp.LaunchProcess(
                executable,
                expectedArgs,
                ExpectedActiveRepoPath))
                .Returns(true);

            string errorString = null;
            mountLauncherMock.Setup(mp => mp.WaitUntilMounted(
                this.tracer,
                ExpectedActiveRepoPath,
                It.IsAny<bool>(),
                out errorString))
                .Returns(true);

            MacScalarMountProcess mountProcess = new MacScalarMountProcess(this.tracer, mountLauncherMock.Object);
            mountProcess.MountRepository(ExpectedActiveRepoPath, ExpectedActiveUserId);

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
