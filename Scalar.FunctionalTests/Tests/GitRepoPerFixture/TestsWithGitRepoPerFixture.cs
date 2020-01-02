using NUnit.Framework;
using Scalar.FunctionalTests.Tools;

namespace Scalar.FunctionalTests.Tests.GitRepoPerFixture
{
    [TestFixture]
    public class TestsWithGitRepoPerFixture
    {
        public ScalarFunctionalTestEnlistment Enlistment
        {
            get; private set;
        }

        [OneTimeSetUp]
        public virtual void CreateRepo()
        {
            this.Enlistment = ScalarFunctionalTestEnlistment.CloneGitRepo(ScalarTestConfig.PathToScalar);
        }

        [OneTimeTearDown]
        public virtual void DeleteRepo()
        {
            if (this.Enlistment != null)
            {
                this.Enlistment.DeleteAll();
            }
        }
    }
}
