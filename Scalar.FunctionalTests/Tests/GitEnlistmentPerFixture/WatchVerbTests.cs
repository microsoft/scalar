using NUnit.Framework;
using Scalar.Tests.Should;

namespace Scalar.FunctionalTests.Tests.GitEnlistmentPerFixture
{
    [Category(Categories.GitRepository)]
    public class WatchVerbTests : TestsWithGitEnlistmentPerFixture
    {
        [TestCase]
        public void WatchVerbSucceedsInGitRepo()
        {
            this.Enlistment.IsScalarRepo.ShouldBeFalse();
            this.Enlistment.Watch()
                           .Trim()
                           .ShouldEqual($"Successfully registered repo at '{this.Enlistment.EnlistmentRoot}'");
        }
    }
}
