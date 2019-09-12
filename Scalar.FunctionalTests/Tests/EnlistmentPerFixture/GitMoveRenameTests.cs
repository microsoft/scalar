using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Should;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System.Collections.Generic;
using System.IO;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixtureSource(typeof(FileSystemRunner), nameof(FileSystemRunner.Runners))]
    [Category(Categories.GitCommands)]
    [Category(Categories.NeedsUpdatesForNonVirtualizedMode)]
    public class GitMoveRenameTests : TestsWithEnlistmentPerFixture
    {
        private string testFileContents = "0123456789";
        private FileSystemRunner fileSystem;
        public GitMoveRenameTests(FileSystemRunner fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        [TestCase, Order(1)]
        public void GitStatus()
        {
            GitHelpers.CheckGitCommandAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "status",
                "On branch " + Properties.Settings.Default.Commitish,
                "nothing to commit, working tree clean");
        }

        [TestCase, Order(2)]
        public void GitStatusAfterNewFile()
        {
            string filename = "new.cs";
            string filePath = this.Enlistment.GetSourcePath(filename);

            filePath.ShouldNotExistOnDisk(this.fileSystem);
            this.fileSystem.WriteAllText(filePath, this.testFileContents);

            filePath.ShouldBeAFile(this.fileSystem).WithContents(this.testFileContents);

            GitHelpers.CheckGitCommandAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "status",
                "On branch " + Properties.Settings.Default.Commitish,
                "Untracked files:",
                filename);

            this.fileSystem.DeleteFile(filePath);
        }

        [TestCase, Order(3)]
        public void GitStatusAfterFileNameCaseChange()
        {
            string oldFilename = "new.cs";
            this.EnsureTestFileExists(oldFilename);

            string newFilename = "New.cs";
            string newFilePath = this.Enlistment.GetSourcePath(newFilename);
            this.fileSystem.MoveFile(this.Enlistment.GetSourcePath(oldFilename), newFilePath);

            GitHelpers.CheckGitCommandAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "status",
                "On branch " + Properties.Settings.Default.Commitish,
                "Untracked files:",
                newFilename);

            this.fileSystem.DeleteFile(newFilePath);
        }

        [TestCase, Order(4)]
        public void GitStatusAfterFileRename()
        {
            string oldFilename = "New.cs";
            this.EnsureTestFileExists(oldFilename);

            string newFilename = "test.cs";
            string newFilePath = this.Enlistment.GetSourcePath(newFilename);
            this.fileSystem.MoveFile(this.Enlistment.GetSourcePath(oldFilename), newFilePath);

            GitHelpers.CheckGitCommandAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "status",
                "On branch " + Properties.Settings.Default.Commitish,
                "Untracked files:",
                newFilename);
        }

        [TestCase, Order(5)]
        public void GitStatusAndObjectAfterGitAdd()
        {
            string existingFilename = "test.cs";
            this.Enlistment.GetSourcePath(existingFilename).ShouldBeAFile(this.fileSystem);

            GitHelpers.CheckGitCommandAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "add " + existingFilename,
                new string[] { });

            // Status should be correct
            GitHelpers.CheckGitCommandAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "status",
                "On branch " + Properties.Settings.Default.Commitish,
                "Changes to be committed:",
                existingFilename);

            // Object file for the test file should have the correct contents
            ProcessResult result = GitHelpers.InvokeGitAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "hash-object " + existingFilename);

            string objectHash = result.Output.Trim();
            result.Errors.ShouldBeEmpty();

            this.Enlistment.GetObjectPathTo(objectHash).ShouldBeAFile(this.fileSystem);

            GitHelpers.CheckGitCommandAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "cat-file -p " + objectHash,
                this.testFileContents);
        }

        [TestCase, Order(6)]
        public void GitStatusAfterUnstage()
        {
            string existingFilename = "test.cs";
            this.Enlistment.GetSourcePath(existingFilename).ShouldBeAFile(this.fileSystem);

            GitHelpers.CheckGitCommandAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "reset HEAD " + existingFilename,
                new string[] { });

            GitHelpers.CheckGitCommandAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "status",
                "On branch " + Properties.Settings.Default.Commitish,
                "Untracked files:",
                existingFilename);
        }

        [TestCase, Order(7)]
        public void GitStatusAfterFileDelete()
        {
            string existingFilename = "test.cs";
            this.EnsureTestFileExists(existingFilename);
            this.fileSystem.DeleteFile(this.Enlistment.GetSourcePath(existingFilename));
            this.Enlistment.GetSourcePath(existingFilename).ShouldNotExistOnDisk(this.fileSystem);

            GitHelpers.CheckGitCommandAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "status",
                "On branch " + Properties.Settings.Default.Commitish,
                "nothing to commit, working tree clean");
        }

        [TestCase, Order(8)]
        public void GitWithEnvironmentVariables()
        {
            // The trace info is an error, so we can't use CheckGitCommand().
            // We just want to make sure this doesn't throw an exception.
            ProcessResult result = GitHelpers.InvokeGitAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "branch",
                new Dictionary<string, string>
                {
                    { "GIT_TRACE_PERFORMANCE", "1" },
                    { "git_trace", "1" },
                },
                removeWaitingMessages: false);
            result.Output.ShouldContain("* FunctionalTests");
            result.Errors.ShouldNotContain(ignoreCase: true, unexpectedSubstrings: "exception");
            result.Errors.ShouldContain("trace.c:", "git command:");
        }

        [TestCase, Order(9)]
        public void GitStatusAfterRenameFileIntoRepo()
        {
            string filename = "GitStatusAfterRenameFileIntoRepo.cs";

            // Create the test file in this.Enlistment.EnlistmentRoot as it's outside of src
            // and is cleaned up when the functional tests run
            string filePath = Path.Combine(this.Enlistment.EnlistmentRoot, filename);

            this.fileSystem.WriteAllText(filePath, this.testFileContents);
            filePath.ShouldBeAFile(this.fileSystem).WithContents(this.testFileContents);

            string renamedFileName = Path.Combine("GVFlt_MoveFileTest", "GitStatusAfterRenameFileIntoRepo.cs");
            string renamedFilePath = this.Enlistment.GetSourcePath(renamedFileName);
            this.fileSystem.MoveFile(filePath, renamedFilePath);
            filePath.ShouldNotExistOnDisk(this.fileSystem);
            renamedFilePath.ShouldBeAFile(this.fileSystem);

            GitHelpers.CheckGitCommandAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "status",
                "On branch " + Properties.Settings.Default.Commitish,
                "Untracked files:",
                renamedFileName.Replace('\\', '/'));
        }

        [TestCase, Order(10)]
        public void GitStatusAfterRenameFileOutOfRepo()
        {
            string existingFilename = Path.Combine("Test_EPF_MoveRenameFileTests", "ChangeUnhydratedFileName", "Program.cs");

            // Move the test file to this.Enlistment.EnlistmentRoot as it's outside of src
            // and is cleaned up when the functional tests run
            this.fileSystem.MoveFile(this.Enlistment.GetSourcePath(existingFilename), Path.Combine(this.Enlistment.EnlistmentRoot, "Program.cs"));
            this.Enlistment.GetSourcePath(existingFilename).ShouldNotExistOnDisk(this.fileSystem);

            ProcessResult result = GitProcess.InvokeProcess(this.Enlistment.RepoRoot, "status");
            result.Output.ShouldContain("On branch " + Properties.Settings.Default.Commitish);
            result.Output.ShouldContain("Changes not staged for commit");
            result.Output.ShouldContain("deleted:    Test_EPF_MoveRenameFileTests/ChangeUnhydratedFileName/Program.cs");
        }

        [TestCase, Order(11)]
        public void GitStatusAfterRenameFolderIntoRepo()
        {
            string folderName = "GitStatusAfterRenameFolderIntoRepo";

            // Create the test folder in this.Enlistment.EnlistmentRoot as it's outside of src
            // and is cleaned up when the functional tests run
            string folderPath = Path.Combine(this.Enlistment.EnlistmentRoot, folderName);

            this.fileSystem.CreateDirectory(folderPath);

            string fileName = "GitStatusAfterRenameFolderIntoRepo_file.txt";
            string filePath = Path.Combine(folderPath, fileName);
            this.fileSystem.WriteAllText(filePath, this.testFileContents);
            filePath.ShouldBeAFile(this.fileSystem).WithContents(this.testFileContents);

            this.fileSystem.MoveDirectory(folderPath, this.Enlistment.GetSourcePath(folderName));

            GitHelpers.CheckGitCommandAgainstScalarRepo(
                this.Enlistment.RepoRoot,
                "status -uall",
                "On branch " + Properties.Settings.Default.Commitish,
                "Untracked files:",
                folderName + "/",
                folderName + "/" + fileName);
        }

        private void EnsureTestFileExists(string relativePath)
        {
            string filePath = this.Enlistment.GetSourcePath(relativePath);
            if (!this.fileSystem.FileExists(filePath))
            {
                this.fileSystem.WriteAllText(filePath, this.testFileContents);
            }

            this.Enlistment.GetSourcePath(relativePath).ShouldBeAFile(this.fileSystem);
        }
    }
}
