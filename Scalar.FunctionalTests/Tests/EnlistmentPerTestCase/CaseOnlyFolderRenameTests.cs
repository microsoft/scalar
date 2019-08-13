using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Should;
using Scalar.Tests.Should;
using System.IO;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerTestCase
{
    [TestFixture]
    [Category(Categories.NeedsUpdatesForNonVirtualizedMode)]
    public class CaseOnlyFolderRenameTests : TestsWithEnlistmentPerTestCase
    {
        private FileSystemRunner fileSystem;

        public CaseOnlyFolderRenameTests()
        {
            this.fileSystem = new BashRunner();
        }

        // MacOnly because renames of partial folders are blocked on Windows
        [TestCase]
        [Category(Categories.MacOnly)]
        public void CaseRenameFoldersAndRemountAndRenameAgain()
        {
            // Projected folder without a physical folder
            string parentFolderName = "Scalar";
            string oldScalarSubFolderName = "Scalar";
            string oldScalarSubFolderPath = Path.Combine(parentFolderName, oldScalarSubFolderName);
            string newScalarSubFolderName = "scalar";
            string newScalarSubFolderPath = Path.Combine(parentFolderName, newScalarSubFolderName);

            this.Enlistment.GetVirtualPathTo(oldScalarSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(oldScalarSubFolderName);

            this.fileSystem.MoveFile(this.Enlistment.GetVirtualPathTo(oldScalarSubFolderPath), this.Enlistment.GetVirtualPathTo(newScalarSubFolderPath));

            this.Enlistment.GetVirtualPathTo(newScalarSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(newScalarSubFolderName);

            // Projected folder with a physical folder
            string oldTestsSubFolderName = "Scalar.FunctionalTests";
            string oldTestsSubFolderPath = Path.Combine(parentFolderName, oldTestsSubFolderName);
            string newTestsSubFolderName = "scalar.functionaltests";
            string newTestsSubFolderPath = Path.Combine(parentFolderName, newTestsSubFolderName);

            string fileToAdd = "NewFile.txt";
            string fileToAddContent = "This is new file text.";
            string fileToAddPath = this.Enlistment.GetVirtualPathTo(Path.Combine(oldTestsSubFolderPath, fileToAdd));
            this.fileSystem.WriteAllText(fileToAddPath, fileToAddContent);

            this.Enlistment.GetVirtualPathTo(oldTestsSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(oldTestsSubFolderName);

            this.fileSystem.MoveFile(this.Enlistment.GetVirtualPathTo(oldTestsSubFolderPath), this.Enlistment.GetVirtualPathTo(newTestsSubFolderPath));

            this.Enlistment.GetVirtualPathTo(newTestsSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(newTestsSubFolderName);

            // Remount
            this.Enlistment.UnmountScalar();
            this.Enlistment.MountScalar();

            this.Enlistment.GetVirtualPathTo(newScalarSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(newScalarSubFolderName);
            this.Enlistment.GetVirtualPathTo(newTestsSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(newTestsSubFolderName);
            this.Enlistment.GetVirtualPathTo(Path.Combine(newTestsSubFolderPath, fileToAdd)).ShouldBeAFile(this.fileSystem).WithContents().ShouldEqual(fileToAddContent);

            // Rename each folder again
            string finalScalarSubFolderName = "gvFS";
            string finalScalarSubFolderPath = Path.Combine(parentFolderName, finalScalarSubFolderName);
            this.fileSystem.MoveFile(this.Enlistment.GetVirtualPathTo(newScalarSubFolderPath), this.Enlistment.GetVirtualPathTo(finalScalarSubFolderPath));
            this.Enlistment.GetVirtualPathTo(finalScalarSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(finalScalarSubFolderName);

            string finalTestsSubFolderName = "scalar.FunctionalTESTS";
            string finalTestsSubFolderPath = Path.Combine(parentFolderName, finalTestsSubFolderName);
            this.fileSystem.MoveFile(this.Enlistment.GetVirtualPathTo(newTestsSubFolderPath), this.Enlistment.GetVirtualPathTo(finalTestsSubFolderPath));
            this.Enlistment.GetVirtualPathTo(finalTestsSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(finalTestsSubFolderName);
            this.Enlistment.GetVirtualPathTo(Path.Combine(finalTestsSubFolderPath, fileToAdd)).ShouldBeAFile(this.fileSystem).WithContents().ShouldEqual(fileToAddContent);
        }
    }
}
