using Moq;
using NUnit.Framework;
using Scalar.Common.FileSystem;
using Scalar.Common.RepoRegistry;
using Scalar.Tests.Should;
using Scalar.UnitTests.Category;
using Scalar.UnitTests.Mock.Common;
using Scalar.UnitTests.Mock.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scalar.UnitTests.Common.RepoRegistry
{
    [TestFixture]
    public class ScalarRepoRegistryTests
    {
        private readonly string registryFolderPath = Path.Combine("mock:", "root", "UnitTests.RepoRegistry");

        [TestCase]
        public void TryRegisterRepo_CreatesMissingRegistryDirectory()
        {
            MockFileSystem fileSystem = new MockFileSystem(new MockDirectory(Path.GetDirectoryName(this.registryFolderPath), null, null));
            ScalarRepoRegistry registry = new ScalarRepoRegistry(
                new MockTracer(),
                fileSystem,
                this.registryFolderPath);

            List<ScalarRepoRegistration> registrations = new List<ScalarRepoRegistration>
            {
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos", "Repo1"), "testUser")
            };

            fileSystem.DirectoryExists(this.registryFolderPath).ShouldBeFalse();
            this.RegisterRepos(registry, registrations);
            fileSystem.DirectoryExists(this.registryFolderPath).ShouldBeTrue("Registering a repo should have created the missing registry directory");

            this.RegistryShouldContainRegistrations(registry, registrations);
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void TryRegisterRepo_FailsIfMissingRegistryDirectoryCantBeCreated()
        {
            Mock<PhysicalFileSystem> mockFileSystem = new Mock<PhysicalFileSystem>(MockBehavior.Strict);
            mockFileSystem.Setup(fileSystem => fileSystem.DirectoryExists(this.registryFolderPath)).Returns(false);
            mockFileSystem.Setup(fileSystem => fileSystem.CreateDirectory(this.registryFolderPath)).Throws(new UnauthorizedAccessException());

            ScalarRepoRegistry registry = new ScalarRepoRegistry(
                new MockTracer(),
                mockFileSystem.Object,
                this.registryFolderPath);

            string testRepoRoot = Path.Combine("mock:", "Repos", "Repo1");
            string testUserId = "testUser";

            registry.TryRegisterRepo(testRepoRoot, testUserId, out string errorMessage).ShouldBeFalse();
            errorMessage.ShouldBeNonEmpty();

            mockFileSystem.VerifyAll();
        }

        [TestCase]
        public void TryRegisterRepo_RegisterMultipleRepos()
        {
            MockFileSystem fileSystem = new MockFileSystem(new MockDirectory(Path.GetDirectoryName(this.registryFolderPath), null, null));
            ScalarRepoRegistry registry = new ScalarRepoRegistry(
                new MockTracer(),
                fileSystem,
                this.registryFolderPath);

            List<ScalarRepoRegistration> repoRegistrations = new List<ScalarRepoRegistration>
            {
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos", "Repo1"), "testUser"),
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos", "Repo2"), "testUser"),
                new ScalarRepoRegistration(Path.Combine("mock:", "MoreRepos", "Repo1"), "user2"),
                new ScalarRepoRegistration(Path.Combine("mock:", "MoreRepos", "Repo2"), "user2"),
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos2", "Repo1"), "testUser"),
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos3", "Repo1"), "ThirdUser"),
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos3", "Repo2"), "ThirdUser")
            };

            this.RegisterRepos(registry, repoRegistrations);
            this.RegistryShouldContainRegistrations(registry, repoRegistrations);
        }

        [TestCase]
        public void TryRegisterRepo_UpdatesUsersForExistingRegistrations()
        {
            MockFileSystem fileSystem = new MockFileSystem(new MockDirectory(Path.GetDirectoryName(this.registryFolderPath), null, null));
            ScalarRepoRegistry registry = new ScalarRepoRegistry(
                new MockTracer(),
                fileSystem,
                this.registryFolderPath);

            List<ScalarRepoRegistration> registrationsPart1 = new List<ScalarRepoRegistration>
            {
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos", "Repo1"), "testUser"),
                new ScalarRepoRegistration(Path.Combine("mock:", "MoreRepos", "Repo1"), "user2"),
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos2", "Repo1"), "testUser")
            };

            List<ScalarRepoRegistration> registrationsPart2 = new List<ScalarRepoRegistration>
            {
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos", "Repo2"), "testUser"),
                new ScalarRepoRegistration(Path.Combine("mock:", "MoreRepos", "Repo2"), "user2"),
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos3", "Repo1"), "ThirdUser"),
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos3", "Repo2"), "ThirdUser")
            };

            this.RegisterRepos(registry, registrationsPart1.Concat(registrationsPart2));
            this.RegistryShouldContainRegistrations(registry, registrationsPart1.Concat(registrationsPart2));

            // Update the users on some registrations
            foreach (ScalarRepoRegistration registration in registrationsPart2)
            {
                registration.UserId = $"UPDATED_{registration.UserId}";
            }

            // Just register the updates
            this.RegisterRepos(registry, registrationsPart2);

            // The unchanged + updated entries should be present
            this.RegistryShouldContainRegistrations(registry, registrationsPart1.Concat(registrationsPart2));
        }

        [TestCase]
        public void TryUnregisterRepo_RemovesRegisteredRepos()
        {
            MockFileSystem fileSystem = new MockFileSystem(new MockDirectory(Path.GetDirectoryName(this.registryFolderPath), null, null));
            ScalarRepoRegistry registry = new ScalarRepoRegistry(
                new MockTracer(),
                fileSystem,
                this.registryFolderPath);

            List<ScalarRepoRegistration> registrationsPart1 = new List<ScalarRepoRegistration>
            {
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos", "Repo1"), "testUser"),
                new ScalarRepoRegistration(Path.Combine("mock:", "MoreRepos", "Repo1"), "user2"),
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos2", "Repo1"), "testUser")
            };

            List<ScalarRepoRegistration> registrationsPart2 = new List<ScalarRepoRegistration>
            {
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos", "Repo2"), "testUser"),
                new ScalarRepoRegistration(Path.Combine("mock:", "MoreRepos", "Repo2"), "user2"),
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos3", "Repo1"), "ThirdUser"),
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos3", "Repo2"), "ThirdUser")
            };

            this.RegisterRepos(registry, registrationsPart1.Concat(registrationsPart2));
            this.RegistryShouldContainRegistrations(registry, registrationsPart1.Concat(registrationsPart2));
            this.UnregisterRepos(registry, registrationsPart2);
            this.RegistryShouldContainRegistrations(registry, registrationsPart1);
        }

        [TestCase]
        public void TryUnregisterRepo_FailsIfRegistryDirectoryMissing()
        {
            MockFileSystem fileSystem = new MockFileSystem(new MockDirectory(Path.GetDirectoryName(this.registryFolderPath), null, null));
            ScalarRepoRegistry registry = new ScalarRepoRegistry(
                new MockTracer(),
                fileSystem,
                this.registryFolderPath);
            fileSystem.DirectoryExists(this.registryFolderPath).ShouldBeFalse();
            registry.TryUnregisterRepo(Path.Combine("mock:", "Repos", "Repo1"), out string errorMessage).ShouldBeFalse();
            errorMessage.ShouldBeNonEmpty();
            fileSystem.DirectoryExists(this.registryFolderPath).ShouldBeFalse();
        }

        [TestCase]
        public void TryUnregisterRepo_FailsForUnregisteredRepo()
        {
            MockFileSystem fileSystem = new MockFileSystem(new MockDirectory(Path.GetDirectoryName(this.registryFolderPath), null, null));
            ScalarRepoRegistry registry = new ScalarRepoRegistry(
                new MockTracer(),
                fileSystem,
                this.registryFolderPath);

            List<ScalarRepoRegistration> registrations = new List<ScalarRepoRegistration>
            {
                new ScalarRepoRegistration(Path.Combine("mock:", "Repos", "Repo1"), "testUser")
            };

            this.RegisterRepos(registry, registrations);
            registry.TryUnregisterRepo(Path.Combine("mock:", "Repos", "Repo2"), out string errorMessage).ShouldBeFalse();
            errorMessage.ShouldBeNonEmpty();
            this.RegistryShouldContainRegistrations(registry, registrations);
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void TryUnregisterRepo_FailsIfDeleteFails()
        {
            string repoPath = Path.Combine("mock:", "Repos", "Repo1");
            string registrationFilePath = Path.Combine("mock:", "root", "UnitTests.RepoRegistry", ScalarRepoRegistry.GetRepoRootSha(repoPath) + ".repo");

            Mock<PhysicalFileSystem> mockFileSystem = new Mock<PhysicalFileSystem>(MockBehavior.Strict);
            mockFileSystem.Setup(fileSystem => fileSystem.FileExists(registrationFilePath)).Returns(true);
            mockFileSystem.Setup(fileSystem => fileSystem.DeleteFile(registrationFilePath)).Throws(new UnauthorizedAccessException());

            ScalarRepoRegistry registry = new ScalarRepoRegistry(
                new MockTracer(),
                mockFileSystem.Object,
                this.registryFolderPath);

            registry.TryUnregisterRepo(repoPath, out string errorMessage).ShouldBeFalse();
            errorMessage.ShouldBeNonEmpty();

            mockFileSystem.VerifyAll();
        }

        [TestCase]
        public void GetRepoRootSha_IsStable()
        {
            ScalarRepoRegistry.GetRepoRootSha("mock:/Repos/Repo1").ShouldEqual("7fe00a3479e25557a439fc5a2b19a1ec2605fb59");
            ScalarRepoRegistry.GetRepoRootSha("mock:/folder/repoRoot").ShouldEqual("7e83aa2e7baab0b4bcd39eaeb3c8b189e5167297");
        }

        private static bool RepoRegistrationsEqual(ScalarRepoRegistration repo1, ScalarRepoRegistration repo2)
        {
            return
                repo1.NormalizedRepoRoot.Equals(repo2.NormalizedRepoRoot, StringComparison.Ordinal) &&
                repo1.UserId.Equals(repo2.UserId, StringComparison.Ordinal);
        }

        private void RegisterRepos(ScalarRepoRegistry registry, IEnumerable<ScalarRepoRegistration> registrations)
        {
            foreach (ScalarRepoRegistration registration in registrations)
            {
                registry.TryRegisterRepo(registration.NormalizedRepoRoot, registration.UserId, out string errorMessage).ShouldBeTrue();
                errorMessage.ShouldBeNull();
            }
        }

        private void UnregisterRepos(ScalarRepoRegistry registry, IEnumerable<ScalarRepoRegistration> registrations)
        {
            foreach (ScalarRepoRegistration registration in registrations)
            {
                registry.TryUnregisterRepo(registration.NormalizedRepoRoot, out string errorMessage).ShouldBeTrue();
                errorMessage.ShouldBeNull();
            }
        }

        private void RegistryShouldContainRegistrations(ScalarRepoRegistry registry, IEnumerable<ScalarRepoRegistration> registrations)
        {
            registry.GetRegisteredRepos().ShouldMatch(registrations, RepoRegistrationsEqual);

            IEnumerable<string> registeredUsers = registrations.Select(r => r.UserId).Distinct();
            foreach (string user in registeredUsers)
            {
                registry.GetRegisteredReposForUser(user).ShouldMatch(
                    registrations.Where(x => x.UserId.Equals(user)),
                    RepoRegistrationsEqual);
            }
        }
    }
}
