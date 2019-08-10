using GVFS.DiskLayoutUpgrades;
using System;

namespace GVFS.Common
{
    public interface IDiskLayoutUpgradeData
    {
        DiskLayoutUpgrade[] Upgrades { get; }
        DiskLayoutVersion Version { get; }
    }
}
