using NUnit.Framework;
using Scalar.FunctionalTests.Tests.EnlistmentPerTestCase;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System.Runtime.InteropServices;

namespace Scalar.FunctionalTests.Tests
{
    [TestFixture]
    [Category(Categories.ExtraCoverage)]
    [Category(Categories.NeedsUpdatesForNonVirtualizedMode)]
    public class DiskLayoutVersionTests : TestsWithEnlistmentPerTestCase
    {
        private const int WindowsCurrentDiskLayoutMajorVersion = 0;
        private const int MacCurrentDiskLayoutMajorVersion = 0;
        private const int WindowsCurrentDiskLayoutMinimumMajorVersion = 0;
        private const int MacCurrentDiskLayoutMinimumMajorVersion = 0;
        private const int CurrentDiskLayoutMinorVersion = 0;
        private int currentDiskMajorVersion;
        private int currentDiskMinimumMajorVersion;

        [SetUp]
        public override void CreateEnlistment()
        {
            base.CreateEnlistment();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                this.currentDiskMajorVersion = MacCurrentDiskLayoutMajorVersion;
                this.currentDiskMinimumMajorVersion = MacCurrentDiskLayoutMinimumMajorVersion;
            }
            else
            {
                this.currentDiskMajorVersion = WindowsCurrentDiskLayoutMajorVersion;
                this.currentDiskMinimumMajorVersion = WindowsCurrentDiskLayoutMinimumMajorVersion;
            }
        }

        [TestCase]
        public void MountSucceedsIfMinorVersionHasAdvancedButNotMajorVersion()
        {
            // Advance the minor version, mount should still work
            this.Enlistment.UnmountScalar();
            ScalarHelpers.SaveDiskLayoutVersion(
                this.Enlistment.DotScalarRoot,
                this.currentDiskMajorVersion.ToString(),
                (CurrentDiskLayoutMinorVersion + 1).ToString());
            this.Enlistment.TryMountScalar().ShouldBeTrue("Mount should succeed because only the minor version advanced");

            // Advance the major version, mount should fail
            this.Enlistment.UnmountScalar();
            ScalarHelpers.SaveDiskLayoutVersion(
                this.Enlistment.DotScalarRoot,
                (this.currentDiskMajorVersion + 1).ToString(),
                CurrentDiskLayoutMinorVersion.ToString());
            this.Enlistment.TryMountScalar().ShouldBeFalse("Mount should fail because the major version has advanced");
        }

        [TestCase]
        public void MountFailsIfBeforeMinimumVersion()
        {
            // Mount should fail if on disk version is below minimum supported version
            this.Enlistment.UnmountScalar();
            ScalarHelpers.SaveDiskLayoutVersion(
                this.Enlistment.DotScalarRoot,
                (this.currentDiskMinimumMajorVersion - 1).ToString(),
                CurrentDiskLayoutMinorVersion.ToString());
            this.Enlistment.TryMountScalar().ShouldBeFalse("Mount should fail because we are before minimum version");
        }
    }
}
