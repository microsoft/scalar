namespace Scalar.FunctionalTests
{
    public static class Categories
    {
        public const string ExtraCoverage = "ExtraCoverage";
        public const string GitCommands = "GitCommands";

        public const string WindowsOnly = "WindowsOnly";
        public const string MacOnly = "MacOnly";

        public const string GitRepository = "GitRepository";

        public const string Stress = "Stress";

        public const string NeedsUpdatesForNonVirtualizedMode = "NeedsUpdatesForNonVirtualizedMode";

        public static class MacTODO
        {
            // Tests that require Config to be built
            public const string NeedsScalarConfig = "NeedsConfig";

            // Tests that require Scalar Service
            public const string NeedsServiceVerb = "NeedsServiceVerb";

            // Tests requires code updates so that we lock the file instead of looking for a .lock file
            public const string TestNeedsToLockFile = "TestNeedsToLockFile";
        }
    }
}
