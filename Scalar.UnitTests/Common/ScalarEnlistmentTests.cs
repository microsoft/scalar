using NUnit.Framework;
using Scalar.Common;
using Scalar.Common.Git;
using Scalar.Tests.Should;
using Scalar.UnitTests.Mock.Git;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Scalar.UnitTests.Common
{
    [TestFixture]
    public class ScalarEnlistmentTests
    {
        private const string EnlistmentId = "520dcf634ce34065a06abaa4010a256f";

        [TestCase]
        public void CanGetEnlistmentId()
        {
            TestScalarEnlistment enlistment = new TestScalarEnlistment();
            enlistment.GetEnlistmentId().ShouldEqual(EnlistmentId);
        }

        [TestCase]
        public void TryGetScalarEnlistmentRoot()
        {
            string root = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "M:" : "/usr";
            string a = Path.Combine(root, "a");
            string a_b = Path.Combine(a, "b");
            string a_b_src = Path.Combine(a_b, "src");
            string a_b_src_git = Path.Combine(a_b_src, ".git");
            string a_b_src_d = Path.Combine(a_b_src, "d");
            string a_b_src_d_e = Path.Combine(a_b_src_d, "e");
            string a_src = Path.Combine(a, "src");
            string a_src_src = Path.Combine(a_src, "src");
            string a_src_src_git = Path.Combine(a_src_src, ".git");
            string a_srcy = Path.Combine(a, "srcy");
            string a_srcy_git = Path.Combine(a_srcy, ".git");
            string a_c = Path.Combine(a, "c");
            string a_c_git = Path.Combine(a_c, ".git");

            HashSet<string> paths = new HashSet<string>()
            {
                a,
                a_b,
                a_b_src,
                a_b_src_git,
                a_b_src_d,
                a_src,
                a_src_src,
                a_src_src_git,
                a_srcy,
                a_srcy_git,
                a_c,
                a_c_git,
            };

            TestGetRoot(paths, false, a, null, null);
            TestGetRoot(paths, true, a_b, a_b, a_b_src);
            TestGetRoot(paths, true, a_b_src, a_b, a_b_src);
            TestGetRoot(paths, true, a_b_src_d, a_b, a_b_src);
            TestGetRoot(paths, true, a_b_src_d_e, a_b, a_b_src);
            TestGetRoot(paths, true, a_src, a_src, a_src_src);
            TestGetRoot(paths, true, a_src_src, a_src, a_src_src);
            TestGetRoot(paths, true, a_src_src_git, a_src, a_src_src);
            TestGetRoot(paths, true, a_srcy, a_srcy, a_srcy);
            TestGetRoot(paths, true, a_srcy_git, a_srcy, a_srcy);
            TestGetRoot(paths, true, a_c, a_c, a_c);
        }

        private void TestGetRoot(
            HashSet<string> paths,
            bool expectedResult,
            string directory,
            string expectedEnlistmentRoot,
            string expectedWorkingDirectoryRoot)
        {
            bool actualResult = ScalarEnlistment.TryGetScalarEnlistmentRoot(
                directory,
                out string enlistmentRoot,
                out string workingDirectoryRoot,
                path => paths.Contains(path));

            actualResult.ShouldEqual(expectedResult);

            if (!expectedResult)
            {
                return;
            }

            enlistmentRoot.ShouldEqual(expectedEnlistmentRoot);
            workingDirectoryRoot.ShouldEqual(expectedWorkingDirectoryRoot);
        }

        private class TestScalarEnlistment : ScalarEnlistment
        {
            private MockGitProcess gitProcess;

            public TestScalarEnlistment()
                : base("mock:\\path", "mock://repoUrl", "mock:\\git", authentication: null)
            {
                this.gitProcess = new MockGitProcess();
                this.gitProcess.SetExpectedCommandResult(
                    "config --local scalar.enlistment-id",
                    () => new GitProcess.Result(EnlistmentId, string.Empty, GitProcess.Result.SuccessCode));
            }

            public override GitProcess CreateGitProcess()
            {
                return this.gitProcess;
            }
        }
    }
}
