using NUnit.Framework;
using Scalar.Tests.Should;

namespace Scalar.FunctionalTests.Tests.GitEnlistmentPerFixture
{
    [Category(Categories.GitRepository)]
    public class ReposVerbTests : TestsWithGitEnlistmentPerFixture
    {
        [TestCase]
        public void ReposAddSucceedsInGitRepo()
        {
            this.Enlistment.IsScalarRepo.ShouldBeFalse();
            this.Enlistment.ReposAdd()
                           .Trim()
                           .ShouldEqual($"Successfully registered repo at '{this.Enlistment.EnlistmentRoot}'");
            this.Enlistment.ReposList().ShouldContain(this.Enlistment.EnlistmentRoot);
        }
    }
}
