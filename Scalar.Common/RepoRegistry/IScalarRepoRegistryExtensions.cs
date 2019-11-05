using System;
using System.Collections.Generic;
using System.Linq;

namespace Scalar.Common.RepoRegistry
{
    public static class IScalarRepoRegistryExtensions
    {
        public static IEnumerable<ScalarRepoRegistration> GetRegisteredReposForUser(this IScalarRepoRegistry registry, string userId)
        {
            return registry.GetRegisteredRepos().Where(x => x.UserId.Equals(userId, StringComparison.CurrentCultureIgnoreCase));
        }
    }
}
