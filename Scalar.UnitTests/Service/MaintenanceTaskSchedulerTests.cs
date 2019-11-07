using Moq;
using NUnit.Framework;
using Scalar.Common.FileSystem;
using Scalar.Common.Maintenance;
using Scalar.Common.RepoRegistry;
using Scalar.Service;
using Scalar.Tests.Should;
using Scalar.UnitTests.Mock.Common;
using Scalar.UnitTests.Mock.FileSystem;
using System.Collections.Generic;
using System.IO;

namespace Scalar.UnitTests.Service
{
    [TestFixture]
    public class MaintenanceTaskSchedulerTests
    {
        private MockTracer mockTracer;
        private Mock<PhysicalFileSystem> mockFileSystem;
        private Mock<IScalarVerbRunner> mockVerbRunner;
        private Mock<IScalarRepoRegistry> mockRepoRegistry;
        private Mock<IRegisteredUserStore> mockRegisteredUserStore;

        [SetUp]
        public void Setup()
        {
            this.mockTracer = new MockTracer();
            this.mockFileSystem = new Mock<PhysicalFileSystem>(MockBehavior.Strict);
            this.mockVerbRunner = new Mock<IScalarVerbRunner>(MockBehavior.Strict);
            this.mockRepoRegistry = new Mock<IScalarRepoRegistry>(MockBehavior.Strict);
            this.mockRegisteredUserStore = new Mock<IRegisteredUserStore>(MockBehavior.Strict);
        }

        [TearDown]
        public void TearDown()
        {
            this.mockFileSystem.VerifyAll();
            this.mockVerbRunner.VerifyAll();
            this.mockRepoRegistry.VerifyAll();
            this.mockRegisteredUserStore.VerifyAll();
        }

        [TestCase]
        public void RegisterUser()
        {
            using (MaintenanceTaskScheduler taskScheduler = new MaintenanceTaskScheduler(
                this.mockTracer,
                this.mockFileSystem.Object,
                this.mockVerbRunner.Object,
                this.mockRepoRegistry.Object))
            {
                taskScheduler.RegisteredUser.ShouldBeNull();

                UserAndSession testUser1 = new UserAndSession("testUser1", sessionId: 1);
                UserAndSession testUser2 = new UserAndSession("testUser2", sessionId: 2);

                taskScheduler.RegisterUser(testUser1);
                taskScheduler.RegisteredUser.UserId.ShouldEqual(testUser1.UserId);
                taskScheduler.RegisteredUser.SessionId.ShouldEqual(testUser1.SessionId);

                taskScheduler.RegisterUser(testUser2);
                taskScheduler.RegisteredUser.UserId.ShouldEqual(testUser2.UserId);
                taskScheduler.RegisteredUser.SessionId.ShouldEqual(testUser2.SessionId);
            }
        }

        [TestCase]
        public void MaintenanceTask_Execute_NoRegisteredUser()
        {
            MaintenanceTasks.Task task = MaintenanceTasks.Task.PackFiles;

            this.mockRegisteredUserStore.SetupGet(mrus => mrus.RegisteredUser).Returns((UserAndSession)null);

            MaintenanceTaskScheduler.MaintenanceTask maintenanceTask = new MaintenanceTaskScheduler.MaintenanceTask(
                this.mockTracer,
                this.mockFileSystem.Object,
                this.mockVerbRunner.Object,
                this.mockRepoRegistry.Object,
                this.mockRegisteredUserStore.Object,
                task);

            maintenanceTask.Execute();
            this.mockTracer.RelatedInfoEvents.ShouldContain(entry => entry.Contains($"Skipping '{task}', no registered user"));
        }

        [TestCase]
        public void MaintenanceTask_Execute_SkipsReposThatDoNotMatchRegisteredUser()
        {
            MaintenanceTasks.Task task = MaintenanceTasks.Task.PackFiles;

            UserAndSession testUser = new UserAndSession("testUserId", sessionId: 1);
            this.mockRegisteredUserStore.SetupGet(mrus => mrus.RegisteredUser).Returns(testUser);

            this.mockRepoRegistry.Setup(reg => reg.GetRegisteredRepos()).Returns(
                new List<ScalarRepoRegistration>
                {
                    new ScalarRepoRegistration(Path.Combine(MockFileSystem.GetMockRoot(), "Repos", "repoRoot"), "nonMatchingUser"),
                    new ScalarRepoRegistration(Path.Combine(MockFileSystem.GetMockRoot(), "Repos", "repoRoot2"), "nonMatchingUser2")
                });

            MaintenanceTaskScheduler.MaintenanceTask maintenanceTask = new MaintenanceTaskScheduler.MaintenanceTask(
                this.mockTracer,
                this.mockFileSystem.Object,
                this.mockVerbRunner.Object,
                this.mockRepoRegistry.Object,
                this.mockRegisteredUserStore.Object,
                task);

            maintenanceTask.Execute();
            this.mockTracer.RelatedEvents.ShouldContain(entry => entry.Contains("No registered repos for user"));
        }

