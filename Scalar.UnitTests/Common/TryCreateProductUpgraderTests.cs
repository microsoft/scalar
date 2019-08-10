using Moq;
using NUnit.Framework;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.NuGetUpgrade;
using Scalar.Tests.Should;
using Scalar.UnitTests.Mock.Common;

namespace Scalar.UnitTests.Common
{
    public class TryCreateProductUpgradeTests
    {
        private static string defaultUpgradeFeedPackageName = "package";
        private static string defaultUpgradeFeedUrl = "https://pkgs.dev.azure.com/contoso/";
        private static string defaultOrgInfoServerUrl = "https://www.contoso.com";
        private static string defaultRing = "slow";

        private MockTracer tracer;
        private Mock<PhysicalFileSystem> fileSystemMock;
        private Mock<ICredentialStore> credentialStoreMock;

        [SetUp]
        public void Setup()
        {
            this.tracer = new MockTracer();

            // It is important that creating a new Upgrader does not
            // require credentials.  We must be able to create an
            // upgrader to query / check upgrade preconditions without
            // requiring authorization.  We create these mocks with
            // strict behavior to validate methods on them are called
            // unnecessarily.
            this.credentialStoreMock = new Mock<ICredentialStore>(MockBehavior.Strict);
            this.fileSystemMock = new Mock<PhysicalFileSystem>(MockBehavior.Strict);
        }

        [TearDown]
        public void TearDown()
        {
            this.credentialStoreMock.VerifyAll();
            this.fileSystemMock.VerifyAll();
        }

        [TestCase]
        public void CreatesNuGetUpgraderWhenConfigured()
        {
            MockLocalScalarConfig scalarConfig = this.ConstructDefaultMockNuGetConfigBuilder()
                .Build();

            bool success = ProductUpgrader.TryCreateUpgrader(
                this.tracer,
                this.fileSystemMock.Object,
                scalarConfig,
                this.credentialStoreMock.Object,
                false,
                false,
                out ProductUpgrader productUpgrader,
                out string error);

            success.ShouldBeTrue();
            productUpgrader.ShouldNotBeNull();
            productUpgrader.ShouldBeOfType<NuGetUpgrader>();
            error.ShouldBeNull();
        }

        [TestCase]
        public void CreatesNuGetUpgraderWhenConfiguredWithNoRing()
        {
            MockLocalScalarConfig scalarConfig = this.ConstructDefaultMockNuGetConfigBuilder()
                .WithNoUpgradeRing()
                .Build();

            bool success = ProductUpgrader.TryCreateUpgrader(
                this.tracer,
                this.fileSystemMock.Object,
                scalarConfig,
                this.credentialStoreMock.Object,
                false,
                false,
                out ProductUpgrader productUpgrader,
                out string error);

            success.ShouldBeTrue();
            productUpgrader.ShouldNotBeNull();
            productUpgrader.ShouldBeOfType<NuGetUpgrader>();
            error.ShouldBeNull();
        }

        [TestCase]
        public void CreatesGitHubUpgraderWhenConfigured()
        {
            MockLocalScalarConfig scalarConfig = this.ConstructDefaultGitHubConfigBuilder()
                .Build();

            bool success = ProductUpgrader.TryCreateUpgrader(
                this.tracer,
                this.fileSystemMock.Object,
                scalarConfig,
                this.credentialStoreMock.Object,
                false,
                false,
                out ProductUpgrader productUpgrader,
                out string error);

            success.ShouldBeTrue();
            productUpgrader.ShouldNotBeNull();
            productUpgrader.ShouldBeOfType<GitHubUpgrader>();
            error.ShouldBeNull();
        }

        [TestCase]
        public void CreatesOrgNuGetUpgrader()
        {
            MockLocalScalarConfig scalarConfig = this.ConstructDefaultMockOrgNuGetConfigBuilder()
                .Build();

            bool success = ProductUpgrader.TryCreateUpgrader(
                this.tracer,
                this.fileSystemMock.Object,
                scalarConfig,
                this.credentialStoreMock.Object,
                false,
                false,
                out ProductUpgrader productUpgrader,
                out string error);

            success.ShouldBeTrue();
            productUpgrader.ShouldNotBeNull();
            productUpgrader.ShouldBeOfType<OrgNuGetUpgrader>();
            error.ShouldBeNull();
        }

