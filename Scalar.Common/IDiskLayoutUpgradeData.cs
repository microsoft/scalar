using Scalar.DiskLayoutUpgrades;
using System;

namespace Scalar.Common
{
    public interface IDiskLayoutUpgradeData
    {
        DiskLayoutUpgrade[] Upgrades { get; }
        DiskLayoutVersion Version { get; }
    }
}
