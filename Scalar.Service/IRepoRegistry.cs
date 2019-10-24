using System.Collections.Generic;
using Scalar.Common.Maintenance;

namespace Scalar.Service
{
    public interface IRepoRegistry
    {
        bool TryRegisterRepo(string repoRoot, string ownerSID, out string errorMessage);
        bool TryDeactivateRepo(string repoRoot, out string errorMessage);
        bool TryGetActiveRepos(out List<RepoRegistration> repoList, out string errorMessage);
        bool TryRemoveRepo(string repoRoot, out string errorMessage);
        void RunMaintenanceTaskForRepos(MaintenanceTasks.Task task, string userId, int sessionId);
        void TraceStatus();
    }
}
