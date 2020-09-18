using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Should;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System.IO;
using System.Linq;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    public class GitFetchTests : TestsWithEnlistmentPerFixture
    {
        private const string PrefetchPackPrefix = "prefetch";
        private const string TempPackFolder = "tempPacks";

        private FileSystemRunner fileSystem;

        public GitFetchTests()
            : base(forcePerRepoObjectCache: true, skipFetchCommitsAndTreesDuringClone: true)
        {
            this.fileSystem = new SystemIORunner();
        }

        private string PackRoot
        {
            get
            {
                return this.Enlistment.GetPackRoot(this.fileSystem);
            }
        }

        private string TempPackRoot
        {
            get
            {
                return Path.Combine(this.PackRoot, TempPackFolder);
            }
        }

        [TestCase, Order(1)]
        public void GitFetchDownloadsPrefetchPacks()
        {
            this.fileSystem.DeleteDirectory(this.PackRoot);

            GitHelpers.InvokeGitAgainstScalarRepo(this.Enlistment.RepoRoot, "fetch origin");

            // Verify pack root has a prefetch pack
            this.PackRoot
                .ShouldBeADirectory(this.fileSystem)
                .WithItems()
                .Where(info => info.Name.StartsWith(PrefetchPackPrefix))
                .ShouldBeNonEmpty();

            // Verify tempPacks is empty
            this.TempPackRoot.ShouldBeADirectory(this.fileSystem).WithNoItems();
        }
    }
}
