using GVFS.Common;
using GVFS.DiskLayoutUpgrades;

namespace GVFS.Platform.Windows.DiskLayoutUpgrades
{
    public class WindowsDiskLayoutUpgradeData : IDiskLayoutUpgradeData
    {
        public DiskLayoutUpgrade[] Upgrades
        {
            get
            {
                return new DiskLayoutUpgrade[]
                {
                };
            }
        }

        public DiskLayoutVersion Version => new DiskLayoutVersion(
                    currentMajorVersion: 0,
                    currentMinorVersion: 0,
                    minimumSupportedMajorVersion: 0);
    }
}