        [TestCase]
        public void MaintenanceTask_Execute_SkipsRegisteredRepoIfVolumeDoesNotExist()
        {
            MaintenanceTasks.Task task = MaintenanceTasks.Task.PackFiles;

            UserAndSession testUser = new UserAndSession("testUserId", sessionId: 1);
            this.mockRegisteredUserStore.SetupGet(mrus => mrus.RegisteredUser).Returns(testUser);

            string repoPath = Path.Combine(MockFileSystem.GetMockRoot(), "Repos", "repoRoot");
            this.mockRepoRegistry.Setup(reg => reg.GetRegisteredRepos()).Returns(
                new List<ScalarRepoRegistration>
                {
                    new ScalarRepoRegistration(repoPath, testUser.UserId)
                });

            this.mockFileSystem.Setup(fs => fs.DirectoryExists(Path.GetPathRoot(repoPath))).Returns(false);

            MaintenanceTaskScheduler.MaintenanceTask maintenanceTask = new MaintenanceTaskScheduler.MaintenanceTask(
                this.mockTracer,
                this.mockFileSystem.Object,
                this.mockVerbRunner.Object,
                this.mockRepoRegistry.Object,
                this.mockRegisteredUserStore.Object,
                task);

            maintenanceTask.Execute();
            this.mockTracer.RelatedEvents.ShouldContain(entry => entry.Contains("SkippedRepoWithMissingVolume"));
        }

        [TestCase]
        public void MaintenanceTask_Execute_UnregistersRepoIfMissing()
        {
            MaintenanceTasks.Task task = MaintenanceTasks.Task.PackFiles;

            UserAndSession testUser = new UserAndSession("testUserId", sessionId: 1);
            this.mockRegisteredUserStore.SetupGet(mrus => mrus.RegisteredUser).Returns(testUser);

            string repoPath = Path.Combine(MockFileSystem.GetMockRoot(), "Repos", "repoRoot");
            this.mockRepoRegistry.Setup(reg => reg.GetRegisteredRepos()).Returns(
                new List<ScalarRepoRegistration>
                {
                    new ScalarRepoRegistration(repoPath, testUser.UserId)
                });

            // Validate that TryUnregisterRepo will be called for repoPath
            string emptyString = string.Empty;
            this.mockRepoRegistry.Setup(reg => reg.TryUnregisterRepo(repoPath, out emptyString)).Returns(true);

            // The root volume should exist
            this.mockFileSystem.Setup(fs => fs.DirectoryExists(Path.GetPathRoot(repoPath))).Returns(true);

            // The repo itself does not exist
            this.mockFileSystem.Setup(fs => fs.DirectoryExists(repoPath)).Returns(false);

            MaintenanceTaskScheduler.MaintenanceTask maintenanceTask = new MaintenanceTaskScheduler.MaintenanceTask(
                this.mockTracer,
                this.mockFileSystem.Object,
                this.mockVerbRunner.Object,
                this.mockRepoRegistry.Object,
                this.mockRegisteredUserStore.Object,
                task);

            maintenanceTask.Execute();
            this.mockTracer.RelatedEvents.ShouldContain(entry => entry.Contains("RemovedMissingRepo"));
        }

        [TestCase]
        public void MaintenanceTask_Execute_CallsMaintenanceVerbOnlyForRegisteredRepos()
        {
            MaintenanceTasks.Task task = MaintenanceTasks.Task.PackFiles;

            UserAndSession testUser = new UserAndSession("testUserId", sessionId: 1);
            UserAndSession secondUser = new UserAndSession("testUserId2", sessionId: 1);
            string repoPath1 = Path.Combine(MockFileSystem.GetMockRoot(), "Repos", "repoRoot");
            string repoPath2 = Path.Combine(MockFileSystem.GetMockRoot(), "Repos", "repoRoot2");
            string secondUsersRepoPath = Path.Combine(MockFileSystem.GetMockRoot(), "Repos", "secondUsersRepo");

            this.mockRegisteredUserStore.SetupGet(mrus => mrus.RegisteredUser).Returns(testUser);

            this.mockRepoRegistry.Setup(reg => reg.GetRegisteredRepos()).Returns(
                new List<ScalarRepoRegistration>
                {
                    new ScalarRepoRegistration(repoPath1, testUser.UserId),
                    new ScalarRepoRegistration(secondUsersRepoPath, secondUser.UserId),
                    new ScalarRepoRegistration(repoPath2, testUser.UserId)
                });

            // The root volume and repos exist
            this.mockFileSystem.Setup(fs => fs.DirectoryExists(Path.GetPathRoot(repoPath1))).Returns(true);
            this.mockFileSystem.Setup(fs => fs.DirectoryExists(repoPath1)).Returns(true);
            this.mockFileSystem.Setup(fs => fs.DirectoryExists(repoPath2)).Returns(true);

            this.mockVerbRunner.Setup(vr => vr.CallMaintenance(task, repoPath1, testUser.SessionId)).Returns(true);
            this.mockVerbRunner.Setup(vr => vr.CallMaintenance(task, repoPath2, testUser.SessionId)).Returns(true);

            MaintenanceTaskScheduler.MaintenanceTask maintenanceTask = new MaintenanceTaskScheduler.MaintenanceTask(
                this.mockTracer,
                this.mockFileSystem.Object,
                this.mockVerbRunner.Object,
                this.mockRepoRegistry.Object,
                this.mockRegisteredUserStore.Object,
                task);

            maintenanceTask.Execute();
        }
    }
}
