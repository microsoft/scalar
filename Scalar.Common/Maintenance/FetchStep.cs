using Scalar.Common.Git;

namespace Scalar.Common.Maintenance
{
    public class FetchStep : GitMaintenanceStep
    {
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

        protected GitObjects GitObjects { get; }

        protected override void PerformMaintenance()
        {
            this.RunGitCommand(
                process => process.MaintenanceFetch(this.forceRun),
                nameof(GitProcess.MaintenanceFetch));
        }
    }
}
