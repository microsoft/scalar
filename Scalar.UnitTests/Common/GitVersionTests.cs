using NUnit.Framework;
using Scalar.Common;
using Scalar.Common.Git;
using Scalar.Tests.Should;
using System.Collections.Generic;

namespace Scalar.UnitTests.Common
{
    [TestFixture]
    public class GitVersionTests
    {
        [TestCase]
        public void GetFeatureFlags_VfsGitVersion_ReturnsGvfsProtocolSupported()
        {
            var version = new GitVersion(2, 28, 0, "vfs", 1, 0);
            GitFeatureFlags features = version.GetFeatures();
            features.HasFlag(GitFeatureFlags.GvfsProtocol).ShouldBeTrue();
        }

        [TestCase]
        public void GetFeatureFlags_NormalGitVersion_ReturnsGvfsProtocolNotSupported()
        {
            var gitGitVersion = new GitVersion(2, 28, 0);
            GitFeatureFlags gitGitFeatures = gitGitVersion.GetFeatures();
            gitGitFeatures.HasFlag(GitFeatureFlags.GvfsProtocol).ShouldBeFalse();

            var winGitVersion = new GitVersion(2, 28, 0, "windows", 1, 1);
            GitFeatureFlags winGitFeatures = winGitVersion.GetFeatures();
            winGitFeatures.HasFlag(GitFeatureFlags.GvfsProtocol).ShouldBeFalse();
        }

        [TestCase]
        public void GetFeatureFlags_MaintenanceBuiltin()
        {
            var notSupportedVerisons = new List<GitVersion>
            {
                new GitVersion(2, 27, 1),
                new GitVersion(2, 28, 0),
                new GitVersion(2, 28, 1),
                new GitVersion(2, 29, 0),
                new GitVersion(2, 27, 1, "windows"),
                new GitVersion(2, 28, 0, "windows"),
                new GitVersion(2, 28, 1, "windows"),
                new GitVersion(2, 29, 0, "windows"),
                new GitVersion(2, 27, 0, "vfs", 1, 1),
                new GitVersion(2, 28, 0, "vfs", 0, 0),
                new GitVersion(2, 28, 0, "vfs", 0, 1),
            };

            foreach (GitVersion version in notSupportedVerisons)
            {
                GitFeatureFlags gitGitFeatures = version.GetFeatures();
                gitGitFeatures.HasFlag(GitFeatureFlags.MaintenanceBuiltin).ShouldBeFalse($"Incorrect for version {version}");
            }

            var supportedVerisons = new List<GitVersion>
            {
                new GitVersion(2, 28, 0, "vfs", 1, 0),
                new GitVersion(2, 29, 0, "vfs", 0, 0),
                new GitVersion(2, 30, 0, "vfs", 0, 0),
            };

            foreach (GitVersion version in supportedVerisons)
            {
                GitFeatureFlags gitGitFeatures = version.GetFeatures();
                gitGitFeatures.HasFlag(GitFeatureFlags.MaintenanceBuiltin).ShouldBeTrue($"Incorrect for version {version}");
            }
        }

        [TestCase]
        public void GetFeatureFlags_BuiltinFSMonitor()
        {
            var notSupportedVerisons = new List<GitVersion>
            {
                new GitVersion(2, 31, 0),
                new GitVersion(2, 29, 0, "windows"),
                new GitVersion(2, 30, 0, "vfs", 0, 1),
            };

            foreach (GitVersion version in notSupportedVerisons)
            {
                GitFeatureFlags gitGitFeatures = version.GetFeatures();
                gitGitFeatures.HasFlag(GitFeatureFlags.BuiltinFSMonitor).ShouldBeFalse($"Incorrect for version {version}");
            }

            var supportedVerisons = new List<GitVersion>
            {
                new GitVersion(2, 30, 0, "vfs", 0, 0, extra: "exp"),
            };

            foreach (GitVersion version in supportedVerisons)
            {
                GitFeatureFlags gitGitFeatures = version.GetFeatures();
                gitGitFeatures.HasFlag(GitFeatureFlags.BuiltinFSMonitor).ShouldBeTrue($"Incorrect for version {version}");
            }
        }

        [TestCase]
        public void TryParseInstallerName()
        {
            this.ParseAndValidateInstallerVersion("Git-1.2.3.scalar.4.5.gb16030b-64-bit" + ScalarPlatform.Instance.Constants.InstallerExtension);
            this.ParseAndValidateInstallerVersion("git-1.2.3.scalar.4.5.gb16030b-64-bit" + ScalarPlatform.Instance.Constants.InstallerExtension);
            this.ParseAndValidateInstallerVersion("Git-1.2.3.scalar.4.5.gb16030b-64-bit" + ScalarPlatform.Instance.Constants.InstallerExtension);
        }

        [TestCase]
        public void Version_Data_Null_Returns_False()
        {
            GitVersion version;
            bool success = GitVersion.TryParseVersion(null, out version);
            success.ShouldEqual(false);
        }

        [TestCase]
        public void Version_Data_Empty_Returns_False()
        {
            GitVersion version;
            bool success = GitVersion.TryParseVersion(string.Empty, out version);
            success.ShouldEqual(false);
        }

