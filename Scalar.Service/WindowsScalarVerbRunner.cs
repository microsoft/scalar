using Scalar.Common;
using Scalar.Common.Maintenance;
using Scalar.Common.Tracing;
using Scalar.Platform.Windows;

namespace Scalar.Service
{
    public class WindowsScalarVerbRunner : IScalarVerbRunner
    {
        private readonly ITracer tracer;

        public WindowsScalarVerbRunner(ITracer tracer)
        {
            this.tracer = tracer;
        }

        public bool CallMaintenance(MaintenanceTasks.Task task, string repoRoot, int sessionId)
        {
            using (CurrentUser currentUser = new CurrentUser(this.tracer, sessionId))
            {
                if (!this.CallScalarMaintenance(task, repoRoot, currentUser))
                {
                    this.tracer.RelatedError($"{nameof(this.CallMaintenance)}: Unable to start the Scalar.exe process.");
                    return false;
                }
            }

            return true;
        }

        private bool CallScalarMaintenance(MaintenanceTasks.Task task, string repoRoot, CurrentUser currentUser)
        {
            string taskVerbName = MaintenanceTasks.GetVerbTaskName(task);

            return currentUser.RunAs(
                Configuration.Instance.ScalarLocation,
                $"run {taskVerbName} \"{repoRoot}\"",
                wait: true);
        }
    }
}
