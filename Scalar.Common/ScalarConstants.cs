using System.IO;

namespace Scalar.Common
{
    public static partial class ScalarConstants
    {
        public const int ShaStringLength = 40;
        public const int MaxPath = 260;
        public const string AllZeroSha = "0000000000000000000000000000000000000000";

        public const char GitPathSeparator = '/';
        public const string GitPathSeparatorString = "/";
        public const char GitCommentSign = '#';

        public const string PrefetchPackPrefix = "prefetch";

        public const string ScalarEtwProviderName = "Microsoft.Git.Scalar";
        public const string WorkingDirectoryRootName = "src";
        public const string UnattendedEnvironmentVariable = "Scalar_UNATTENDED";

        public const string DefaultScalarCacheFolderName = ".scalarCache";

        public const string GitIsNotInstalledError = "Could not find git.exe.  Ensure that Git is installed.";

        public static partial class BundledBinaries
        {
            public const string NuGetFileName = "nuget";

            public const string GcmDirectoryName = "gcm";
            public const string GcmFileName = "git-credential-manager-core";
        }

        public static class GitConfig
        {
            public const string ScalarPrefix = "scalar.";
            public const string MaxRetriesConfig = ScalarPrefix + "max-retries";
            public const string TimeoutSecondsConfig = ScalarPrefix + "timeout-seconds";
            public const string GitStatusCacheBackoffConfig = ScalarPrefix + "status-cache-backoff-seconds";
            public const string EnlistmentId = ScalarPrefix + "enlistment-id";
            public const string CacheServer = "gvfs.cache-server";
            public const string ObjectCache = "gvfs.sharedCache";
            public const string ScalarTelemetryId = GitConfig.ScalarPrefix + "telemetry-id";
            public const string ScalarTelemetryPipe = GitConfig.ScalarPrefix + "telemetry-pipe";
            public const string IKey = GitConfig.ScalarPrefix + "ikey";
            public const string HooksExtension = ".hooks";
            public const string UseGvfsHelper = "core.useGvfsHelper";
        }

        public static class LocalScalarConfig
        {
            public const string UpgradeRing = "upgrade.ring";
            public const string UpgradeFeedPackageName = "upgrade.feedpackagename";
            public const string UpgradeFeedUrl = "upgrade.feedurl";
            public const string OrgInfoServerUrl = "upgrade.orgInfoServerUrl";
        }

        public static class Service
        {
            public const string ServiceName = "Scalar.Service";
            public const string LogDirectory = "Logs";
            public const string UIName = "Scalar.Service.UI";
        }

        public static class RepoRegistry
        {
            public const string RegistryDirectoryName = "Scalar.RepoRegistry";
        }

        public static class MediaTypes
        {
            public const string PrefetchPackFilesAndIndexesMediaType = "application/x-gvfs-timestamped-packfiles-indexes";
            public const string LooseObjectMediaType = "application/x-git-loose-object";
            public const string CustomLooseObjectsMediaType = "application/x-gvfs-loose-objects";
            public const string PackFileMediaType = "application/x-git-packfile";
        }

        public static class Endpoints
        {
            public const string ScalarConfig = "/gvfs/config";
            public const string ScalarObjects = "/gvfs/objects";
            public const string ScalarPrefetch = "/gvfs/prefetch";
            public const string ScalarSizes = "/gvfs/sizes";
            public const string InfoRefs = "/info/refs?service=git-upload-pack";
            public const string RepoInfo = "/vsts/info";
        }

        public static class SpecialGitFiles
        {
            public const string GitIgnore = ".gitignore";
        }

        public static class LogFileTypes
        {
            public const string UpgradePrefix = "productupgrade";

            public const string Clone = "clone";
            public const string Maintenance = "maintenance";
            public const string Repair = "repair";
            public const string Service = "service";
            public const string ServiceUI = "service_ui";
            public const string UpgradeVerb = UpgradePrefix + "_verb";
            public const string UpgradeProcess = UpgradePrefix + "_process";
        }

        public static class DotGit
        {
            public const string Root = ".git";
            public const string HeadName = "HEAD";
            public const string IndexName = "index";
            public const string PackedRefsName = "packed-refs";
            public const string LockExtension = ".lock";

