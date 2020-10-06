using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scalar.Common.Maintenance
{
    /// <summary>
    /// This step maintains the packfiles in the object cache.
    ///
    /// This is done in two steps:
    ///
    /// git multi-pack-index expire: This deletes the pack-files whose objects
    /// appear in newer pack-files. The multi-pack-index prevents git from
    /// looking at these packs. Rewrites the multi-pack-index to no longer
    /// refer to these (deleted) packs.
    ///
    /// git multi-pack-index repack --batch-size= inspects packs covered by the
    /// multi-pack-index in modified-time order(ascending). Greedily selects a
    /// batch of packs whose file sizes are all less than "size", but that sum
    /// up to at least "size". Then generate a new pack-file containing the
    /// objects that are uniquely referenced by the multi-pack-index.
    /// </summary>
    public class PackfileMaintenanceStep : GitMaintenanceStep
    {
        public const string PackfileLastRunFileName = "pack-maintenance.time";
        public const long DefaultBatchSizeBytes = 2 * 1024 * 1024 * 1024L;
        private const string MultiPackIndexLock = "multi-pack-index.lock";
        private readonly bool forceRun;
        private string batchSize;

        public PackfileMaintenanceStep(
            ScalarContext context,
            bool requireObjectCacheLock = true,
            bool forceRun = false,
            string batchSize = null,
            GitFeatureFlags gitFeatures = GitFeatureFlags.None,
            GitProcessChecker gitProcessChecker = null)
            : base(context, requireObjectCacheLock, gitFeatures, gitProcessChecker)
        {
            this.forceRun = forceRun;
            this.batchSize = batchSize ?? DefaultBatchSizeBytes.ToString();
        }

        public override string Area => nameof(PackfileMaintenanceStep);

        public override string ProgressMessage => "Cleaning up pack-files";

        protected override string LastRunTimeFilePath => Path.Combine(this.Context.Enlistment.GitObjectsRoot, "info", PackfileLastRunFileName);
        protected override TimeSpan TimeBetweenRuns => TimeSpan.FromDays(1);

        // public only for unit tests
        public List<string> CleanStaleIdxFiles(out int numDeletionBlocked)
        {
            List<DirectoryItemInfo> packDirContents = this.Context
                                                          .FileSystem
                                                          .ItemsInDirectory(this.Context.Enlistment.GitPackRoot)
                                                          .ToList();

            numDeletionBlocked = 0;
            List<string> deletedIdxFiles = new List<string>();

            // If something (probably Scalar) has a handle open to a ".idx" file, then
            // the 'git multi-pack-index expire' command cannot delete it. We should come in
            // later and try to clean these up. Count those that we are able to delete and
            // those we still can't.

            foreach (DirectoryItemInfo info in packDirContents)
            {
                if (string.Equals(Path.GetExtension(info.Name), ".idx", ScalarPlatform.Instance.Constants.PathComparison))
                {
                    string pairedPack = Path.ChangeExtension(info.FullName, ".pack");

                    if (!this.Context.FileSystem.FileExists(pairedPack))
                    {
                        if (this.Context.FileSystem.TryDeleteFile(info.FullName))
                        {
                            deletedIdxFiles.Add(info.Name);
                        }
                        else
                        {
                            numDeletionBlocked++;
                        }
                    }
                }
            }

            return deletedIdxFiles;
        }

        protected override void PerformMaintenance()
        {
            using (ITracer activity = this.Context.Tracer.StartActivity(this.Area, EventLevel.Informational, Keywords.Telemetry, metadata: null))
            {
                // forceRun is only currently true for functional tests
                if (!this.forceRun)
                {
                    if (!this.EnoughTimeBetweenRuns())
                    {
                        activity.RelatedWarning($"Skipping {nameof(PackfileMaintenanceStep)} due to not enough time between runs");
                        return;
                    }

                    IEnumerable<int> processIds = this.GitProcessChecker.GetRunningGitProcessIds();
                    if (processIds.Any())
                    {
                        activity.RelatedWarning($"Skipping {nameof(PackfileMaintenanceStep)} due to git pids {string.Join(",", processIds)}", Keywords.Telemetry);
                        return;
                    }
                }

                this.GetPackFilesInfo(out int beforeCount, out long beforeSize, out _, out bool hasKeep);

                if (!hasKeep && this.Context.Enlistment.UsesGvfsProtocol)
                {
                    activity.RelatedWarning(this.CreateEventMetadata(), "Skipping pack maintenance due to no .keep file.");
                    return;
                }
                EventMetadata metadata = new EventMetadata();
                metadata.Add("GitObjectsRoot", this.Context.Enlistment.GitObjectsRoot);
                metadata.Add("BatchSize", this.batchSize);
                metadata.Add(nameof(beforeCount), beforeCount);
                metadata.Add(nameof(beforeSize), beforeSize);

                string multiPackIndexLockPath = Path.Combine(this.Context.Enlistment.GitPackRoot, MultiPackIndexLock);
                this.Context.FileSystem.TryDeleteFile(multiPackIndexLockPath);

                if (this.GitFeatures.HasFlag(GitFeatureFlags.MaintenanceBuiltin))
                {
                    GitProcess.Result taskResult = this.RunGitCommand(
                                                        process => process.MaintenanceRunTask(GitProcess.MaintenanceTask.IncrementalRepack, this.Context.Enlistment.GitObjectsRoot),
                                                        nameof(GitProcess.MaintenanceRunTask));
                    metadata.Add("MaintenanceRunExitCode", taskResult.ExitCode);
                }
                else
                {
                    this.RunGitCommand((process) => process.WriteMultiPackIndex(this.Context.Enlistment.GitObjectsRoot), nameof(GitProcess.WriteMultiPackIndex));

                    GitProcess.Result expireResult = this.RunGitCommand((process) => process.MultiPackIndexExpire(this.Context.Enlistment.GitObjectsRoot), nameof(GitProcess.MultiPackIndexExpire));

                    this.GetPackFilesInfo(out int expireCount, out long expireSize, out long expireSize2, out hasKeep);

                    GitProcess.Result verifyAfterExpire = this.RunGitCommand((process) => process.VerifyMultiPackIndex(this.Context.Enlistment.GitObjectsRoot), nameof(GitProcess.VerifyMultiPackIndex));

                    if (!this.Stopping && verifyAfterExpire.ExitCodeIsFailure)
                    {
                        this.LogErrorAndRewriteMultiPackIndex(activity);
                    }

                    if (this.batchSize.Equals(DefaultBatchSizeBytes.ToString()) &&
                        expireSize < DefaultBatchSizeBytes &&
                        expireCount > 2)
                    {
                        // Ignoring the largest pack, repack the rest up to the size of the
                        // second-smallest pack. This results in a geometrically-decreasing
                        // list of pack sizes after the largest pack.
                        this.batchSize = expireSize2.ToString();
                    }

                    GitProcess.Result repackResult = this.RunGitCommand((process) => process.MultiPackIndexRepack(this.Context.Enlistment.GitObjectsRoot, this.batchSize), nameof(GitProcess.MultiPackIndexRepack));

                    metadata.Add(nameof(expireCount), expireCount);
                    metadata.Add(nameof(expireSize), expireSize);
                    metadata.Add(nameof(expireSize2), expireSize2);
                    metadata.Add("VerifyAfterExpireExitCode", verifyAfterExpire.ExitCode);
                }

                List<string> staleIdxFiles = this.CleanStaleIdxFiles(out int numDeletionBlocked);

                this.GetPackFilesInfo(out int afterCount, out long afterSize, out long afterSize2, out hasKeep);

                GitProcess.Result verifyAfterRepack = this.RunGitCommand((process) => process.VerifyMultiPackIndex(this.Context.Enlistment.GitObjectsRoot), nameof(GitProcess.VerifyMultiPackIndex));

                if (!this.Stopping && verifyAfterRepack.ExitCodeIsFailure)
                {
                    this.LogErrorAndRewriteMultiPackIndex(activity);
                }

                metadata.Add(nameof(afterCount), afterCount);
                metadata.Add(nameof(afterSize), afterSize);
                metadata.Add(nameof(afterSize2), afterSize2);
                metadata.Add("NumStaleIdxFiles", staleIdxFiles.Count);
                metadata.Add("NumIdxDeletionsBlocked", numDeletionBlocked);
                metadata.Add("VerifyAfterRepackExitCode", verifyAfterRepack.ExitCode);
                activity.RelatedEvent(EventLevel.Informational, $"{this.Area}_{nameof(this.PerformMaintenance)}", metadata, Keywords.Telemetry);

                this.SaveLastRunTimeToFile();
            }
        }
    }
}
