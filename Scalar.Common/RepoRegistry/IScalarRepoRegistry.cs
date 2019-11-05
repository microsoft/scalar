using System.Collections.Generic;

namespace Scalar.Common.RepoRegistry
{
    public interface IScalarRepoRegistry
    {
        bool TryRegisterRepo(string repoRoot, string ownerSID, out string errorMessage);
        bool TryRemoveRepo(string repoRoot, out string errorMessage);
        IEnumerable<ScalarRepoRegistration> GetRegisteredRepos();
    }
}
