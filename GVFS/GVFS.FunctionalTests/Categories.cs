﻿namespace GVFS.FunctionalTests
{
    public static class Categories
    {
        public const string ExtraCoverage = "ExtraCoverage";
        public const string FastFetch = "FastFetch";
        public const string GitCommands = "GitCommands";

        public const string WindowsOnly = "WindowsOnly";
        public const string MacOnly = "MacOnly";

        public const string NeedsUpdatesForNonVirtualizedMode = "NeedsUpdatesForNonVirtualizedMode";

        public static class MacTODO
        {
            // Tests that require #360 (detecting/handling new empty folders)
            public const string NeedsNewFolderCreateNotification = "NeedsNewFolderCreateNotification";

            // Tests that require the Status Cache to be built
            public const string NeedsStatusCache = "NeedsStatusCache";

            // Tests that require Config to be built
            public const string NeedsGVFSConfig = "NeedsConfig";

            // Tests that require VFS Service
            public const string NeedsServiceVerb = "NeedsServiceVerb";

            // Requires both functional and test fixes
            public const string NeedsDehydrate = "NeedsDehydrate";

            // Tests requires code updates so that we lock the file instead of looking for a .lock file
            public const string TestNeedsToLockFile = "TestNeedsToLockFile";

            // Tests that have been flaky on build servers and need additional logging and\or
            // investigation
            public const string FlakyTest = "MacFlakyTest";
        }
    }
}
