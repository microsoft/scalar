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
            this.RunReposListCommand(enlistment1.EnlistmentRoot, expectedRepoRoots: repoRootList);
        }

        [TestCase]
        public void RemoveAndCheckList()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CreateNewEnlistment();
            ScalarFunctionalTestEnlistment enlistment2 = this.CreateNewEnlistment();

            string[] repoRootList = new string[] { enlistment1.EnlistmentRoot, enlistment2.EnlistmentRoot };

            // Do not check for unexpected repos, as other repos on the machine may be registered while
            // this test is running
            this.RunReposListCommand(enlistment1.EnlistmentRoot, expectedRepoRoots: repoRootList);

            this.RunReposRemoveCommand(enlistment1.EnlistmentRoot);
            this.RunReposRemoveCommand(enlistment2.EnlistmentRoot);
        }

        private void RunReposListCommand(string workdir, string[] expectedRepoRoots)
        {
            ScalarProcess scalarProcess = new ScalarProcess(
                ScalarTestConfig.PathToScalar,
                enlistmentRoot: workdir,
                localCacheRoot: null);

            string result = scalarProcess.ReposList();
            result.ShouldContain(expectedRepoRoots);
        }

        private void RunReposRemoveCommand(string enlistmentDir)
        {
            ScalarProcess scalarProcess = new ScalarProcess(
                ScalarTestConfig.PathToScalar,
                enlistmentRoot: enlistmentDir,
                localCacheRoot: null);

            scalarProcess.ReposRemove();
            string result = scalarProcess.ReposList();
            result.ShouldNotContain(ignoreCase: false, new[] { enlistmentDir });
        }
    }
}
