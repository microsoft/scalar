using NUnit.Framework;
using Scalar.Tests.Should;

namespace Scalar.FunctionalTests.Tests.GitRepoPerFixture
{
    [Category(Categories.GitRepository)]
    public class ReposVerbTests : TestsWithGitRepoPerFixture
    {
        [TestCase]
        public void ReposVerbSucceedsInGitRepo()
        {
            this.Enlistment.IsScalarRepo.ShouldBeFalse();
            this.Enlistment.ReposAdd()
                           .Trim()
                           .ShouldEqual($"Successfully registered repo at '{this.Enlistment.EnlistmentRoot}'");
        }
    }
}
