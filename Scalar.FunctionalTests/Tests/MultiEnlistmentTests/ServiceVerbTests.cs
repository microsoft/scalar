using NUnit.Framework;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;

namespace Scalar.FunctionalTests.Tests.MultiEnlistmentTests
{
    [TestFixture]
    [Category(Categories.ExtraCoverage)]
    public class ServiceVerbTests : TestsWithMultiEnlistment
    {
        [TestCase]
        public void ServiceListRegistered()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CreateNewEnlistment();
            ScalarFunctionalTestEnlistment enlistment2 = this.CreateNewEnlistment();

            string[] repoRootList = new string[] { enlistment1.EnlistmentRoot, enlistment2.EnlistmentRoot };

            ScalarProcess scalarProcess1 = new ScalarProcess(
                ScalarTestConfig.PathToScalar,
                enlistment1.EnlistmentRoot,
                enlistment1.LocalCacheRoot);

            ScalarProcess scalarProcess2 = new ScalarProcess(
                ScalarTestConfig.PathToScalar,
                enlistment2.EnlistmentRoot,
                enlistment2.LocalCacheRoot);

            // Do not check for unexpected repos, as other repos on the machine may be registered while
            // this test is running
            this.RunServiceCommandAndCheckOutput("--list-registered", expectedRepoRoots: repoRootList);
        }

        private void RunServiceCommandAndCheckOutput(string argument, string[] expectedRepoRoots, string[] unexpectedRepoRoots = null)
        {
            ScalarProcess scalarProcess = new ScalarProcess(
                ScalarTestConfig.PathToScalar,
                enlistmentRoot: null,
                localCacheRoot: null);

            string result = scalarProcess.RunServiceVerb(argument);
            result.ShouldContain(expectedRepoRoots);

            if (unexpectedRepoRoots != null)
            {
                result.ShouldNotContain(false, unexpectedRepoRoots);
            }
        }
    }
}
