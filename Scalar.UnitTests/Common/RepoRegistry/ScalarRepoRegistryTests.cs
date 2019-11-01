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
        [SetUp]
        public void Setup()
        {
        }

        [TearDown]
        public void TearDown()
        {
        }

        ////[TestCase]
        ////public void TryRegisterRepo_EmptyRegistry()
        ////{
        ////    string dataLocation = Path.Combine("mock:", "registryDataFolder");
        ////
        ////    MockFileSystem fileSystem = new MockFileSystem(new MockDirectory(dataLocation, null, null));
        ////    RepoRegistry registry = new RepoRegistry(
        ////        new MockTracer(),
        ////        fileSystem,
        ////        dataLocation);
        ////
        ////    string repoRoot = Path.Combine("c:", "test");
        ////    string ownerSID = Guid.NewGuid().ToString();
        ////
        ////    string errorMessage;
        ////    registry.TryRegisterRepo(repoRoot, ownerSID, out errorMessage).ShouldEqual(true);
        ////
        ////    Dictionary<string, RepoRegistration> verifiableRegistry = registry.ReadRegistry();
        ////    verifiableRegistry.Count.ShouldEqual(1);
        ////    this.VerifyRepo(verifiableRegistry[repoRoot], ownerSID);
        ////}

        private void VerifyRepo(ScalarRepoRegistration repo, string expectedOwnerSID)
        {
            repo.ShouldNotBeNull();
            repo.OwnerSID.ShouldEqual(expectedOwnerSID);
        }
    }
}
