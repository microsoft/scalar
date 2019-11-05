using System;
using System.Collections.Generic;
using System.Linq;

namespace Scalar.Common.RepoRegistry
{
    public static class IScalarRepoRegistryExtensions
    {
        public static IEnumerable<ScalarRepoRegistration> GetRegisteredReposForUser(this IScalarRepoRegistry registry, string ownerSID)
        {
            return registry.GetRegisteredRepos().Where(x => x.OwnerSID.Equals(ownerSID, StringComparison.CurrentCultureIgnoreCase));
        }
    }
}
