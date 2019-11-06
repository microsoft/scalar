using Moq;
using NUnit.Framework;
using Scalar.Common.RepoRegistry;
using Scalar.Tests.Should;
using Scalar.UnitTests.Mock.Common;
using Scalar.UnitTests.Mock.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;

namespace Scalar.UnitTests.Common.RepoRegistry
{
    [TestFixture]
    public class ScalarRepoRegistryTests
    {
        [TestCase]
        public void TryRegisterRepo_CreatesMissingRegistryDirectory()
        {
            string registryFolderPath = Path.Combine("mock:", "root", "UnitTests.RepoRegistry");

            MockFileSystem fileSystem = new MockFileSystem(new MockDirectory(Path.GetDirectoryName(registryFolderPath), null, null));
            ScalarRepoRegistry registry = new ScalarRepoRegistry(
                new MockTracer(),
                fileSystem,
                registryFolderPath);

            string testRepoRoot = Path.Combine("mock:", "Repos", "Repo1");
            string testUserId = "testUser";

            fileSystem.DirectoryExists(registryFolderPath).ShouldBeFalse();
            registry.TryRegisterRepo(testRepoRoot, testUserId, out string errorMessage).ShouldBeTrue();
            errorMessage.ShouldBeNull();
            fileSystem.DirectoryExists(registryFolderPath).ShouldBeTrue("Registering a repo should have created the missing registry directory");

            List<ScalarRepoRegistration> expectedRegistrations = new List<ScalarRepoRegistration>
            {
                new ScalarRepoRegistration(testRepoRoot, testUserId)
            };

            registry.GetRegisteredRepos().ShouldMatchInOrder(expectedRegistrations, RepoRegistrationsEqual);
            registry.GetRegisteredReposForUser(testUserId).ShouldMatchInOrder(expectedRegistrations, RepoRegistrationsEqual);
        }

        private static bool RepoRegistrationsEqual(ScalarRepoRegistration repo1, ScalarRepoRegistration repo2)
        {
            return
                repo1.NormalizedRepoRoot.Equals(repo2.NormalizedRepoRoot, StringComparison.Ordinal) &&
                repo1.UserId.Equals(repo2.UserId, StringComparison.Ordinal);
        }
    }
}
