using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.IO;

namespace Scalar.Common.Maintenance
{
    public class FetchStep : GitMaintenanceStep
    {
        private const int IoFailureRetryDelayMS = 50;
        private const int LockWaitTimeMs = 100;
        private const int WaitingOnLockLogThreshold = 50;
        private const string FetchCommitsAndTreesLock = "fetch-commits-trees.lock";
        private const string FetchTimeFile = "fetch.time";
        private readonly TimeSpan timeBetweenFetches = TimeSpan.FromMinutes(70);
        private readonly TimeSpan timeBetweenFetchesNoCacheServer = TimeSpan.FromDays(1);
        private readonly bool forceRun;

        public FetchStep(
                    ScalarContext context,
                    GitObjects gitObjects,
                    bool requireCacheLock = true,
                    bool forceRun = false)
            : base(context, requireCacheLock)
        {
            this.GitObjects = gitObjects;
            this.forceRun = forceRun;
        }

        public override string Area => "FetchCommitsAndTreesStep";

        public override string ProgressMessage
        {
            get
            {
                if (!this.Context.Enlistment.UsesGvfsProtocol)
                {
                    return "Fetching from remotes";
                }
                else if (this.GitObjects.IsUsingCacheServer())
                {
                    return "Fetching from cache server";
                }
                else
                {
                    return "Fetching from origin (no cache server)";
                }
            }
        }

        // Used only for vanilla Git repos
        protected override TimeSpan TimeBetweenRuns => this.timeBetweenFetches;

        protected GitObjects GitObjects { get; }

        public bool TryFetch(out string error, GitProcess gitProcess = null)
        {
            if (gitProcess == null)
            {
                gitProcess = new GitProcess(this.Context.Enlistment);
            }

            return this.TryFetchUsingGitProtocol(gitProcess, out error);
        }

        protected override void PerformMaintenance()
        {
            string error = null;

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

        private bool TryFetchUsingGitProtocol(GitProcess gitProcess, out string error)
        {
            this.LastRunTimeFilePath = Path.Combine(this.Context.Enlistment.ScalarLogsRoot, FetchTimeFile);

            if (!this.forceRun && !this.EnoughTimeBetweenRuns())
            {
                this.Context.Tracer.RelatedInfo($"Skipping {nameof(FetchStep)} due to not enough time between runs");
                error = null;
                return true;
            }

            using (ITracer activity = this.Context.Tracer.StartActivity(nameof(GitProcess.BackgroundFetch), EventLevel.LogAlways))
            {
                if (!gitProcess.TryGetRemotes(out string[] remotes, out string errors))
                {
                    error = $"Failed to load remotes with error: {errors}";
                    activity.RelatedError(error);
                    return false;
                }

                bool response = true;

                error = "";
                foreach (string remote in remotes)
                {
                    activity.RelatedInfo($"Running fetch for remote '{remote}'");
                    GitProcess.Result result = gitProcess.BackgroundFetch(remote);

                    if (!string.IsNullOrWhiteSpace(result.Output))
                    {
                        activity.RelatedError($"Background fetch from '{remote}' completed with stdout: {result.Output}");
                    }

                    if (!string.IsNullOrWhiteSpace(result.Errors))
                    {
                        error += result.Errors;
                        activity.RelatedError($"Background fetch from '{remote}' completed with stderr: {result.Errors}");
                    }

                    if (result.ExitCodeIsFailure)
                    {
                        response = false;
                        // Keep going through other remotes, but the overall result will still be false.
                        activity.RelatedError($"Background fetch from '{remote}' failed");
                    }
                }

                this.SaveLastRunTimeToFile();
                return response;
            }
        }
    }
}