        [TestCase]
        public void Version_Data_Not_Enough_Numbers_Returns_False()
        {
            GitVersion version;
            bool success = GitVersion.TryParseVersion("2.0", out version);
            success.ShouldEqual(false);
        }

        [TestCase]
        public void Version_Data_Too_Many_Numbers_Returns_True()
        {
            GitVersion version;
            bool success = GitVersion.TryParseVersion("2.0.1.test.1.4.3.6", out version);
            success.ShouldEqual(true);
        }

        [TestCase]
        public void Version_Data_Valid_Returns_True()
        {
            GitVersion version;
            bool success = GitVersion.TryParseVersion("2.0.1", out version);
            success.ShouldEqual(true);
        }

        [TestCase]
        public void Version_Data_Valid_With_RC_Returns_True()
        {
            GitVersion version;
            bool success = GitVersion.TryParseVersion("2.0.1-rc3", out version);
            success.ShouldEqual(true);
        }

        [TestCase]
        public void Version_Data_Valid_With_Platform_Returns_True()
        {
            GitVersion version;
            bool success = GitVersion.TryParseVersion("2.0.1.test.1.2", out version);
            success.ShouldEqual(true);
        }

        [TestCase]
        public void Version_Data_Valid_With_RC_And_Platform_Returns_True()
        {
            GitVersion version;
            bool success = GitVersion.TryParseVersion("2.0.1-rc3.test.1.2", out version);
            success.ShouldEqual(true);
        }

        [TestCase]
        public void Compare_Different_Platforms_Returns_False()
        {
            GitVersion version1 = new GitVersion(1, 2, 3, "test", 4, 1);
            GitVersion version2 = new GitVersion(1, 2, 3, "test1", 4, 1);

            version1.IsLessThan(version2).ShouldEqual(false);
            version1.IsEqualTo(version2).ShouldEqual(false);
        }

        [TestCase]
        public void Compare_Version_Equal()
        {
            GitVersion version1 = new GitVersion(1, 2, 3, "test", 4, 1);
            GitVersion version2 = new GitVersion(1, 2, 3, "test", 4, 1);

            version1.IsLessThan(version2).ShouldEqual(false);
            version1.IsEqualTo(version2).ShouldEqual(true);
        }

        [TestCase]
        public void Compare_Version_Major_Less()
        {
            GitVersion version1 = new GitVersion(0, 2, 3, "test", 4, 1);
            GitVersion version2 = new GitVersion(1, 2, 3, "test", 4, 1);

            version1.IsLessThan(version2).ShouldEqual(true);
            version1.IsEqualTo(version2).ShouldEqual(false);
        }

        [TestCase]
        public void Compare_Version_Major_Greater()
        {
            GitVersion version1 = new GitVersion(2, 2, 3, "test", 4, 1);
            GitVersion version2 = new GitVersion(1, 2, 3, "test", 4, 1);

            version1.IsLessThan(version2).ShouldEqual(false);
            version1.IsEqualTo(version2).ShouldEqual(false);
        }

        [TestCase]
        public void Compare_Version_Minor_Less()
        {
            GitVersion version1 = new GitVersion(1, 1, 3, "test", 4, 1);
            GitVersion version2 = new GitVersion(1, 2, 3, "test", 4, 1);

            version1.IsLessThan(version2).ShouldEqual(true);
            version1.IsEqualTo(version2).ShouldEqual(false);
        }

        [TestCase]
        public void Compare_Version_Minor_Greater()
        {
            GitVersion version1 = new GitVersion(1, 3, 3, "test", 4, 1);
            GitVersion version2 = new GitVersion(1, 2, 3, "test", 4, 1);

            version1.IsLessThan(version2).ShouldEqual(false);
            version1.IsEqualTo(version2).ShouldEqual(false);
        }

        [TestCase]
        public void Compare_Version_Build_Less()
        {
            GitVersion version1 = new GitVersion(1, 2, 2, "test", 4, 1);
            GitVersion version2 = new GitVersion(1, 2, 3, "test", 4, 1);

            version1.IsLessThan(version2).ShouldEqual(true);
            version1.IsEqualTo(version2).ShouldEqual(false);
        }

        [TestCase]
        public void Compare_Version_Build_Greater()
        {
            GitVersion version1 = new GitVersion(1, 2, 4, "test", 4, 1);
            GitVersion version2 = new GitVersion(1, 2, 3, "test", 4, 1);

            version1.IsLessThan(version2).ShouldEqual(false);
            version1.IsEqualTo(version2).ShouldEqual(false);
        }

        [TestCase]
        public void Compare_Version_Revision_Less()
        {
            GitVersion version1 = new GitVersion(1, 2, 3, "test", 3, 1);
            GitVersion version2 = new GitVersion(1, 2, 3, "test", 4, 1);

            version1.IsLessThan(version2).ShouldEqual(true);
            version1.IsEqualTo(version2).ShouldEqual(false);
        }

