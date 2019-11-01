using System.Collections.Generic;

namespace Scalar.Common.RepoRegistry
{
    public interface IScalarRepoRegistry
    {
        bool TryRegisterRepo(string repoRoot, string ownerSID, out string errorMessage);
        List<ScalarRepoRegistration> GetRegisteredRepos();
        List<ScalarRepoRegistration> GetRegisteredReposForUser(string ownerSID);
        bool TryRemoveRepo(string repoRoot, out string errorMessage);
    }
}
