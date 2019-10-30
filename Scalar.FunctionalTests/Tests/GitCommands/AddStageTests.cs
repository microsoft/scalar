using NUnit.Framework;
using Scalar.FunctionalTests.Properties;

namespace Scalar.FunctionalTests.Tests.GitCommands
{
    [TestFixtureSource(typeof(GitRepoTests), nameof(GitRepoTests.ValidateWorkingTree))]
    [Category(Categories.GitCommands)]
    public class AddStageTests : GitRepoTests
    {
        public AddStageTests(Settings.ValidateWorkingTreeMode validateWorkingTree)
            : base(enlistmentPerTest: false, validateWorkingTree: validateWorkingTree)
        {
        }

        [TestCase, Order(1)]
        public void AddBasicTest()
        {
            this.EditFile("Some new content.", "Readme.md");
            this.ValidateGitCommand("add Readme.md");
            this.RunGitCommand("commit -m \"Changing the Readme.md\"");
        }

        [TestCase, Order(2)]
        public void StageBasicTest()
        {
            this.EditFile("Some new content.", "AuthoringTests.md");
            this.ValidateGitCommand("stage AuthoringTests.md");
            this.RunGitCommand("commit -m \"Changing the AuthoringTests.md\"");
        }

        [TestCase, Order(3)]
        public void AddAndStageHardLinksTest()
        {
            this.CreateHardLink("ReadmeLink.md", "Readme.md");
            this.ValidateGitCommand("add ReadmeLink.md");
            this.RunGitCommand("commit -m \"Created ReadmeLink.md\"");

            this.CreateHardLink("AuthoringTestsLink.md", "AuthoringTests.md");
            this.ValidateGitCommand("stage AuthoringTestsLink.md");
            this.RunGitCommand("commit -m \"Created AuthoringTestsLink.md\"");
        }
    }
}
