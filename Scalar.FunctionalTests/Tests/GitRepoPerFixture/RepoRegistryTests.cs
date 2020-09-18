using NUnit.Framework;
using Scalar.Tests.Should;

namespace Scalar.FunctionalTests.Tests.GitRepoPerFixture
{
    [Category(Categories.GitRepository)]
    public class RepoRegistryTests : TestsWithGitRepoPerFixture
    {
        [TestCase]
        public void ReposVerbSucceedsInGitRepo()
        {
            this.Enlistment.IsScalarRepo.ShouldBeFalse();
            this.Enlistment.Register()
                           .Trim()
                           .ShouldEqual($"Successfully registered repo at '{this.Enlistment.EnlistmentRoot}'");
        }
    }
}
