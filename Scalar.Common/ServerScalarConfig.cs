using Scalar.Common.Http;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scalar.Common
{
    public class ServerScalarConfig
    {
        public IEnumerable<VersionRange> AllowedScalarClientVersions { get; set; }

        public IEnumerable<CacheServerInfo> CacheServers { get; set; } = Enumerable.Empty<CacheServerInfo>();

        public class VersionRange
        {
            public Version Min { get; set; }
            public Version Max { get; set; }
        }
    }
}
