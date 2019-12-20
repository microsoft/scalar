using NUnit.Framework;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;

namespace Scalar.FunctionalTests.Tests.MultiEnlistmentTests
{
    [TestFixture]
    public class ReposVerbTests : TestsWithMultiEnlistment
    {
        [TestCase]
        public void ServiceListRegistered()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CreateNewEnlistment();
            ScalarFunctionalTestEnlistment enlistment2 = this.CreateNewEnlistment();

            string[] repoRootList = new string[] { enlistment1.EnlistmentRoot, enlistment2.EnlistmentRoot };

            // Do not check for unexpected repos, as other repos on the machine may be registered while
            // this test is running
            this.RunReposListCommand(expectedRepoRoots: repoRootList);
        }

        private void RunReposListCommand(string[] expectedRepoRoots)
        {
            ScalarProcess scalarProcess = new ScalarProcess(
                ScalarTestConfig.PathToScalar,
                enlistmentRoot: null,
                localCacheRoot: null);

            string result = scalarProcess.ReposList();
            result.ShouldContain(expectedRepoRoots);
        }
    }
}
