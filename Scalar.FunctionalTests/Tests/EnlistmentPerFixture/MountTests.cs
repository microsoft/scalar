using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Properties;
using Scalar.FunctionalTests.Should;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System.Diagnostics;
using System.IO;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    [Category(Categories.ExtraCoverage)]
    [Category(Categories.NeedsUpdatesForNonVirtualizedMode)]
    public class MountTests : TestsWithEnlistmentPerFixture
    {
        private const int ScalarGenericError = 3;
        private const uint GenericRead = 2147483648;
        private const uint FileFlagBackupSemantics = 3355443;

        private FileSystemRunner fileSystem;

        public MountTests()
        {
            this.fileSystem = new SystemIORunner();
        }

        [TestCaseSource(typeof(MountSubfolders), MountSubfolders.MountFolders)]
        public void SecondMountAttemptFails(string mountSubfolder)
        {
            this.MountShouldFail(0, "already mounted", this.Enlistment.GetSourcePath(mountSubfolder));
        }

        [TestCase]
        public void MountFailsOutsideEnlistment()
        {
            this.MountShouldFail("is not a valid Scalar enlistment", Path.GetDirectoryName(this.Enlistment.EnlistmentRoot));
        }

        [TestCase]
        public void MountCopiesMissingReadObjectHook()
        {
            this.Enlistment.UnmountScalar();

            string readObjectPath = this.Enlistment.GetSourcePath(".git", "hooks", "read-object" + Settings.Default.BinaryFileNameExtension);
            readObjectPath.ShouldBeAFile(this.fileSystem);
            this.fileSystem.DeleteFile(readObjectPath);
            readObjectPath.ShouldNotExistOnDisk(this.fileSystem);
            this.Enlistment.MountScalar();
            readObjectPath.ShouldBeAFile(this.fileSystem);
        }

        [TestCase]
        public void MountSetsCoreHooksPath()
        {
            this.Enlistment.UnmountScalar();

            GitProcess.Invoke(this.Enlistment.RepoRoot, "config --unset core.hookspath");
            string.IsNullOrWhiteSpace(
                GitProcess.Invoke(this.Enlistment.RepoRoot, "config core.hookspath"))
                .ShouldBeTrue();

            this.Enlistment.MountScalar();
            string expectedHooksPath = Path.Combine(this.Enlistment.RepoRoot, ".git", "hooks");
            expectedHooksPath = GitHelpers.ConvertPathToGitFormat(expectedHooksPath);

            GitProcess.Invoke(
                this.Enlistment.RepoRoot, "config core.hookspath")
                .Trim('\n')
                .ShouldEqual(expectedHooksPath);
        }

        [TestCase]
        public void MountChangesMountId()
        {
            string mountId = GitProcess.Invoke(this.Enlistment.RepoRoot, "config scalar.mount-id")
                .Trim('\n');
            this.Enlistment.UnmountScalar();
            this.Enlistment.MountScalar();
            GitProcess.Invoke(this.Enlistment.RepoRoot, "config scalar.mount-id")
                .Trim('\n')
                .ShouldNotEqual(mountId, "scalar.mount-id should change on every mount");
        }

        [TestCase]
        public void MountFailsWhenNoOnDiskVersion()
        {
            this.Enlistment.UnmountScalar();

            // Get the current disk layout version
            string majorVersion;
            string minorVersion;
            ScalarHelpers.GetPersistedDiskLayoutVersion(this.Enlistment.DotScalarRoot, out majorVersion, out minorVersion);

            int majorVersionNum;
            int minorVersionNum;
            int.TryParse(majorVersion.ShouldNotBeNull(), out majorVersionNum).ShouldEqual(true);
            int.TryParse(minorVersion.ShouldNotBeNull(), out minorVersionNum).ShouldEqual(true);

            // Move the RepoMetadata database to a temp file
            string versionDatabasePath = Path.Combine(this.Enlistment.DotScalarRoot, ScalarHelpers.RepoMetadataName);
            versionDatabasePath.ShouldBeAFile(this.fileSystem);

            string tempDatabasePath = versionDatabasePath + "_MountFailsWhenNoOnDiskVersion";
            tempDatabasePath.ShouldNotExistOnDisk(this.fileSystem);

            this.fileSystem.MoveFile(versionDatabasePath, tempDatabasePath);
            versionDatabasePath.ShouldNotExistOnDisk(this.fileSystem);

            this.MountShouldFail("Failed to upgrade repo disk layout");

            // Move the RepoMetadata database back
            this.fileSystem.DeleteFile(versionDatabasePath);
            this.fileSystem.MoveFile(tempDatabasePath, versionDatabasePath);
            tempDatabasePath.ShouldNotExistOnDisk(this.fileSystem);
            versionDatabasePath.ShouldBeAFile(this.fileSystem);

            this.Enlistment.MountScalar();
        }

        [TestCase]
        public void MountFailsWhenNoLocalCacheRootInRepoMetadata()
        {
            this.Enlistment.UnmountScalar();

            string majorVersion;
            string minorVersion;
            ScalarHelpers.GetPersistedDiskLayoutVersion(this.Enlistment.DotScalarRoot, out majorVersion, out minorVersion);
            majorVersion.ShouldNotBeNull();
            minorVersion.ShouldNotBeNull();

            string objectsRoot = ScalarHelpers.GetPersistedGitObjectsRoot(this.Enlistment.DotScalarRoot).ShouldNotBeNull();

            string metadataPath = Path.Combine(this.Enlistment.DotScalarRoot, ScalarHelpers.RepoMetadataName);
            string metadataBackupPath = metadataPath + ".backup";
            this.fileSystem.MoveFile(metadataPath, metadataBackupPath);

            this.fileSystem.CreateEmptyFile(metadataPath);
            ScalarHelpers.SaveDiskLayoutVersion(this.Enlistment.DotScalarRoot, majorVersion, minorVersion);
            ScalarHelpers.SaveGitObjectsRoot(this.Enlistment.DotScalarRoot, objectsRoot);

            this.MountShouldFail("Failed to determine local cache path from repo metadata");

            this.fileSystem.DeleteFile(metadataPath);
            this.fileSystem.MoveFile(metadataBackupPath, metadataPath);

            this.Enlistment.MountScalar();
        }

        [TestCase]
        public void MountFailsWhenNoGitObjectsRootInRepoMetadata()
        {
            this.Enlistment.UnmountScalar();

            string majorVersion;
            string minorVersion;
            ScalarHelpers.GetPersistedDiskLayoutVersion(this.Enlistment.DotScalarRoot, out majorVersion, out minorVersion);
            majorVersion.ShouldNotBeNull();
            minorVersion.ShouldNotBeNull();

            string localCacheRoot = ScalarHelpers.GetPersistedLocalCacheRoot(this.Enlistment.DotScalarRoot).ShouldNotBeNull();

            string metadataPath = Path.Combine(this.Enlistment.DotScalarRoot, ScalarHelpers.RepoMetadataName);
            string metadataBackupPath = metadataPath + ".backup";
            this.fileSystem.MoveFile(metadataPath, metadataBackupPath);

            this.fileSystem.CreateEmptyFile(metadataPath);
            ScalarHelpers.SaveDiskLayoutVersion(this.Enlistment.DotScalarRoot, majorVersion, minorVersion);
            ScalarHelpers.SaveLocalCacheRoot(this.Enlistment.DotScalarRoot, localCacheRoot);

            this.MountShouldFail("Failed to determine git objects root from repo metadata");

            this.fileSystem.DeleteFile(metadataPath);
            this.fileSystem.MoveFile(metadataBackupPath, metadataPath);

            this.Enlistment.MountScalar();
        }

        [TestCase]
        public void MountRegeneratesAlternatesFileWhenMissingGitObjectsRoot()
        {
            this.Enlistment.UnmountScalar();

            string objectsRoot = ScalarHelpers.GetPersistedGitObjectsRoot(this.Enlistment.DotScalarRoot).ShouldNotBeNull();

            string alternatesFilePath = Path.Combine(this.Enlistment.RepoRoot, ".git", "objects", "info", "alternates");
            alternatesFilePath.ShouldBeAFile(this.fileSystem).WithContents(objectsRoot);
            this.fileSystem.WriteAllText(alternatesFilePath, "Z:\\invalidPath");

            this.Enlistment.MountScalar();

            alternatesFilePath.ShouldBeAFile(this.fileSystem).WithContents(objectsRoot);
        }

        [TestCase]
        public void MountRegeneratesAlternatesFileWhenMissingFromDisk()
        {
            this.Enlistment.UnmountScalar();

            string objectsRoot = ScalarHelpers.GetPersistedGitObjectsRoot(this.Enlistment.DotScalarRoot).ShouldNotBeNull();

            string alternatesFilePath = Path.Combine(this.Enlistment.RepoRoot, ".git", "objects", "info", "alternates");
            alternatesFilePath.ShouldBeAFile(this.fileSystem).WithContents(objectsRoot);
            this.fileSystem.DeleteFile(alternatesFilePath);

            this.Enlistment.MountScalar();

            alternatesFilePath.ShouldBeAFile(this.fileSystem).WithContents(objectsRoot);
        }

        [TestCaseSource(typeof(MountSubfolders), MountSubfolders.MountFolders)]
        public void MountFailsAfterBreakingDowngrade(string mountSubfolder)
        {
            MountSubfolders.EnsureSubfoldersOnDisk(this.Enlistment, this.fileSystem);
            this.Enlistment.UnmountScalar();

            string majorVersion;
            string minorVersion;
            ScalarHelpers.GetPersistedDiskLayoutVersion(this.Enlistment.DotScalarRoot, out majorVersion, out minorVersion);

            int majorVersionNum;
            int minorVersionNum;
            int.TryParse(majorVersion.ShouldNotBeNull(), out majorVersionNum).ShouldEqual(true);
            int.TryParse(minorVersion.ShouldNotBeNull(), out minorVersionNum).ShouldEqual(true);

            ScalarHelpers.SaveDiskLayoutVersion(this.Enlistment.DotScalarRoot, (majorVersionNum + 1).ToString(), "0");

            this.MountShouldFail("do not allow mounting after downgrade", this.Enlistment.GetSourcePath(mountSubfolder));

            ScalarHelpers.SaveDiskLayoutVersion(this.Enlistment.DotScalarRoot, majorVersionNum.ToString(), minorVersionNum.ToString());
            this.Enlistment.MountScalar();
        }

        [TestCaseSource(typeof(MountSubfolders), MountSubfolders.MountFolders)]
        public void MountFailsUpgradingFromInvalidUpgradePath(string mountSubfolder)
        {
            MountSubfolders.EnsureSubfoldersOnDisk(this.Enlistment, this.fileSystem);
            string headCommitId = GitProcess.Invoke(this.Enlistment.RepoRoot, "rev-parse HEAD");

            this.Enlistment.UnmountScalar();

            string majorVersion;
            string minorVersion;
            ScalarHelpers.GetPersistedDiskLayoutVersion(this.Enlistment.DotScalarRoot, out majorVersion, out minorVersion);

            int majorVersionNum;
            int minorVersionNum;
            int.TryParse(majorVersion.ShouldNotBeNull(), out majorVersionNum).ShouldEqual(true);
            int.TryParse(minorVersion.ShouldNotBeNull(), out minorVersionNum).ShouldEqual(true);

            // 1 will always be below the minumum support version number
            ScalarHelpers.SaveDiskLayoutVersion(this.Enlistment.DotScalarRoot, "1", "0");
            this.MountShouldFail("Breaking change to Scalar disk layout has been made since cloning", this.Enlistment.GetSourcePath(mountSubfolder));

            ScalarHelpers.SaveDiskLayoutVersion(this.Enlistment.DotScalarRoot, majorVersionNum.ToString(), minorVersionNum.ToString());
            this.Enlistment.MountScalar();
        }

        private void MountShouldFail(int expectedExitCode, string expectedErrorMessage, string mountWorkingDirectory = null)
        {
            string enlistmentRoot = this.Enlistment.EnlistmentRoot;

            // TODO: 865304 Use app.config instead of --internal* arguments
            ProcessStartInfo processInfo = new ProcessStartInfo(ScalarTestConfig.PathToScalar);
            processInfo.Arguments = "mount " + TestConstants.InternalUseOnlyFlag + " " + ScalarHelpers.GetInternalParameter();
            processInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processInfo.WorkingDirectory = string.IsNullOrEmpty(mountWorkingDirectory) ? enlistmentRoot : mountWorkingDirectory;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;

            ProcessResult result = ProcessHelper.Run(processInfo);
            result.ExitCode.ShouldEqual(expectedExitCode, $"mount exit code was not {expectedExitCode}. Output: {result.Output}");
            result.Output.ShouldContain(expectedErrorMessage);
        }

        private void MountShouldFail(string expectedErrorMessage, string mountWorkingDirectory = null)
        {
            this.MountShouldFail(ScalarGenericError, expectedErrorMessage, mountWorkingDirectory);
        }

        private class MountSubfolders
        {
            public const string MountFolders = "Folders";
            private static object[] mountFolders =
            {
                new object[] { string.Empty },
                new object[] { "GVFS" },
            };

            public static object[] Folders
            {
                get
                {
                    return mountFolders;
                }
            }

            public static void EnsureSubfoldersOnDisk(ScalarFunctionalTestEnlistment enlistment, FileSystemRunner fileSystem)
            {
                // Enumerate the directory to ensure that the folder is on disk after Scalar is unmounted
                foreach (object[] folder in Folders)
                {
                    string folderPath = enlistment.GetSourcePath((string)folder[0]);
                    folderPath.ShouldBeADirectory(fileSystem).WithItems();
                }
            }
        }
    }
}
