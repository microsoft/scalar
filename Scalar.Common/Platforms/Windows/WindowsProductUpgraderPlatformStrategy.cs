using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using System;
using System.IO;

namespace Scalar.Platform.Windows
{
    public class WindowsProductUpgraderPlatformStrategy : ProductUpgraderPlatformStrategy
    {
        public WindowsProductUpgraderPlatformStrategy(PhysicalFileSystem fileSystem, ITracer tracer)
        : base(fileSystem, tracer)
        {
        }

        public override bool TryPrepareLogDirectory(out string error)
        {
            // Under normal circumstances
            // ProductUpgraderInfo.GetLogDirectoryPath will have
            // already been created by Scalar.Service.  If for some
            // reason it does not (e.g. the service failed to start),
            // we need to create
            // ProductUpgraderInfo.GetLogDirectoryPath() explicity to
            // ensure that it has the correct ACLs (so that both admin
            // and non-admin users can create log files).  If the logs
            // directory does not already exist, this call could fail
            // when running as a non-elevated user.
            string createDirectoryError;
            if (!this.FileSystem.TryCreateDirectoryWithAdminAndUserModifyPermissions(ProductUpgraderInfo.GetLogDirectoryPath(), out createDirectoryError))
            {
                error = $"ERROR: Unable to create directory `{ProductUpgraderInfo.GetLogDirectoryPath()}`";
                error += $"\n{createDirectoryError}";
                error += $"\n\nTry running {ScalarConstants.UpgradeVerbMessages.ScalarUpgrade} from an elevated command prompt.";
                return false;
            }

            error = null;
            return true;
        }

        public override bool TryPrepareApplicationDirectory(out string error)
        {
            string upgradeApplicationDirectory = ProductUpgraderInfo.GetUpgradeApplicationDirectory();

            Exception deleteDirectoryException;
            if (this.FileSystem.DirectoryExists(upgradeApplicationDirectory) &&
                !this.FileSystem.TryDeleteDirectory(upgradeApplicationDirectory, out deleteDirectoryException))
            {
                error = $"Failed to delete {upgradeApplicationDirectory} - {deleteDirectoryException.Message}";

                this.TraceException(deleteDirectoryException, nameof(this.TryPrepareApplicationDirectory), $"Error deleting {upgradeApplicationDirectory}.");
                return false;
            }

            if (!this.FileSystem.TryCreateOrUpdateDirectoryToAdminModifyPermissions(
                    this.Tracer,
                    upgradeApplicationDirectory,
                    out error))
            {
                return false;
            }

            error = null;
            return true;
        }

        public override bool TryPrepareDownloadDirectory(out string error)
        {
            return this.FileSystem.TryCreateOrUpdateDirectoryToAdminModifyPermissions(
                this.Tracer,
                ProductUpgraderInfo.GetAssetDownloadsPath(),
                out error);
        }
    }
}
