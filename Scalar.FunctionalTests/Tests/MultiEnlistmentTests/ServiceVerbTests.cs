using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using NUnit.Framework;

namespace Scalar.FunctionalTests.Tests.MultiEnlistmentTests
{
    [TestFixture]
    [NonParallelizable]
    [Category(Categories.ExtraCoverage)]
    [Category(Categories.MacTODO.NeedsServiceVerb)]
    [Category(Categories.NeedsUpdatesForNonVirtualizedMode)]
    public class ServiceVerbTests : TestsWithMultiEnlistment
    {
        private static readonly string[] EmptyRepoList = new string[] { };

        [TestCase]
        public void ServiceCommandsWithNoRepos()
        {
            this.RunServiceCommandAndCheckOutput("--unmount-all", EmptyRepoList);
            this.RunServiceCommandAndCheckOutput("--mount-all", EmptyRepoList);
            this.RunServiceCommandAndCheckOutput("--list-mounted", EmptyRepoList);
        }

        [TestCase]
        public void ServiceCommandsWithMultipleRepos()
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

            this.RunServiceCommandAndCheckOutput("--list-mounted", expectedRepoRoots: repoRootList);
            this.RunServiceCommandAndCheckOutput("--unmount-all", expectedRepoRoots: repoRootList);

            // Check both are unmounted
            scalarProcess1.IsEnlistmentMounted().ShouldEqual(false);
            scalarProcess2.IsEnlistmentMounted().ShouldEqual(false);

            this.RunServiceCommandAndCheckOutput("--list-mounted", EmptyRepoList);
            this.RunServiceCommandAndCheckOutput("--unmount-all", EmptyRepoList);
            this.RunServiceCommandAndCheckOutput("--mount-all", expectedRepoRoots: repoRootList);

            // Check both are mounted
            scalarProcess1.IsEnlistmentMounted().ShouldEqual(true);
            scalarProcess2.IsEnlistmentMounted().ShouldEqual(true);

            this.RunServiceCommandAndCheckOutput("--list-mounted", expectedRepoRoots: repoRootList);
        }

        [TestCase]
        public void ServiceCommandsWithMountAndUnmount()
        {
            ScalarFunctionalTestEnlistment enlistment1 = this.CreateNewEnlistment();

            string[] repoRootList = new string[] { enlistment1.EnlistmentRoot };

            ScalarProcess scalarProcess1 = new ScalarProcess(
                ScalarTestConfig.PathToScalar,
                enlistment1.EnlistmentRoot,
                enlistment1.LocalCacheRoot);

            this.RunServiceCommandAndCheckOutput("--list-mounted", expectedRepoRoots: repoRootList);

            scalarProcess1.Unmount();

            this.RunServiceCommandAndCheckOutput("--list-mounted", EmptyRepoList, unexpectedRepoRoots: repoRootList);
            this.RunServiceCommandAndCheckOutput("--unmount-all", EmptyRepoList, unexpectedRepoRoots: repoRootList);
            this.RunServiceCommandAndCheckOutput("--mount-all", EmptyRepoList, unexpectedRepoRoots: repoRootList);

            // Check that it is still unmounted
            scalarProcess1.IsEnlistmentMounted().ShouldEqual(false);

            scalarProcess1.Mount();

            this.RunServiceCommandAndCheckOutput("--unmount-all", expectedRepoRoots: repoRootList);
            this.RunServiceCommandAndCheckOutput("--mount-all", expectedRepoRoots: repoRootList);
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
