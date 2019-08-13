using NUnit.Framework;
using Scalar.FunctionalTests.Tools;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    public abstract class TestsWithEnlistmentPerFixture
    {
        private readonly bool forcePerRepoObjectCache;
        private readonly bool skipPrefetchDuringClone;

        public TestsWithEnlistmentPerFixture(bool forcePerRepoObjectCache = false, bool skipPrefetchDuringClone = false)
        {
            this.forcePerRepoObjectCache = forcePerRepoObjectCache;
            this.skipPrefetchDuringClone = skipPrefetchDuringClone;
        }

        public ScalarFunctionalTestEnlistment Enlistment
        {
            get; private set;
        }

        [OneTimeSetUp]
        public virtual void CreateEnlistment()
        {
            if (this.forcePerRepoObjectCache)
            {
                this.Enlistment = ScalarFunctionalTestEnlistment.CloneAndMountWithPerRepoCache(ScalarTestConfig.PathToScalar, this.skipPrefetchDuringClone);
            }
            else
            {
                this.Enlistment = ScalarFunctionalTestEnlistment.CloneAndMount(ScalarTestConfig.PathToScalar);
            }
        }

        [OneTimeTearDown]
        public virtual void DeleteEnlistment()
        {
            if (this.Enlistment != null)
            {
                this.Enlistment.UnmountAndDeleteAll();
            }
        }
    }
}