            public static readonly string Config = Path.Combine(DotGit.Root, "config");
            public static readonly string Head = Path.Combine(DotGit.Root, HeadName);
            public static readonly string BisectStart = Path.Combine(DotGit.Root, "BISECT_START");
            public static readonly string CherryPickHead = Path.Combine(DotGit.Root, "CHERRY_PICK_HEAD");
            public static readonly string MergeHead = Path.Combine(DotGit.Root, "MERGE_HEAD");
            public static readonly string RevertHead = Path.Combine(DotGit.Root, "REVERT_HEAD");
            public static readonly string RebaseApply = Path.Combine(DotGit.Root, "rebase_apply");
            public static readonly string Index = Path.Combine(DotGit.Root, IndexName);
            public static readonly string IndexLock = Path.Combine(DotGit.Root, IndexName + LockExtension);
            public static readonly string PackedRefs = Path.Combine(DotGit.Root, PackedRefsName);
            public static readonly string Shallow = Path.Combine(DotGit.Root, "shallow");

            public static class Logs
            {
                public static readonly string HeadName = "HEAD";
                public static readonly string Name = "logs";

                public static readonly string Root = Path.Combine(DotGit.Root, Logs.Name);
                public static readonly string Head = Path.Combine(Logs.Root, Logs.HeadName);
            }

            public static class Hooks
            {
                public const string QueryWatchmanName = "query-watchman";
                public const string FsMonitorWatchmanSampleName = "fsmonitor-watchman.sample";

                public static readonly string Root = Path.Combine(DotGit.Root, "hooks");
                public static readonly string QueryWatchmanPath = Path.Combine(Hooks.Root, QueryWatchmanName);
                public static readonly string FsMonitorWatchmanSamplePath = Path.Combine(Hooks.Root, FsMonitorWatchmanSampleName);
            }

            public static class Info
            {
                public const string Name = "info";
                public const string SparseCheckoutName = "sparse-checkout";

                public static readonly string Root = Path.Combine(DotGit.Root, Info.Name);
                public static readonly string SparseCheckoutPath = Path.Combine(Info.Root, Info.SparseCheckoutName);
            }

            public static class Objects
            {
                public static readonly string Root = Path.Combine(DotGit.Root, "objects");

                public static class Info
                {
                    public static readonly string Root = Path.Combine(Objects.Root, "info");
                    public static readonly string Alternates = Path.Combine(Info.Root, "alternates");
                }

                public static class Pack
                {
                    public static readonly string Name = "pack";
                    public static readonly string Root = Path.Combine(Objects.Root, Name);
                }
            }

            public static class Refs
            {
                public static readonly string RefName = "refs";
                public static string Root => Path.Combine(DotGit.Root, RefName);

                public static class Heads
                {
                    public static readonly string Name = "heads";
                    public static string Root => Path.Combine(Refs.Root, Name);
                    public static string RefName => $"{Refs.RefName}/{Name}";
                }

                public static class Scalar
                {
                    public static readonly string Name = "scalar";
                    public static string Root => Path.Combine(Refs.Root, Name);
                    public static string RefName => $"{Refs.RefName}/{Name}";

                    public static class Hidden
                    {
                        public static readonly string Name = "hidden";
                        public static string Root => Path.Combine(Scalar.Root, Name);
                        public static string RefName => $"{Scalar.RefName}/{Name}";
                    }
                }
            }
        }

        public static class VerbParameters
        {
            public const string InternalUseOnly = "internal_use_only";

            public static class Maintenance
            {
                public const string AllTasksName = "all";
                public const string ConfigTaskName = "config";
                public const string CommitGraphTaskName = "commit-graph";
                public const string FetchTaskName = "fetch";
                public const string LooseObjectsTaskName = "loose-objects";
                public const string PackFilesTaskName = "pack-files";

                public const string BatchSizeOptionName = "batch-size";
            }
        }

        public static class UpgradeVerbMessages
        {
            public const string ScalarUpgrade = "`scalar upgrade`";
            public const string ScalarUpgradeConfirm = "`scalar upgrade --confirm`";
            public const string ScalarUpgradeDryRun = "`scalar upgrade --dry-run`";
            public const string NoUpgradeCheckPerformed = "No upgrade check was performed.";
            public const string NoneRingConsoleAlert = "Upgrade ring set to \"None\". " + NoUpgradeCheckPerformed;
            public const string NoRingConfigConsoleAlert = "Upgrade ring is not set. " + NoUpgradeCheckPerformed;
            public const string InvalidRingConsoleAlert = "Upgrade ring set to unknown value. " + NoUpgradeCheckPerformed;
            public const string SetUpgradeRingCommand = "To set or change upgrade ring, run `scalar config " + LocalScalarConfig.UpgradeRing + " [\"Fast\"|\"Slow\"|\"None\"]` from a command prompt.";
            public const string ReminderNotification = "A new version of Scalar is available. Run " + UpgradeVerbMessages.ScalarUpgradeConfirm + " from an elevated command prompt to upgrade.";
            public const string UpgradeInstallAdvice = "When ready, run " + UpgradeVerbMessages.ScalarUpgradeConfirm + " from an elevated command prompt.";
        }
    }
}
