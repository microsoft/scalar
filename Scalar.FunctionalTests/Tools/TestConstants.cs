using System.IO;

namespace Scalar.FunctionalTests.Tools
{
    public static class TestConstants
    {
        public const char GitPathSeparator = '/';
        public const string InternalUseOnlyFlag = "--internal_use_only";

        public const string DefaultScalarCacheFolderName = ".scalarCache";

        public static class POSIXPlatform
        {
            public static class EnvironmentVariables
            {
                public const string LocalUserFolder = "HOME";
            }
        }

        public static class LinuxPlatform
        {
            public static class EnvironmentVariables
            {
                public const string LocalUserCacheFolder = "XDG_CACHE_HOME";
            }

            public const string LocalScalarFolderName = "scalar";

            public static readonly string LocalScalarCachePath = Path.Combine(".cache", LocalScalarFolderName);
        }

        public static class DotGit
        {
            public const string Root = ".git";
            public static readonly string Head = Path.Combine(DotGit.Root, "HEAD");

            public static class Objects
            {
                public static readonly string Root = Path.Combine(DotGit.Root, "objects");
            }
        }
    }
}
