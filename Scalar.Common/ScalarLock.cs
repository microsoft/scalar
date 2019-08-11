using Scalar.Common.Tracing;
using System.Diagnostics;
using System.Threading;

namespace Scalar.Common
{
    public class ScalarLock
    {
        private readonly object acquisitionLock = new object();
        private readonly ITracer tracer;

        public ScalarLock(ITracer tracer)
        {
            this.tracer = tracer;
            this.Stats = new ActiveGitCommandStats();
        }

        public ActiveGitCommandStats Stats
        {
            get;
            private set;
        }

        // The lock release event is a convenient place to record stats about things that happened while a git command was running,
        // such as duration/count of object downloads during a git command, cache hits during a git command, etc.
        public class ActiveGitCommandStats
        {
            private Stopwatch lockAcquiredTime;
            private long lockHeldExternallyTimeMs;

            private long placeholderTotalUpdateTimeMs;
            private long placeholderUpdateFilesTimeMs;
            private long placeholderUpdateFoldersTimeMs;
            private long placeholderWriteAndFlushTimeMs;
            private int deleteFolderPlacehoderAttempted;
            private int folderPlaceholdersDeleted;
            private int folderPlaceholdersPathNotFound;
            private long parseGitIndexTimeMs;
            private long projectionWriteLockHeldMs;

            private int numBlobs;
            private long blobDownloadTimeMs;

            private int numCommitsAndTrees;
            private long commitAndTreeDownloadTimeMs;

            private int numSizeQueries;
            private long sizeQueryTimeMs;

            public ActiveGitCommandStats()
            {
                this.lockAcquiredTime = Stopwatch.StartNew();
            }

            public void RecordReleaseExternalLockRequested()
            {
                this.lockHeldExternallyTimeMs = this.lockAcquiredTime.ElapsedMilliseconds;
            }

            public void RecordUpdatePlaceholders(
                long durationMs,
                long updateFilesMs,
                long updateFoldersMs,
                long writeAndFlushMs,
                int deleteFolderPlacehoderAttempted,
                int folderPlaceholdersDeleted,
                int folderPlaceholdersPathNotFound)
            {
                this.placeholderTotalUpdateTimeMs = durationMs;
                this.placeholderUpdateFilesTimeMs = updateFilesMs;
                this.placeholderUpdateFoldersTimeMs = updateFoldersMs;
                this.placeholderWriteAndFlushTimeMs = writeAndFlushMs;
                this.deleteFolderPlacehoderAttempted = deleteFolderPlacehoderAttempted;
                this.folderPlaceholdersDeleted = folderPlaceholdersDeleted;
                this.folderPlaceholdersPathNotFound = folderPlaceholdersPathNotFound;
            }

            public void RecordProjectionWriteLockHeld(long durationMs)
            {
                this.projectionWriteLockHeldMs = durationMs;
            }

            public void RecordParseGitIndex(long durationMs)
            {
                this.parseGitIndexTimeMs = durationMs;
            }

            public void RecordObjectDownload(bool isBlob, long downloadTimeMs)
            {
                if (isBlob)
                {
                    Interlocked.Increment(ref this.numBlobs);
                    Interlocked.Add(ref this.blobDownloadTimeMs, downloadTimeMs);
                }
                else
                {
                    Interlocked.Increment(ref this.numCommitsAndTrees);
                    Interlocked.Add(ref this.commitAndTreeDownloadTimeMs, downloadTimeMs);
                }
            }

            public void RecordSizeQuery(long queryTimeMs)
            {
                Interlocked.Increment(ref this.numSizeQueries);
                Interlocked.Add(ref this.sizeQueryTimeMs, queryTimeMs);
            }

            public void AddStatsToTelemetry(EventMetadata metadata)
            {
                metadata.Add("DurationMS", this.lockAcquiredTime.ElapsedMilliseconds);
                metadata.Add("LockHeldExternallyMS", this.lockHeldExternallyTimeMs);
                metadata.Add("ParseGitIndexMS", this.parseGitIndexTimeMs);
                metadata.Add("UpdatePlaceholdersMS", this.placeholderTotalUpdateTimeMs);
                metadata.Add("UpdateFilePlaceholdersMS", this.placeholderUpdateFilesTimeMs);
                metadata.Add("UpdateFolderPlaceholdersMS", this.placeholderUpdateFoldersTimeMs);
                metadata.Add("DeleteFolderPlacehoderAttempted", this.deleteFolderPlacehoderAttempted);
                metadata.Add("FolderPlaceholdersDeleted", this.folderPlaceholdersDeleted);
                metadata.Add("FolderPlaceholdersPathNotFound", this.folderPlaceholdersPathNotFound);
                metadata.Add("PlaceholdersWriteAndFlushMS", this.placeholderWriteAndFlushTimeMs);
                metadata.Add("ProjectionWriteLockHeldMs", this.projectionWriteLockHeldMs);

                metadata.Add("BlobsDownloaded", this.numBlobs);
                metadata.Add("BlobDownloadTimeMS", this.blobDownloadTimeMs);

                metadata.Add("CommitsAndTreesDownloaded", this.numCommitsAndTrees);
                metadata.Add("CommitsAndTreesDownloadTimeMS", this.commitAndTreeDownloadTimeMs);

                metadata.Add("SizeQueries", this.numSizeQueries);
                metadata.Add("SizeQueryTimeMS", this.sizeQueryTimeMs);
            }
        }
    }
}
