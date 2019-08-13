using System.Collections.Generic;
using System.Linq;

namespace Scalar.Common.NuGetUpgrade
{
    public class InstallManifestPlatform
    {
        public InstallManifestPlatform()
        {
            this.InstallActions = new List<InstallActionInfo>();
        }

        public InstallManifestPlatform(IEnumerable<InstallActionInfo> entries)
        {
            this.InstallActions = entries?.ToList() ?? new List<InstallActionInfo>();
        }

        public List<InstallActionInfo> InstallActions { get; }
    }
}
