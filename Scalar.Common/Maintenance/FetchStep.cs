using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Scalar.Common.Maintenance
{
    public class FetchStep : GitMaintenanceStep
    {
        private const int IoFailureRetryDelayMS = 50;
        private const int LockWaitTimeMs = 100;
        private const int WaitingOnLockLogThreshold = 50;
        private const string FetchCommitsAndTreesLock = "fetch-commits-trees.lock";
        private readonly TimeSpan timeBetweenFetches = TimeSpan.FromMinutes(70);
        private readonly TimeSpan timeBetweenFetchesNoCacheServer = TimeSpan.FromDays(1);

        public FetchStep(ScalarContext context, GitObjects gitObjects, bool requireCacheLock = true)
            : base(context, requireCacheLock)
        {
            this.GitObjects = gitObjects;
        }

        public override string Area => "FetchCommitsAndTreesStep";

        protected GitObjects GitObjects { get; }

        public bool TryFetch(out string error, GitProcess gitProcess = null)
        {
            if (gitProcess == null)
            {
                gitProcess = new GitProcess(this.Context.Enlistment);
            }

            if (!this.Context.Enlistment.UsesGvfsProtocol)
            {
                using (ITracer activity = this.Context.Tracer.StartActivity(nameof(GitProcess.BackgroundFetch), EventLevel.LogAlways))
                {
                    GitProcess.Result result = gitProcess.BackgroundFetch();

                    error = result.Errors;

                    activity.RelatedInfo($"Background fetch completed with stdout: {result.Output}");
                    activity.RelatedError($"Background fetch completed with stderr: {result.Errors}");

                    return result.ExitCodeIsSuccess;
                }
            }

            // We take our own lock here to keep background and foreground fetches
            // (i.e. a user running 'scalar maintenance --task fetch')
            // from running at the same time.
            using (FileBasedLock fetchLock = ScalarPlatform.Instance.CreateFileBasedLock(
                this.Context.FileSystem,
                this.Context.Tracer,
                Path.Combine(this.Context.Enlistment.GitPackRoot, FetchCommitsAndTreesLock)))
            {
                WaitUntilLockIsAcquired(this.Context.Tracer, fetchLock);

                this.GitObjects.DeleteStaleTempPrefetchPackAndIdxs();
                this.GitObjects.DeleteTemporaryFiles();

                GitProcess.Result result = gitProcess.GvfsHelperPrefetch();

                if (result.ExitCodeIsFailure)
                {
                    error = result.Errors;
                    return false;
                }

                this.UpdateKeepPacks();
            }

            error = null;
            return true;
        }

        protected override void PerformMaintenance()
        {
            long last;
            string error = null;

            if (this.Context.Enlistment.UsesGvfsProtocol)
            {
                if (!this.TryGetMaxGoodPrefetchPackTimestamp(out last, out error))
                {
                    this.Context.Tracer.RelatedError(error);
                    return;
                }

                TimeSpan timeBetween = this.GitObjects.IsUsingCacheServer()
                                        ? this.timeBetweenFetches
                                        : this.timeBetweenFetchesNoCacheServer;

                DateTime lastDateTime = EpochConverter.FromUnixEpochSeconds(last);
                DateTime now = DateTime.UtcNow;
                if (now <= lastDateTime + timeBetween)
                {
                    this.Context.Tracer.RelatedInfo(this.Area + ": Skipping fetch since most-recent fetch ({0}) is too close to now ({1})", lastDateTime, now);
                    return;
                }
            }

            this.RunGitCommand(
                process =>
                {
                    this.TryFetch(out error, process);
                    return null;
                },
                nameof(this.TryFetch));

            if (!string.IsNullOrEmpty(error))
            {
                this.Context.Tracer.RelatedWarning(
                    metadata: this.CreateEventMetadata(),
                    message: $"{nameof(this.TryFetch)} failed with error '{error}'",
                    keywords: Keywords.Telemetry);
            }
        }

        private static long? GetTimestamp(string packName)
        {
            string filename = Path.GetFileName(packName);
            if (!filename.StartsWith(ScalarConstants.PrefetchPackPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string[] parts = filename.Split('-');
            long parsed;
            if (parts.Length > 1 && long.TryParse(parts[1], out parsed))
            {
                return parsed;
            }

            return null;
        }

        private static void WaitUntilLockIsAcquired(ITracer tracer, FileBasedLock fileBasedLock)
        {
            int attempt = 0;
            while (!fileBasedLock.TryAcquireLock())
            {
                Thread.Sleep(LockWaitTimeMs);
                ++attempt;
                if (attempt == WaitingOnLockLogThreshold)
                {
                    attempt = 0;
                    tracer.RelatedInfo("WaitUntilLockIsAcquired: Waiting to acquire fetch lock");
                }
            }
        }

        private bool TryGetMaxGoodPrefetchPackTimestamp(out long maxGoodTimestamp, out string error)
        {
            this.Context.FileSystem.CreateDirectory(this.Context.Enlistment.GitPackRoot);

            string[] packs = this.GitObjects.ReadPackFileNames(this.Context.Enlistment.GitPackRoot, ScalarConstants.PrefetchPackPrefix);
            List<PrefetchPackInfo> orderedPacks = packs
                .Where(pack => GetTimestamp(pack).HasValue)
                .Select(pack => new PrefetchPackInfo(GetTimestamp(pack).Value, pack))
                .OrderBy(packInfo => packInfo.Timestamp)
                .ToList();

            maxGoodTimestamp = -1;

            int firstBadPack = -1;
            for (int i = 0; i < orderedPacks.Count; ++i)
            {
                long timestamp = orderedPacks[i].Timestamp;
                string packPath = orderedPacks[i].Path;
                string idxPath = Path.ChangeExtension(packPath, ".idx");
                if (!this.Context.FileSystem.FileExists(idxPath))
                {
                    EventMetadata metadata = this.CreateEventMetadata();
                    metadata.Add("pack", packPath);
                    metadata.Add("idxPath", idxPath);
                    metadata.Add("timestamp", timestamp);
                    GitProcess.Result indexResult = this.RunGitCommand(process => this.GitObjects.IndexPackFile(packPath, process), nameof(this.GitObjects.IndexPackFile));

                    if (indexResult.ExitCodeIsFailure)
                    {
                        firstBadPack = i;

                        this.Context.Tracer.RelatedWarning(metadata, $"{nameof(this.TryGetMaxGoodPrefetchPackTimestamp)}: Found pack file that's missing idx file, and failed to regenerate idx");
                        break;
                    }
                    else
                    {
                        maxGoodTimestamp = timestamp;

                        metadata.Add(TracingConstants.MessageKey.InfoMessage, $"{nameof(this.TryGetMaxGoodPrefetchPackTimestamp)}: Found pack file that's missing idx file, and regenerated idx");
                        this.Context.Tracer.RelatedEvent(EventLevel.Informational, $"{nameof(this.TryGetMaxGoodPrefetchPackTimestamp)}_RebuildIdx", metadata);
                    }
                }
                else
                {
                    maxGoodTimestamp = timestamp;
                }
            }

            if (this.Stopping)
            {
                throw new StoppingException();
            }

            if (firstBadPack != -1)
            {
                const int MaxDeleteRetries = 200; // 200 * IoFailureRetryDelayMS (50ms) = 10 seconds
                const int RetryLoggingThreshold = 40; // 40 * IoFailureRetryDelayMS (50ms) = 2 seconds

                // Before we delete _any_ pack-files, we need to delete the multi-pack-index, which
                // may refer to those packs.

                EventMetadata metadata = this.CreateEventMetadata();
                string midxPath = Path.Combine(this.Context.Enlistment.GitPackRoot, "multi-pack-index");
                metadata.Add("path", midxPath);
                metadata.Add(TracingConstants.MessageKey.InfoMessage, $"{nameof(this.TryGetMaxGoodPrefetchPackTimestamp)} deleting multi-pack-index");
                this.Context.Tracer.RelatedEvent(EventLevel.Informational, $"{nameof(this.TryGetMaxGoodPrefetchPackTimestamp)}_DeleteMultiPack_index", metadata);

                if (!this.Context.FileSystem.TryWaitForDelete(this.Context.Tracer, midxPath, IoFailureRetryDelayMS, MaxDeleteRetries, RetryLoggingThreshold))
                {
                    error = $"Unable to delete {midxPath}";
                    return false;
                }

                // Delete packs and indexes in reverse order so that if fetch-commits-and-trees is killed, subseqeuent
                // fetch-commits-and-trees commands will find the right starting spot.
                for (int i = orderedPacks.Count - 1; i >= firstBadPack; --i)
                {
                    if (this.Stopping)
                    {
                        throw new StoppingException();
                    }

                    string packPath = orderedPacks[i].Path;
                    string idxPath = Path.ChangeExtension(packPath, ".idx");

                    metadata = this.CreateEventMetadata();
                    metadata.Add("path", idxPath);
                    metadata.Add(TracingConstants.MessageKey.InfoMessage, $"{nameof(this.TryGetMaxGoodPrefetchPackTimestamp)} deleting bad idx file");
                    this.Context.Tracer.RelatedEvent(EventLevel.Informational, $"{nameof(this.TryGetMaxGoodPrefetchPackTimestamp)}_DeleteBadIdx", metadata);

                    if (!this.Context.FileSystem.TryWaitForDelete(this.Context.Tracer, idxPath, IoFailureRetryDelayMS, MaxDeleteRetries, RetryLoggingThreshold))
                    {
                        error = $"Unable to delete {idxPath}";
                        return false;
                    }

                    metadata = this.CreateEventMetadata();
                    metadata.Add("path", packPath);
                    metadata.Add(TracingConstants.MessageKey.InfoMessage, $"{nameof(this.TryGetMaxGoodPrefetchPackTimestamp)} deleting bad pack file");
                    this.Context.Tracer.RelatedEvent(EventLevel.Informational, $"{nameof(this.TryGetMaxGoodPrefetchPackTimestamp)}_DeleteBadPack", metadata);

                    if (!this.Context.FileSystem.TryWaitForDelete(this.Context.Tracer, packPath, IoFailureRetryDelayMS, MaxDeleteRetries, RetryLoggingThreshold))
                    {
                        error = $"Unable to delete {packPath}";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Ensure the prefetch pack with most-recent timestamp has an associated
        /// ".keep" file. This prevents any Git command from deleting the pack.
        ///
        /// Delete the previous ".keep" file(s) so that pack can be deleted when they
        /// are not the most-recent pack.
        /// </summary>
        private void UpdateKeepPacks()
        {
            if (!this.TryGetMaxGoodPrefetchPackTimestamp(out long maxGoodTimeStamp, out string error))
            {
                return;
            }

            string prefix = $"{ScalarConstants.PrefetchPackPrefix}-{maxGoodTimeStamp}-";

            DirectoryItemInfo info = this.Context
                                         .FileSystem
                                         .ItemsInDirectory(this.Context.Enlistment.GitPackRoot)
                                         .Where(item => item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                                                        && string.Equals(Path.GetExtension(item.Name), ".pack", StringComparison.OrdinalIgnoreCase))
                                         .FirstOrDefault();
            if (info == null)
            {
                this.Context.Tracer.RelatedWarning(this.CreateEventMetadata(), $"Could not find latest prefetch pack, starting with {prefix}");
                return;
            }

            string newKeepFile = Path.ChangeExtension(info.FullName, ".keep");

            if (!this.Context.FileSystem.TryWriteAllText(newKeepFile, string.Empty))
            {
                this.Context.Tracer.RelatedWarning(this.CreateEventMetadata(), $"Failed to create .keep file at {newKeepFile}");
                return;
            }

            foreach (string keepFile in this.Context
                                     .FileSystem
                                     .ItemsInDirectory(this.Context.Enlistment.GitPackRoot)
                                     .Where(item => item.Name.EndsWith(".keep", StringComparison.OrdinalIgnoreCase))
                                     .Select(item => item.FullName))
            {
                if (!keepFile.Equals(newKeepFile))
                {
                    this.Context.FileSystem.TryDeleteFile(keepFile);
                }
            }
        }

        private class PrefetchPackInfo
        {
            public PrefetchPackInfo(long timestamp, string path)
            {
                this.Timestamp = timestamp;
                this.Path = path;
            }

            public long Timestamp { get; }
            public string Path { get; }
        }
    }
}
