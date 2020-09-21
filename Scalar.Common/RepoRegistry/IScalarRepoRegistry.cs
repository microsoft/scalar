using System;
using System.Collections.Generic;

namespace Scalar.Common.RepoRegistry
{
    public interface IScalarRepoRegistry
    {
        bool TryRegisterRepo(string normalizedRepoRoot, string userId, out string errorMessage);
        bool TryUnregisterRepo(string normalizedRepoRoot, out string errorMessage);
        IEnumerable<ScalarRepoRegistration> GetRegisteredRepos();
        bool TryGetMaintenanceDelayTime(out DateTime time);
        bool TryRemovePauseFile(out string errorMessage);
    }
}
