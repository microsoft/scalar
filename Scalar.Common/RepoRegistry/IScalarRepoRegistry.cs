using System;
using System.Collections.Generic;

namespace Scalar.Common.RepoRegistry
{
    public interface IScalarRepoRegistry
    {
        bool TryRegisterRepo(string normalizedRepoRoot, string userId, out string errorMessage);
        bool TryUnregisterRepo(string normalizedRepoRoot, out string errorMessage);
        IEnumerable<ScalarRepoRegistration> GetRegisteredRepos();
        public bool TryGetMaintenanceDelayTime(out DateTime time);
    }
}