        [TestCase]
        public void Compare_Version_Revision_Greater()
        {
            GitVersion version1 = new GitVersion(1, 2, 3, "test", 5, 1);
            GitVersion version2 = new GitVersion(1, 2, 3, "test", 4, 1);

            version1.IsLessThan(version2).ShouldEqual(false);
            version1.IsEqualTo(version2).ShouldEqual(false);
        }

        [TestCase]
        public void Compare_Version_MinorRevision_Less()
        {
            GitVersion version1 = new GitVersion(1, 2, 3, "test", 4, 1);
            GitVersion version2 = new GitVersion(1, 2, 3, "test", 4, 2);

            version1.IsLessThan(version2).ShouldEqual(true);
            version1.IsEqualTo(version2).ShouldEqual(false);
        }

        [TestCase]
        public void Compare_Version_MinorRevision_Greater()
        {
            GitVersion version1 = new GitVersion(1, 2, 3, "test", 4, 2);
            GitVersion version2 = new GitVersion(1, 2, 3, "test", 4, 1);

            version1.IsLessThan(version2).ShouldEqual(false);
            version1.IsEqualTo(version2).ShouldEqual(false);
        }

        [TestCase]
        public void Allow_Blank_Minor_Revision()
        {
            GitVersion version;
            GitVersion.TryParseVersion("1.2.3.test.4", out version).ShouldEqual(true);

            version.Major.ShouldEqual(1);
            version.Minor.ShouldEqual(2);
            version.Build.ShouldEqual(3);
            version.ReleaseCandidate.ShouldEqual(null);
            version.Platform.ShouldEqual("test");
            version.Revision.ShouldEqual(4);
            version.MinorRevision.ShouldEqual(0);
        }

        [TestCase]
        public void Allow_Invalid_Minor_Revision()
        {
            GitVersion version;
            GitVersion.TryParseVersion("1.2.3.test.4.notint", out version).ShouldEqual(true);

            version.Major.ShouldEqual(1);
            version.Minor.ShouldEqual(2);
            version.Build.ShouldEqual(3);
            version.ReleaseCandidate.ShouldEqual(null);
            version.Platform.ShouldEqual("test");
            version.Revision.ShouldEqual(4);
            version.MinorRevision.ShouldEqual(0);
        }

        [TestCase]
        public void Allow_ReleaseCandidate()
        {
            GitVersion version;
            GitVersion.TryParseVersion("1.2.3-rc4", out version).ShouldEqual(true);

            version.Major.ShouldEqual(1);
            version.Minor.ShouldEqual(2);
            version.Build.ShouldEqual(3);
            version.ReleaseCandidate.ShouldEqual(4);
            version.Platform.ShouldBeNull();
            version.Revision.ShouldEqual(0);
            version.MinorRevision.ShouldEqual(0);
        }

        [TestCase]
        public void Allow_ReleaseCandidate_Platform()
        {
            GitVersion version;
            GitVersion.TryParseVersion("1.2.3-rc4.test", out version).ShouldEqual(true);

            version.Major.ShouldEqual(1);
            version.Minor.ShouldEqual(2);
            version.Build.ShouldEqual(3);
            version.ReleaseCandidate.ShouldEqual(4);
            version.Platform.ShouldEqual("test");
            version.Revision.ShouldEqual(0);
            version.MinorRevision.ShouldEqual(0);
        }

        [TestCase]
        public void Allow_LocalGitBuildVersion_ParseMajorMinorBuildOnly()
        {
            GitVersion version;
            GitVersion.TryParseVersion("1.2.3.456.abcdefg.hijk", out version).ShouldEqual(true);

            version.Major.ShouldEqual(1);
            version.Minor.ShouldEqual(2);
            version.Build.ShouldEqual(3);
            version.ReleaseCandidate.ShouldEqual(null);
            version.Platform.ShouldEqual("456");
            version.Revision.ShouldEqual(0);
            version.MinorRevision.ShouldEqual(0);
        }

        [TestCase]
        public void Allow_GarbageBuildVersion_ParseMajorMinorBuildOnly()
        {
            GitVersion version;
            GitVersion.TryParseVersion("1.2.3.test.4.5.6.7.g1234abcd.8.9.😀.10.11.dirty.MSVC", out version).ShouldEqual(true);

            version.Major.ShouldEqual(1);
            version.Minor.ShouldEqual(2);
            version.Build.ShouldEqual(3);
            version.ReleaseCandidate.ShouldEqual(null);
            version.Platform.ShouldEqual("test");
            version.Revision.ShouldEqual(4);
            version.MinorRevision.ShouldEqual(5);
        }

        private void ParseAndValidateInstallerVersion(string installerName)
        {
            GitVersion version;
            bool success = GitVersion.TryParseInstallerName(installerName, ScalarPlatform.Instance.Constants.InstallerExtension, out version);
            success.ShouldBeTrue();

            version.Major.ShouldEqual(1);
            version.Minor.ShouldEqual(2);
            version.Build.ShouldEqual(3);
            version.ReleaseCandidate.ShouldEqual(null);
            version.Platform.ShouldEqual("scalar");
            version.Revision.ShouldEqual(4);
            version.MinorRevision.ShouldEqual(5);
        }
    }
}
