using NUnit.Framework;
using Scalar.FunctionalTests.Tools;

namespace Scalar.FunctionalTests.Tests.GitEnlistmentPerFixture
{
    [TestFixture]
    public class TestsWithGitEnlistmentPerFixture
    {
        public ScalarFunctionalTestEnlistment Enlistment
        {
            get; private set;
        }

        [OneTimeSetUp]
        public virtual void CreateEnlistment()
        {
            this.Enlistment = ScalarFunctionalTestEnlistment.Clone(ScalarTestConfig.PathToScalar, asGitRepo: true);
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
