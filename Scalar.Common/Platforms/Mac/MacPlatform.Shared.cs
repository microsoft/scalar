using System;
using System.IO;
using Scalar.Common;
using Scalar.Platform.POSIX;

namespace Scalar.Platform.Mac
{
    public partial class MacPlatform
    {
        public static string GetDataRootForScalarImplementation()
        {
            return Path.Combine(
                Environment.GetEnvironmentVariable("HOME"),
                "Library",
                "Application Support",
                "Scalar");
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
