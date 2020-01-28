using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using System.IO;

namespace Scalar.Upgrader
{
    public class WindowsUpgradeOrchestrator : UpgradeOrchestrator
    {
        public WindowsUpgradeOrchestrator(
            ProductUpgrader upgrader,
            ITracer tracer,
            PhysicalFileSystem fileSystem,
            InstallerPreRunChecker preRunChecker,
            TextReader input,
            TextWriter output)
            : base(upgrader, tracer, fileSystem, preRunChecker, input, output)
        {
        }

        public WindowsUpgradeOrchestrator(UpgradeOptions options)
            : base(options)
        {
        }
    }
}
