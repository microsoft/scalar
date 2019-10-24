using Scalar.Common.Maintenance;

namespace Scalar.Service
{
    public interface IScalarVerbRunner
    {
        bool CallMaintenance(MaintenanceTasks.Task task, string repoRoot, int sessionId);
    }
}
