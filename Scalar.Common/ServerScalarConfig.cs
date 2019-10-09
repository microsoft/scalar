using Scalar.Common.Http;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scalar.Common
{
    public class ServerScalarConfig
    {
        public IList<VersionRange> AllowedScalarClientVersions { get; set; }

        public IList<CacheServerInfo> CacheServers { get; set; } = new CacheServerInfo[0];

        public class VersionRange
        {
            public Version Min { get; set; }
            public Version Max { get; set; }
        }
    }
}
