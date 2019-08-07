using GVFS.Common;
using GVFS.DiskLayoutUpgrades;
using Microsoft.Isam.Esent.Collections.Generic;
using System;
using System.IO;

namespace GVFS.Platform.Windows.DiskLayoutUpgrades
{
    public class WindowsDiskLayoutUpgradeData : IDiskLayoutUpgradeData
    {
        public const string DiskLayoutEsentVersionKey = "DiskLayoutVersion";
        public const string EsentRepoMetadataName = "RepoMetadata";

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

        public bool TryParseLegacyDiskLayoutVersion(string dotGVFSPath, out int majorVersion)
        {
            string repoMetadataPath = Path.Combine(dotGVFSPath, EsentRepoMetadataName);
            majorVersion = 0;
            if (Directory.Exists(repoMetadataPath))
            {
                try
                {
                    using (PersistentDictionary<string, string> oldMetadata = new PersistentDictionary<string, string>(repoMetadataPath))
                    {
                        string versionString = oldMetadata[DiskLayoutEsentVersionKey];
                        if (!int.TryParse(versionString, out majorVersion))
                        {
                            return false;
                        }
                    }
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}
