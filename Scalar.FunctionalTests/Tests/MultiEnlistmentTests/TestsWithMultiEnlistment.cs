using NUnit.Framework;
using Scalar.FunctionalTests.Tools;
using System.Collections.Generic;

namespace Scalar.FunctionalTests.Tests.MultiEnlistmentTests
{
    public class TestsWithMultiEnlistment
    {
        private List<ScalarFunctionalTestEnlistment> enlistmentsToDelete = new List<ScalarFunctionalTestEnlistment>();

        [TearDown]
        public void DeleteEnlistments()
        {
            foreach (ScalarFunctionalTestEnlistment enlistment in this.enlistmentsToDelete)
            {
                enlistment.UnmountAndDeleteAll();
            }

            this.OnTearDownEnlistmentsDeleted();

            this.enlistmentsToDelete.Clear();
        }

        /// <summary>
        /// Can be overridden for custom [TearDown] steps that occur after the test enlistements have been unmounted and deleted
        /// </summary>
        protected virtual void OnTearDownEnlistmentsDeleted()
        {
        }

        protected ScalarFunctionalTestEnlistment CreateNewEnlistment(
            string localCacheRoot = null,
            string branch = null,
            bool skipFetchCommitsAndTrees = false)
        {
            ScalarFunctionalTestEnlistment output = ScalarFunctionalTestEnlistment.CloneAndMount(
                ScalarTestConfig.PathToScalar,
                branch,
                localCacheRoot,
                skipFetchCommitsAndTrees);
            this.enlistmentsToDelete.Add(output);
            return output;
        }
    }
}
