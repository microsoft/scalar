using NUnit.Framework;
using Scalar.FunctionalTests.Tools;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    public abstract class TestsWithEnlistmentPerFixture
    {
        private readonly bool forcePerRepoObjectCache;
        private readonly bool skipFetchCommitsAndTreesDuringClone;
        private readonly bool fullClone;

        public TestsWithEnlistmentPerFixture(bool forcePerRepoObjectCache = false, bool skipFetchCommitsAndTreesDuringClone = false, bool fullClone = true)
        {
            this.forcePerRepoObjectCache = forcePerRepoObjectCache;
            this.skipFetchCommitsAndTreesDuringClone = skipFetchCommitsAndTreesDuringClone;
            this.fullClone = fullClone;
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
                this.Enlistment = ScalarFunctionalTestEnlistment.CloneWithPerRepoCache(
                    ScalarTestConfig.PathToScalar,
                    this.skipFetchCommitsAndTreesDuringClone);
            }
            else
            {
                this.Enlistment = ScalarFunctionalTestEnlistment.Clone(ScalarTestConfig.PathToScalar, fullClone: this.fullClone);
            }
        }

        [OneTimeTearDown]
        public virtual void DeleteEnlistment()
        {
            if (this.Enlistment != null)
            {
                this.Enlistment.DeleteAll();
            }
        }
    }
}
