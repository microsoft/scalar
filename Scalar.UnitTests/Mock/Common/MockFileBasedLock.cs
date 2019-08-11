using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;

namespace Scalar.UnitTests.Mock.Common
{
    public class MockFileBasedLock : FileBasedLock
    {
        public MockFileBasedLock(
            PhysicalFileSystem fileSystem,
            ITracer tracer,
            string lockPath)
            : base(fileSystem, tracer, lockPath)
        {
        }

        public override bool TryAcquireLock()
        {
            return true;
        }

        public override void Dispose()
        {
        }
    }
}
