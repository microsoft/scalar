using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scalar.Common.Maintenance
{
    // Performs LooseObject Maintenace
    // 1. Removes loose objects that appear in packfiles
    // 2. Packs loose objects into a packfile
    public class LooseObjectsStep : GitMaintenanceStep
    {
        public const string LooseObjectsLastRunFileName = "loose-objects.time";
        private readonly bool forceRun;

        public LooseObjectsStep(
            ScalarContext context,
            bool requireCacheLock = true,
            bool forceRun = false,
            GitProcessChecker gitProcessChecker = null)
            : base(context, requireCacheLock, gitProcessChecker)
        {
            this.forceRun = forceRun;
        }

        public enum CreatePackResult
        {
            Succeess,
            UnknownFailure,
            CorruptObject
        }

        public override string Area => nameof(LooseObjectsStep);

        // 50,000 was found to be the optimal time taking ~5 minutes
        public int MaxLooseObjectsInPack { get; set; } = 50000;

        protected override string LastRunTimeFilePath => Path.Combine(this.Context.Enlistment.GitObjectsRoot, "info", LooseObjectsLastRunFileName);
        protected override TimeSpan TimeBetweenRuns => TimeSpan.FromDays(1);

        public override string ProgressMessage => "Cleaning up loose objects";

        public void CountLooseObjects(out int count, out long size)
        {
            count = 0;
            size = 0;

            foreach (string directoryPath in this.Context.FileSystem.EnumerateDirectories(this.Context.Enlistment.GitObjectsRoot))
            {
                string directoryName = directoryPath.TrimEnd(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar).Last();

                if (GitObjects.IsLooseObjectsDirectory(directoryName))
                {
                    List<DirectoryItemInfo> dirItems = this.Context.FileSystem.ItemsInDirectory(directoryPath).ToList();
                    count += dirItems.Count;
                    size += dirItems.Sum(item => item.Length);
                }
            }
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
                        activity.RelatedWarning($"Skipping {nameof(LooseObjectsStep)} due to not enough time between runs");
                        return;
                    }

                    IEnumerable<int> processIds = this.GitProcessChecker.GetRunningGitProcessIds();
                    if (processIds.Any())
                    {
                        activity.RelatedWarning($"Skipping {nameof(LooseObjectsStep)} due to git pids {string.Join(",", processIds)}", Keywords.Telemetry);
                        return;
                    }
                }

                this.CountLooseObjects(out int beforeLooseObjectsCount, out long beforeLooseObjectsSize);
                this.GetPackFilesInfo(out int beforePackCount, out long beforePackSize, out long beforeSize2, out bool _);

                GitProcess.Result gitResult = this.RunGitCommand((process) => process.MaintenanceLooseObjects(this.Context.Enlistment.GitObjectsRoot), nameof(GitProcess.MaintenanceLooseObjects));

                this.CountLooseObjects(out int afterLooseObjectsCount, out long afterLooseObjectsSize);
                this.GetPackFilesInfo(out int afterPackCount, out long afterPackSize, out long afterSize2, out bool _);

                EventMetadata metadata = new EventMetadata();
                metadata.Add("GitObjectsRoot", this.Context.Enlistment.GitObjectsRoot);

                metadata.Add("PrunedPackedExitCode", gitResult.ExitCode);
                metadata.Add("StartingCount", beforeLooseObjectsCount);
                metadata.Add("EndingCount", afterLooseObjectsCount);
                metadata.Add("StartingPackCount", beforePackCount);
                metadata.Add("EndingPackCount", afterPackCount);

                metadata.Add("StartingSize", beforeLooseObjectsSize);
                metadata.Add("EndingSize", afterLooseObjectsSize);
                metadata.Add("StartingPackSize", beforePackSize);
                metadata.Add("EndingPackSize", afterPackSize);
                metadata.Add("StartingSize2", beforeSize2);
                metadata.Add("EndingSize2", afterSize2);

                metadata.Add("RemovedCount", beforeLooseObjectsCount - afterLooseObjectsCount);

                activity.RelatedEvent(EventLevel.Informational, $"{this.Area}_{nameof(this.PerformMaintenance)}", metadata, Keywords.Telemetry);
                this.SaveLastRunTimeToFile();
            }
        }
    }
}
