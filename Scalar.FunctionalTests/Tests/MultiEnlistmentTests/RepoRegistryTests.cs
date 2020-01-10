using NUnit.Framework;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System.IO;

namespace Scalar.FunctionalTests.Tests.MultiEnlistmentTests
{
    [TestFixture]
    public class RepoRegistryTests : TestsWithMultiEnlistment
    {
        [TestCase]
        public void ServiceListRegistered()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CreateNewEnlistment();
            ScalarFunctionalTestEnlistment enlistment2 = this.CreateNewEnlistment();

            string[] repoRootList = new string[] { enlistment1.EnlistmentRoot, enlistment2.EnlistmentRoot };

            // Do not check for unexpected repos, as other repos on the machine may be registered while
            // this test is running
            this.RunListCommand(enlistment1.EnlistmentRoot, expectedRepoRoots: repoRootList);
        }

        [TestCase]
        public void UnregisterRemovesFromList()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CreateNewEnlistment();
            ScalarFunctionalTestEnlistment enlistment2 = this.CreateNewEnlistment();

            string[] repoRootList = new string[] { enlistment1.EnlistmentRoot, enlistment2.EnlistmentRoot };

            string workDir = Directory.GetCurrentDirectory();

            this.RunListCommand(workDir, expectedRepoRoots: repoRootList);

            ScalarProcess process = new ScalarProcess(ScalarTestConfig.PathToScalar, "", "");
            process.Unregister(enlistment1.EnlistmentRoot);

            this.RunListCommand(
                workDir,
                expectedRepoRoots: new[] { enlistment2.EnlistmentRoot },
                unexpectedRepoRoots: new[] { enlistment1.EnlistmentRoot });
        }

        private void RunListCommand(string workdir, string[] expectedRepoRoots, string[] unexpectedRepoRoots = null)
        {
            ScalarProcess scalarProcess = new ScalarProcess(
                ScalarTestConfig.PathToScalar,
                enlistmentRoot: workdir,
                localCacheRoot: null);

            string result = scalarProcess.ListRepos();
            result.ShouldContain(expectedRepoRoots);

            if (unexpectedRepoRoots != null)
            {
                result.ShouldNotContain(ignoreCase: false, unexpectedRepoRoots);
            }
        }
    }
}
