using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;

namespace Scalar.UnitTests.Mock.Upgrader
{
    public class MockGitHubUpgrader : GitHubUpgrader
    {
        private string expectedScalarAssetName;
        private string expectedGitAssetName;
        private ActionType failActionTypes;

        public MockGitHubUpgrader(
            string currentVersion,
            ITracer tracer,
            PhysicalFileSystem fileSystem,
            GitHubUpgraderConfig config) : base(currentVersion, tracer, fileSystem, config)
        {
            this.DownloadedFiles = new List<string>();
            this.InstallerArgs = new Dictionary<string, Dictionary<string, string>>();
        }

        [Flags]
        public enum ActionType
        {
            Invalid = 0,
            FetchReleaseInfo = 0x1,
            CopyTools = 0x2,
            GitDownload = 0x4,
            ScalarDownload = 0x8,
            GitInstall = 0x10,
            ScalarInstall = 0x20,
            ScalarCleanup = 0x40,
            GitCleanup = 0x80,
            GitAuthenticodeCheck = 0x100,
            ScalarAuthenticodeCheck = 0x200,
            CreateDownloadDirectory = 0x400,
        }

        public List<string> DownloadedFiles { get; private set; }
        public Dictionary<string, Dictionary<string, string>> InstallerArgs { get; private set; }
        public bool InstallerExeLaunched { get; set; }
        private Release FakeUpgradeRelease { get; set; }

        public void SetDryRun(bool dryRun)
        {
            this.dryRun = dryRun;
        }

        public void SetFailOnAction(ActionType failureType)
        {
            this.failActionTypes |= failureType;
        }

        public void SetSucceedOnAction(ActionType failureType)
        {
            this.failActionTypes &= ~failureType;
        }

        public void ResetFailedAction()
        {
            this.failActionTypes = ActionType.Invalid;
        }

        public void PretendNewReleaseAvailableAtRemote(string upgradeVersion, GitHubUpgraderConfig.RingType remoteRing)
        {
            string assetDownloadURLPrefix = "https://github.com/Microsoft/Scalar/releases/download/v" + upgradeVersion;
            Release release = new Release();

            release.Name = "Scalar " + upgradeVersion;
            release.Tag = "v" + upgradeVersion;
            release.PreRelease = remoteRing == GitHubUpgraderConfig.RingType.Fast;
            release.Assets = new List<Asset>();

            Random random = new Random();
            Asset scalarAsset = new Asset();
            scalarAsset.Name = "Scalar." + upgradeVersion + ScalarPlatform.Instance.Constants.InstallerExtension;

            // This is not cross-checked anywhere, random value is good.
            scalarAsset.Size = random.Next(int.MaxValue / 10, int.MaxValue / 2);
            scalarAsset.DownloadURL = new Uri(assetDownloadURLPrefix + "/Scalar." + upgradeVersion + ScalarPlatform.Instance.Constants.InstallerExtension);
            release.Assets.Add(scalarAsset);

            Asset gitAsset = new Asset();
            gitAsset.Name = "Git-2.17.1.scalar.2.1.4.g4385455-64-bit" + ScalarPlatform.Instance.Constants.InstallerExtension;
            gitAsset.Size = random.Next(int.MaxValue / 10, int.MaxValue / 2);
            gitAsset.DownloadURL = new Uri(assetDownloadURLPrefix + "/Git-2.17.1.scalar.2.1.4.g4385455-64-bit" + ScalarPlatform.Instance.Constants.InstallerExtension);
            release.Assets.Add(gitAsset);

            this.expectedScalarAssetName = scalarAsset.Name;
            this.expectedGitAssetName = gitAsset.Name;
            this.FakeUpgradeRelease = release;
        }

        public override bool TrySetupUpgradeApplicationDirectory(out string upgradeApplicationPath, out string error)
        {
            if (this.failActionTypes.HasFlag(ActionType.CopyTools))
            {
                upgradeApplicationPath = null;
                error = "Unable to copy upgrader tools";
                return false;
            }

            upgradeApplicationPath = @"mock:\ProgramData\Scalar\Scalar.Upgrade\Tools\Scalar.Upgrader.exe";
            error = null;
            return true;
        }

        protected override bool TryCreateAndConfigureDownloadDirectory(ITracer tracer, out string error)
        {
            if (this.failActionTypes.HasFlag(ActionType.CreateDownloadDirectory))
            {
                error = "Error creating download directory";
                return false;
            }

            error = null;
            return true;
        }

