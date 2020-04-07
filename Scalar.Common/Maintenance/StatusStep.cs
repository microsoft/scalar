namespace Scalar.Common.Maintenance
{
    public class StatusStep : GitMaintenanceStep
    {
        public StatusStep(ScalarContext context)
            : base(context, false, null)
        {
        }

        public override string Area => nameof(StatusStep);

        public override string ProgressMessage => "Running 'git status'";

        protected override void PerformMaintenance()
        {
            if (ScalarPlatform.Instance.FileSystem.SupportsUntrackedCache)
            {
                this.RunGitCommand(process => process.UpdateUntrackedCache(),
                                   gitCommand: "update-index");
            }

            this.RunGitCommand(process => process.Status(allowObjectDownloads: true, useStatusCache: false, showUntracked: true),
                               gitCommand: "status");
        }
    }
}
