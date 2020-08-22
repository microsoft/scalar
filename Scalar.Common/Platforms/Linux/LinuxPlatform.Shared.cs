using System;
using System.IO;
using Scalar.Common;
using Scalar.Platform.POSIX;

namespace Scalar.Platform.Linux
{
    public partial class LinuxPlatform
    {
        public static string GetDataRootForScalarImplementation()
        {
            string localDataRoot;
            string localDataRootError;

            if (!TryGetEnvironmentVariableBasePath(EnvironmentVariableBaseDataPaths, out localDataRoot, out localDataRootError))
            {
                throw new ArgumentException(localDataRootError);
            }

            return localDataRoot;
        }

        public static string GetDataRootForScalarComponentImplementation(string componentName)
        {
            return Path.Combine(GetDataRootForScalarImplementation(), componentName);
        }

        public static string GetUpgradeHighestAvailableVersionDirectoryImplementation()
        {
            return GetUpgradeNonProtectedDirectoryImplementation();
        }

        public static string GetUpgradeNonProtectedDirectoryImplementation()
        {
            return Path.Combine(GetDataRootForScalarImplementation(), ProductUpgraderInfo.UpgradeDirectoryName);
        }

        private string GetUpgradeNonProtectedDataDirectory()
        {
            return GetUpgradeNonProtectedDirectoryImplementation();
        }
    }
}
