using System.IO;

namespace Scalar.FunctionalTests.Tools
{
    public static class TestConstants
    {
        public const char GitPathSeparator = '/';
        public const string InternalUseOnlyFlag = "--internal_use_only";

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
