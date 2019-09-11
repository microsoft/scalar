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

            this.Enlistment.GetSourcePath(oldScalarSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(oldScalarSubFolderName);

            this.fileSystem.MoveFile(this.Enlistment.GetSourcePath(oldScalarSubFolderPath), this.Enlistment.GetSourcePath(newScalarSubFolderPath));

            this.Enlistment.GetSourcePath(newScalarSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(newScalarSubFolderName);

            // Projected folder with a physical folder
            string oldTestsSubFolderName = "Scalar.FunctionalTests";
            string oldTestsSubFolderPath = Path.Combine(parentFolderName, oldTestsSubFolderName);
            string newTestsSubFolderName = "scalar.functionaltests";
            string newTestsSubFolderPath = Path.Combine(parentFolderName, newTestsSubFolderName);

            string fileToAdd = "NewFile.txt";
            string fileToAddContent = "This is new file text.";
            string fileToAddPath = this.Enlistment.GetSourcePath(Path.Combine(oldTestsSubFolderPath, fileToAdd));
            this.fileSystem.WriteAllText(fileToAddPath, fileToAddContent);

            this.Enlistment.GetSourcePath(oldTestsSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(oldTestsSubFolderName);

            this.fileSystem.MoveFile(this.Enlistment.GetSourcePath(oldTestsSubFolderPath), this.Enlistment.GetSourcePath(newTestsSubFolderPath));

            this.Enlistment.GetSourcePath(newTestsSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(newTestsSubFolderName);

            // Remount
            this.Enlistment.UnmountScalar();
            this.Enlistment.MountScalar();

            this.Enlistment.GetSourcePath(newScalarSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(newScalarSubFolderName);
            this.Enlistment.GetSourcePath(newTestsSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(newTestsSubFolderName);
            this.Enlistment.GetSourcePath(Path.Combine(newTestsSubFolderPath, fileToAdd)).ShouldBeAFile(this.fileSystem).WithContents().ShouldEqual(fileToAddContent);

            // Rename each folder again
            string finalScalarSubFolderName = "gvFS";
            string finalScalarSubFolderPath = Path.Combine(parentFolderName, finalScalarSubFolderName);
            this.fileSystem.MoveFile(this.Enlistment.GetSourcePath(newScalarSubFolderPath), this.Enlistment.GetSourcePath(finalScalarSubFolderPath));
            this.Enlistment.GetSourcePath(finalScalarSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(finalScalarSubFolderName);

            string finalTestsSubFolderName = "scalar.FunctionalTESTS";
            string finalTestsSubFolderPath = Path.Combine(parentFolderName, finalTestsSubFolderName);
            this.fileSystem.MoveFile(this.Enlistment.GetSourcePath(newTestsSubFolderPath), this.Enlistment.GetSourcePath(finalTestsSubFolderPath));
            this.Enlistment.GetSourcePath(finalTestsSubFolderPath).ShouldBeADirectory(this.fileSystem).WithCaseMatchingName(finalTestsSubFolderName);
            this.Enlistment.GetSourcePath(Path.Combine(finalTestsSubFolderPath, fileToAdd)).ShouldBeAFile(this.fileSystem).WithContents().ShouldEqual(fileToAddContent);
        }
    }
}
