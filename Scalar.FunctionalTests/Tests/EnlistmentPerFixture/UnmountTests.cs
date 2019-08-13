using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    [Category(Categories.ExtraCoverage)]
    [Category(Categories.NeedsUpdatesForNonVirtualizedMode)]
    public class UnmountTests : TestsWithEnlistmentPerFixture
    {
        private FileSystemRunner fileSystem;

        public UnmountTests()
        {
            this.fileSystem = new SystemIORunner();
        }

        [SetUp]
        public void SetupTest()
        {
            ScalarProcess scalarProcess = new ScalarProcess(
                ScalarTestConfig.PathToScalar,
                this.Enlistment.EnlistmentRoot,
                Path.Combine(this.Enlistment.EnlistmentRoot, ScalarTestConfig.DotScalarRoot));

            if (!scalarProcess.IsEnlistmentMounted())
            {
                scalarProcess.Mount();
            }
        }

        [TestCase]
        public void UnmountWaitsForLock()
        {
            ManualResetEventSlim lockHolder = GitHelpers.AcquireScalarLock(this.Enlistment, out _);

            using (Process unmountingProcess = this.StartUnmount())
            {
                unmountingProcess.WaitForExit(3000).ShouldEqual(false, "Unmount completed while lock was acquired.");

                // Release the lock.
                lockHolder.Set();

                unmountingProcess.WaitForExit(10000).ShouldEqual(true, "Unmount didn't complete as expected.");
            }
        }

        [TestCase]
        public void UnmountSkipLock()
        {
            ManualResetEventSlim lockHolder = GitHelpers.AcquireScalarLock(this.Enlistment, out _, Timeout.Infinite, true);

            using (Process unmountingProcess = this.StartUnmount("--skip-wait-for-lock"))
            {
                unmountingProcess.WaitForExit(10000).ShouldEqual(true, "Unmount didn't complete as expected.");
            }

            // Signal process holding lock to terminate and release lock.
            lockHolder.Set();
        }

        private Process StartUnmount(string extraParams = "")
        {
            string enlistmentRoot = this.Enlistment.EnlistmentRoot;

            // TODO: 865304 Use app.config instead of --internal* arguments
            ProcessStartInfo processInfo = new ProcessStartInfo(ScalarTestConfig.PathToScalar);
            processInfo.Arguments = "unmount " + extraParams + " " + TestConstants.InternalUseOnlyFlag + " " + ScalarHelpers.GetInternalParameter();
            processInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processInfo.WorkingDirectory = enlistmentRoot;
            processInfo.UseShellExecute = false;

            Process executingProcess = new Process();
            executingProcess.StartInfo = processInfo;
            executingProcess.Start();

            return executingProcess;
        }
    }
}