        protected override bool TryDownloadAsset(Asset asset, out string errorMessage)
        {
            bool validAsset = true;
            if (this.expectedScalarAssetName.Equals(asset.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (this.failActionTypes.HasFlag(ActionType.ScalarDownload))
                {
                    errorMessage = "Error downloading Scalar from GitHub";
                    return false;
                }
            }
            else if (this.expectedGitAssetName.Equals(asset.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (this.failActionTypes.HasFlag(ActionType.GitDownload))
                {
                    errorMessage = "Error downloading Git from GitHub";
                    return false;
                }
            }
            else
            {
                validAsset = false;
            }

            if (validAsset)
            {
                string fakeDownloadDirectory = @"mock:\ProgramData\Scalar\Scalar.Upgrade\Downloads";
                asset.LocalPath = Path.Combine(fakeDownloadDirectory, asset.Name);
                this.DownloadedFiles.Add(asset.LocalPath);

                errorMessage = null;
                return true;
            }

            errorMessage = "Cannot download unknown asset.";
            return false;
        }

        protected override bool TryDeleteDownloadedAsset(Asset asset, out Exception exception)
        {
            if (this.expectedScalarAssetName.Equals(asset.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (this.failActionTypes.HasFlag(ActionType.ScalarCleanup))
                {
                    exception = new Exception("Error deleting downloaded Scalar installer.");
                    return false;
                }

                exception = null;
                return true;
            }
            else if (this.expectedGitAssetName.Equals(asset.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (this.failActionTypes.HasFlag(ActionType.GitCleanup))
                {
                    exception = new Exception("Error deleting downloaded Git installer.");
                    return false;
                }

                exception = null;
                return true;
            }
            else
            {
                exception = new Exception("Unknown asset.");
                return false;
            }
        }

        protected override bool TryFetchReleases(out List<Release> releases, out string errorMessage)
        {
            if (this.failActionTypes.HasFlag(ActionType.FetchReleaseInfo))
            {
                releases = null;
                errorMessage = "Error fetching upgrade release info.";
                return false;
            }

            releases = new List<Release> { this.FakeUpgradeRelease };
            errorMessage = null;

            return true;
        }

        protected override void RunInstaller(string path, string args, string certCN, string issuerCN, out int exitCode, out string error)
        {
            string fileName = Path.GetFileName(path);
            Dictionary<string, string> installationInfo = new Dictionary<string, string>();
            installationInfo.Add("Installer", fileName);
            installationInfo.Add("Args", args);

            exitCode = 0;
            error = null;

            if (fileName.Equals(this.expectedGitAssetName, StringComparison.OrdinalIgnoreCase))
            {
                this.InstallerArgs.Add("Git", installationInfo);
                this.InstallerExeLaunched = true;
                if (this.failActionTypes.HasFlag(ActionType.GitInstall))
                {
                    exitCode = -1;
                    error = "Git installation failed";
                }

                if (this.failActionTypes.HasFlag(ActionType.GitAuthenticodeCheck))
                {
                    exitCode = -1;
                    error = "The contents of file C:\\ProgramData\\Scalar\\Scalar.Upgrade\\Tools\\Git-2.17.1.scalar.2.1.4.g4385455-64-bit might have been changed by an unauthorized user or process, because the hash of the file does not match the hash stored in the digital signature. The script cannot run on the specified system. For more information, run Get-Help about_Signing.";
                }

                return;
            }

            if (fileName.Equals(this.expectedScalarAssetName, StringComparison.OrdinalIgnoreCase))
            {
                this.InstallerArgs.Add("Scalar", installationInfo);
                this.InstallerExeLaunched = true;
                if (this.failActionTypes.HasFlag(ActionType.ScalarInstall))
                {
                    exitCode = -1;
                    error = "Scalar installation failed";
                }

                if (this.failActionTypes.HasFlag(ActionType.ScalarAuthenticodeCheck))
                {
                    exitCode = -1;
                    error = "The contents of file C:\\ProgramData\\Scalar\\Scalar.Upgrade\\Tools\\SetupScalar.1.0.18297.1.exe might have been changed by an unauthorized user or process, because the hash of the file does not match the hash stored in the digital signature. The script cannot run on the specified system. For more information, run Get-Help about_Signing.";
                }

                return;
            }

            exitCode = -1;
            error = "Cannot launch unknown installer";
            return;
        }
    }
}
