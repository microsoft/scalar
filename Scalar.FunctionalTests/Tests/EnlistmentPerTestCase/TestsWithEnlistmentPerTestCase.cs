using NUnit.Framework;
using Scalar.FunctionalTests.Tools;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerTestCase
{
    [TestFixture]
    public abstract class TestsWithEnlistmentPerTestCase
    {
        private readonly bool forcePerRepoObjectCache;

        public TestsWithEnlistmentPerTestCase(bool forcePerRepoObjectCache = false)
        {
            this.forcePerRepoObjectCache = forcePerRepoObjectCache;
        }

        public ScalarFunctionalTestEnlistment Enlistment
        {
            get; private set;
        }

        [SetUp]
        public virtual void CreateEnlistment()
        {
            if (this.forcePerRepoObjectCache)
            {
                this.Enlistment = ScalarFunctionalTestEnlistment.CloneWithPerRepoCache(
                    ScalarTestConfig.PathToScalar,
                    skipFetchCommitsAndTrees: false);
            }
            else
            {
                this.Enlistment = ScalarFunctionalTestEnlistment.Clone(ScalarTestConfig.PathToScalar);
            }
        }

        [TearDown]
        public virtual void DeleteEnlistment()
        {
            if (this.Enlistment != null)
            {
                this.Enlistment.DeleteAll();
            }
        }
    }
}
