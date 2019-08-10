using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;

namespace Scalar.UnitTests.Mock.Common
{
    public class MockProductUpgraderPlatformStrategy : ProductUpgraderPlatformStrategy
    {
        public MockProductUpgraderPlatformStrategy(PhysicalFileSystem fileSystem, ITracer tracer)
        : base(fileSystem, tracer)
        {
        }

        public override bool TryPrepareLogDirectory(out string error)
        {
            error = null;
            return true;
        }

        public override bool TryPrepareApplicationDirectory(out string error)
        {
            error = null;
            return true;
        }

        public override bool TryPrepareDownloadDirectory(out string error)
        {
            error = null;
            return true;
        }
    }
}
