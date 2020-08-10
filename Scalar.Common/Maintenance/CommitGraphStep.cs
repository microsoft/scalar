using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System.IO;
using System.Text;

namespace Scalar.Common.Maintenance
{
    public class CommitGraphStep : GitMaintenanceStep
    {
        private const string CommitGraphChainLock = "commit-graph-chain.lock";

        public CommitGraphStep(ScalarContext context, bool requireObjectCacheLock = true)
            : base(context, requireObjectCacheLock)
        {
        }

        public override string Area => "CommitGraphStep";

        public override string ProgressMessage => "Updating commit-graph";

        protected override void PerformMaintenance()
        {
            using (ITracer activity = this.Context.Tracer.StartActivity("TryWriteGitCommitGraph", EventLevel.Informational))
            {
                string commitGraphLockPath = Path.Combine(this.Context.Enlistment.GitObjectsRoot, "info", "commit-graphs", CommitGraphChainLock);
                this.Context.FileSystem.TryDeleteFile(commitGraphLockPath);

                GitProcess.Result maintenanceResult = this.RunGitCommand((process) => process.MaintenanceCommitGraph(this.Context.Enlistment.GitObjectsRoot), nameof(GitProcess.MaintenanceCommitGraph));

                StringBuilder sb = new StringBuilder();
                string commitGraphsDir = Path.Combine(this.Context.Enlistment.GitObjectsRoot, "info", "commit-graphs");

                if (this.Context.FileSystem.DirectoryExists(commitGraphsDir))
                {
                    foreach (DirectoryItemInfo info in this.Context.FileSystem.ItemsInDirectory(commitGraphsDir))
                    {
                        sb.Append(info.Name);
                        sb.Append(";");
                    }
                }

                activity.RelatedInfo($"commit-graph list after write: {sb}");
            }
        }
    }
}