        [TestCase]
        public void NoUpgraderWhenNuGetFeedMissing()
        {
            MockLocalScalarConfig scalarConfig = this.ConstructDefaultMockNuGetConfigBuilder()
                .WithNoUpgradeFeedUrl()
                .Build();

            bool success = ProductUpgrader.TryCreateUpgrader(
                this.tracer,
                this.fileSystemMock.Object,
                scalarConfig,
                this.credentialStoreMock.Object,
                false,
                false,
                out ProductUpgrader productUpgrader,
                out string error);

            success.ShouldBeFalse();
            productUpgrader.ShouldBeNull();
            error.ShouldNotBeNull();
        }

        [TestCase]
        public void NoOrgUpgraderWhenNuGetPackNameMissing()
        {
            MockLocalScalarConfig scalarConfig = this.ConstructDefaultMockOrgNuGetConfigBuilder()
                .WithNoUpgradeFeedPackageName()
                .Build();

            bool success = ProductUpgrader.TryCreateUpgrader(
                this.tracer,
                this.fileSystemMock.Object,
                scalarConfig,
                this.credentialStoreMock.Object,
                false,
                false,
                out ProductUpgrader productUpgrader,
                out string error);

            success.ShouldBeFalse();
            productUpgrader.ShouldBeNull();
            error.ShouldNotBeNull();
        }

        [TestCase]
        public void NoOrgUpgraderWhenNuGetFeedMissing()
        {
            MockLocalScalarConfig scalarConfig = this.ConstructDefaultMockOrgNuGetConfigBuilder()
                .WithNoUpgradeFeedUrl()
                .Build();

            bool success = ProductUpgrader.TryCreateUpgrader(
                this.tracer,
                this.fileSystemMock.Object,
                scalarConfig,
                this.credentialStoreMock.Object,
                false,
                false,
                out ProductUpgrader productUpgrader,
                out string error);

            success.ShouldBeFalse();
            productUpgrader.ShouldBeNull();
            error.ShouldNotBeNull();
        }

        [TestCase]
        public void NoUpgraderWhenNuGetPackNameMissing()
        {
            MockLocalScalarConfig scalarConfig = this.ConstructDefaultMockNuGetConfigBuilder()
                .WithNoUpgradeFeedPackageName()
                .Build();

            bool success = ProductUpgrader.TryCreateUpgrader(
                this.tracer,
                this.fileSystemMock.Object,
                scalarConfig,
                this.credentialStoreMock.Object,
                false,
                false,
                out ProductUpgrader productUpgrader,
                out string error);

            success.ShouldBeFalse();
            productUpgrader.ShouldBeNull();
            error.ShouldNotBeNull();
        }

        private MockLocalScalarConfigBuilder ConstructDefaultMockNuGetConfigBuilder()
        {
            MockLocalScalarConfigBuilder configBuilder = this.ConstructMockLocalScalarConfigBuilder()
                .WithUpgradeRing()
                .WithUpgradeFeedPackageName()
                .WithUpgradeFeedUrl();

            return configBuilder;
        }

        private MockLocalScalarConfigBuilder ConstructDefaultMockOrgNuGetConfigBuilder()
        {
            MockLocalScalarConfigBuilder configBuilder = this.ConstructMockLocalScalarConfigBuilder()
                .WithUpgradeRing()
                .WithUpgradeFeedPackageName()
                .WithUpgradeFeedUrl()
                .WithOrgInfoServerUrl();

            return configBuilder;
        }

        private MockLocalScalarConfigBuilder ConstructDefaultGitHubConfigBuilder()
        {
            MockLocalScalarConfigBuilder configBuilder = this.ConstructMockLocalScalarConfigBuilder()
                            .WithUpgradeRing();

            return configBuilder;
        }

        private MockLocalScalarConfigBuilder ConstructMockLocalScalarConfigBuilder()
        {
            return new MockLocalScalarConfigBuilder(
                defaultRing,
                defaultUpgradeFeedUrl,
                defaultUpgradeFeedPackageName,
                defaultOrgInfoServerUrl);
        }
    }
}
