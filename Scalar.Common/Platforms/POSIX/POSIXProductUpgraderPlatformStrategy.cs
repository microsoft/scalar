using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using System;
using System.IO;

namespace Scalar.Platform.POSIX
{
    public class POSIXProductUpgraderPlatformStrategy : ProductUpgraderPlatformStrategy
    {
        public POSIXProductUpgraderPlatformStrategy(PhysicalFileSystem fileSystem, ITracer tracer)
        : base(fileSystem, tracer)
        {
        }

        public override bool TryPrepareLogDirectory(out string error)
        {
            throw new NotImplementedException();
        }

        public override bool TryPrepareApplicationDirectory(out string error)
        {
            throw new NotImplementedException();
        }

        public override bool TryPrepareDownloadDirectory(out string error)
        {
            throw new NotImplementedException();
        }
    }
}
