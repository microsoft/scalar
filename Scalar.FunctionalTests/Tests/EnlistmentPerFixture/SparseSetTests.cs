using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    [Category(Categories.GitCommands)]
    public class SparseSetTests : TestsWithEnlistmentPerFixture
    {
        private const string AddFailureMessage = "Failed to add folders to sparse-checkout";

        private const string FolderDotGit = ".git";
        private const string FolderDeleteFileTests = "DeleteFileTests";
        private const string FolderEnumerateAndReadTestFiles = "EnumerateAndReadTestFiles";
        private const string FolderFileNameEncoding = "FilenameEncoding";
        private const string FolderGitCommandsTests = "GitCommandsTests";
        private const string FolderGVFS = "GVFS";
        private const string FolderScripts = "Scripts";
        private const string FolderTest_ConflictTests = "Test_ConflictTests";
        private const string FolderTest_MoveRenameFileTests = "Test_EPF_MoveRenameFileTests";
        private const string FolderTest_MoveRenameFileTests2 = "Test_EPF_MoveRenameFileTests_2";
        private const string FolderTest_WorkingDirectoryTests = "Test_EPF_WorkingDirectoryTests";
        private const string FolderTrailingSlashTests = "TrailingSlashTests";

        private SystemIORunner fileSystem;

        public SparseSetTests() : base(fullClone: false)
        {
            this.fileSystem = new SystemIORunner();
        }

        [TestCase, Order(1)]
        public void SparseSet()
        {
            // Simple Test Adding FileNameEncoding and verifying it exists afterwards
            this.VerifyDirectory(this.Enlistment.RepoRoot, new List<string>
                {
                    FolderDotGit
                });

            ScalarProcess scalar = new ScalarProcess(this.Enlistment);
            string[] sparseModeFolders = new string[] { FolderFileNameEncoding };
            scalar.SparseSet(sparseModeFolders);

            this.VerifyDirectory(this.Enlistment.RepoRoot, new List<string>
                {
                    FolderDotGit,
                    FolderFileNameEncoding
                });
        }

        [TestCase, Order(2)]
        public void SparseSetOneLevelDeep()
        {
            // Add GitCommands/DeleteFileTests, Verify the only directory added to GitCommands is DeleteFileTests
            ScalarProcess scalar = new ScalarProcess(this.Enlistment);
            string addFolder = Path.Combine(FolderGitCommandsTests, FolderDeleteFileTests);
            string[] sparseModeFolders = new string[] { addFolder };
            scalar.SparseSet(sparseModeFolders);

            this.VerifyDirectory(this.Enlistment.RepoRoot, new List<string>
            {
                FolderDotGit,
                FolderFileNameEncoding,
                FolderGitCommandsTests
            });

            // Verify only 1 folder exists (actual repo has many folders)
            this.VerifyDirectory(Path.Combine(this.Enlistment.RepoRoot, FolderGitCommandsTests), new List<string>
            {
                FolderDeleteFileTests
            });
        }

        [TestCase, Order(3)]
        public void SparseSetNameAlreadyExists()
        {
            // Create a Folder (Scripts) that exists in the repo, with a file that does not
            // Verify the Folder can be successfully added with new files populated
            string scriptsDirectory = this.Enlistment.GetSourcePath(FolderScripts);
            string testFile = Path.Combine(scriptsDirectory, "TestFile");
            this.fileSystem.CreateDirectory(scriptsDirectory);
            this.fileSystem.CreateEmptyFile(testFile);

            ScalarProcess scalar = new ScalarProcess(this.Enlistment);
            string[] sparseModeFolders = new string[] { FolderScripts };
            scalar.SparseSet(sparseModeFolders);

            this.VerifyDirectory(this.Enlistment.RepoRoot, new List<string>
            {
                FolderDotGit,
                FolderFileNameEncoding,
                FolderGitCommandsTests,
                FolderScripts
            });

            // Should include our 1 new added file and 5 existing files from the repo
            string[] filesOneLevelDeepAfterAdd = Directory.GetFiles(scriptsDirectory);
            filesOneLevelDeepAfterAdd.Length.ShouldEqual(6);
        }

        [TestCase, Order(4)]
        public void SparseSetFileAlreadyExists()
        {
            // Create a Folder that exists in the repo, with a file that also exists.  This should make the 'add' fail.
            // Verify that deleting the conflicting file and then reperforming the add causes all directories to show up.
            string folderEnumerateDirectory = this.Enlistment.GetSourcePath(FolderEnumerateAndReadTestFiles);
            string existingFile = Path.Combine(folderEnumerateDirectory, "_a");
            this.fileSystem.CreateDirectory(folderEnumerateDirectory);
            this.fileSystem.CreateEmptyFile(existingFile);

            // Contents before add
            this.VerifyContentsForSparseSetFileAlreadyExists();

            // If a file already exists that would conflict with a sparse checkout, the add should fail
            ScalarProcess scalar = new ScalarProcess(this.Enlistment);
            string[] sparseModeFolders = new string[] { FolderEnumerateAndReadTestFiles };
            string result = scalar.SparseSet(sparseModeFolders);
            result.ShouldContain(AddFailureMessage);

            // Contents should be unchanged after failure
            this.VerifyContentsForSparseSetFileAlreadyExists();

            // After deleting the conflicting file you should be able to add
            this.fileSystem.DeleteFile(existingFile);
            result = scalar.SparseSet(sparseModeFolders);
            result.ShouldNotContain(true, AddFailureMessage);

            // Should now have multiple files under EnumerateAndReadTestFiles
            Directory.GetFiles(folderEnumerateDirectory).Length.ShouldEqual(18);
        }

        [TestCase, Order(5)]
        public void SparseSetOnMovedFolder()
        {
            // Add GVFS, Move GVFS to GVFS2, then Add GVFS again
            // We should only have a GVFS2 folder as it should detect the folder has been already added
            string gvfsDirectory = this.Enlistment.GetSourcePath(FolderGVFS);
            string gvfs2 = "GVFS2";
            string gvfsDirectory2 = this.Enlistment.GetSourcePath(gvfs2);

            // Add GVFS
            ScalarProcess scalar = new ScalarProcess(this.Enlistment);
            string[] sparseModeFolders = new string[] { FolderGVFS };
            scalar.SparseSet(sparseModeFolders);

            this.VerifyDirectory(this.Enlistment.RepoRoot, new List<string>
            {
                FolderDotGit,
                FolderEnumerateAndReadTestFiles,
                FolderFileNameEncoding,
                FolderGitCommandsTests,
                FolderGVFS,
                FolderScripts
            });

            // Move GVFS to GVFS2
            this.fileSystem.MoveDirectory(gvfsDirectory, gvfsDirectory2);

            // Add GVFS again
            scalar.SparseSet(sparseModeFolders);

            // Only GVFS2 should exist
            this.VerifyDirectory(this.Enlistment.RepoRoot, new List<string>
            {
                FolderDotGit,
                FolderEnumerateAndReadTestFiles,
                FolderFileNameEncoding,
                FolderGitCommandsTests,
                gvfs2,
                FolderScripts
            });

            // Moving GVFS2 to GVFS should be successful
            this.fileSystem.MoveDirectory(gvfsDirectory2, gvfsDirectory);
            this.VerifyDirectory(this.Enlistment.RepoRoot, new List<string>
            {
                FolderDotGit,
                FolderEnumerateAndReadTestFiles,
                FolderFileNameEncoding,
                FolderGitCommandsTests,
                FolderGVFS,
                FolderScripts
            });
        }

        [TestCase, Order(6)]
        public void SparseSetWithOneFolderConflict()
        {
            // Create a conflict file Test_EPF_MoveRenameFileTests_2/_a
            // Add Test_EPF_MoveRenameFileTests / Test_EPF_MoveRenameFileTests_2, this should fail since Test_EPF_MoveRenameFileTests_2 has a conflict
            // Checkout out a new branch and attempt to go back to the previous branch.  You won't be able to switch back to the previous branch. *current scalar bug*
            // Deleting the conflict file will allow you to switch back to the branch and Test_EPF_MoveRenameFileTests / Test_EPF_MoveRenameFileTests_2 will be populated
            string folderRenameDirectory = this.Enlistment.GetSourcePath(FolderTest_MoveRenameFileTests);
            string folderRenameDirectory2 = this.Enlistment.GetSourcePath(FolderTest_MoveRenameFileTests2);
            string existingFile = Path.Combine(folderRenameDirectory2, "RunUnitTests.bat");
            this.fileSystem.CreateDirectory(folderRenameDirectory);
            this.fileSystem.CreateDirectory(folderRenameDirectory2);
            this.fileSystem.CreateEmptyFile(existingFile);

            this.VerifyContentsSparseSetWithOneFolderConflict();

            // One folder conflict should fail the entire add
            ScalarProcess scalar = new ScalarProcess(this.Enlistment);
            string[] sparseModeFolders = new string[] { FolderTest_MoveRenameFileTests, FolderTest_MoveRenameFileTests2 };
            string result = scalar.SparseSet(sparseModeFolders);
            result.ShouldContain(AddFailureMessage);

            // Results should be unchanged
            this.VerifyContentsSparseSetWithOneFolderConflict();

            // Checkout new_branch
            GitProcess.Invoke(this.Enlistment.RepoRoot, "checkout -b new_branch");

            // Attempt to go back to previous branch
            // This will fail because of previous failed add
            ProcessResult checkoutResult = GitProcess.InvokeProcess(this.Enlistment.RepoRoot, "checkout " + this.Enlistment.Commitish);
            checkoutResult.Errors.ShouldContain("error: The following working tree files would be overwritten by sparse checkout update:\r\n\tTest_EPF_MoveRenameFileTests_2/RunUnitTests.bat\r\n\r\nAborting\r\n");

            // Remove the conflicting file
            this.fileSystem.DeleteFile(existingFile);

            // Now that the conflict is removed we should be able to switch branches
            checkoutResult = GitProcess.InvokeProcess(this.Enlistment.RepoRoot, "checkout " + this.Enlistment.Commitish);
            checkoutResult.Errors.ShouldContain("Switched to branch 'FunctionalTests/20180214'\r\n");

            // Verify the folders are now populated
            this.VerifyDirectory(folderRenameDirectory, new List<string>
            {
                "ChangeNestedUnhydratedFileNameCase",
                "ChangeUnhydratedFileName",
                "MoveUnhydratedFileToDotGitFolder"
            });

            this.VerifyDirectory(folderRenameDirectory2, new List<string>
            {
                "MoveUnhydratedFileToOverwriteFullFileAndWrite",
                "MoveUnhydratedFileToOverwriteUnhydratedFileAndWrite",
            });
        }

        [TestCase, Order(7)]
        public void AddWhileInMergeConflict()
        {
            // Attempt to add while you have a cherry-pick merge conflict.  It will say it failed.
            // Abort the cherry-pick, switch branches, and switch back. Now the folders will be fully populated.
            // The correct behavior should be that if the 'add' fails the folders should not be added to sparse
            string conflictFilename = "conflict";
            string fileToConflict = this.Enlistment.GetSourcePath(conflictFilename);

            // Make a new commit on a branch
            GitProcess.Invoke(this.Enlistment.RepoRoot, "checkout -b branch_with_conflict");
            this.fileSystem.WriteAllText(fileToConflict, "ABC");
            GitProcess.Invoke(this.Enlistment.RepoRoot, "add " + conflictFilename);
            string commitResult = GitProcess.Invoke(this.Enlistment.RepoRoot, "commit -m \"conflict on branch_with_conflict\"");

            // create a conflicting commit on the originating branch
            int startIndex = commitResult.IndexOf(' ');
            string commitId = commitResult.Substring(startIndex, commitResult.IndexOf(']') - startIndex);
            GitProcess.Invoke(this.Enlistment.RepoRoot, "checkout " + this.Enlistment.Commitish);
            this.fileSystem.WriteAllText(fileToConflict, "DEF");
            GitProcess.Invoke(this.Enlistment.RepoRoot, "add " + conflictFilename);
            GitProcess.Invoke(this.Enlistment.RepoRoot, "commit -m \"conflict on master\"");

            // Attempt to cherry-pick the commit we know will result in a merge conflict
            GitProcess.Invoke(this.Enlistment.RepoRoot, "cherry-pick " + commitId);

            // Should not be able to add which we have a merge conflict
            ScalarProcess scalar = new ScalarProcess(this.Enlistment);
            string[] sparseModeFolders = new string[] { FolderTrailingSlashTests };
            string result = scalar.SparseSet(sparseModeFolders);
            result.ShouldContain(AddFailureMessage);

            // New directory should not be listed because of the error
            this.VerifyDirectory(this.Enlistment.RepoRoot, new List<string>
            {
                FolderDotGit,
                FolderEnumerateAndReadTestFiles,
                FolderFileNameEncoding,
                FolderGitCommandsTests,
                FolderGVFS,
                FolderScripts,
                FolderTest_MoveRenameFileTests,
                FolderTest_MoveRenameFileTests2
             });

            // Fix the error, switch to the new branch, and switch back and we should have all directories
            GitProcess.Invoke(this.Enlistment.RepoRoot, "cherry-pick --abort");
            GitProcess.Invoke(this.Enlistment.RepoRoot, "checkout branch_with_conflict");
            GitProcess.Invoke(this.Enlistment.RepoRoot, "checkout " + this.Enlistment.Commitish);

            // New directory should now exist
            this.VerifyDirectory(this.Enlistment.RepoRoot, new List<string>
             {
                FolderDotGit,
                FolderEnumerateAndReadTestFiles,
                FolderFileNameEncoding,
                FolderGitCommandsTests,
                FolderGVFS,
                FolderScripts,
                FolderTest_MoveRenameFileTests,
                FolderTest_MoveRenameFileTests2,
                FolderTrailingSlashTests
            });
        }

        [TestCase, Order(8)]
        public void CherryPickWithChangesInAndOutOfTheCone()
        {
            // Cherry-pick a commit with changes in and out of a cone
            // Adding the directory out of the cone should reflect the change
            GitProcess.Invoke(this.Enlistment.RepoRoot, "cherry-pick 316a387485d58d2f83cfd60dbc4fe54f5194055e");

            // Add the folder with the commit out of the cone
            ScalarProcess scalar = new ScalarProcess(this.Enlistment);
            string[] sparseModeFolders = new string[] { FolderTest_WorkingDirectoryTests };
            string result = scalar.SparseSet(sparseModeFolders);

            // New Folder should exist
            string newFolder = this.Enlistment.GetSourcePath(FolderTest_WorkingDirectoryTests);
            this.VerifyDirectory(this.Enlistment.RepoRoot, new List<string>
             {
                FolderDotGit,
                FolderEnumerateAndReadTestFiles,
                FolderFileNameEncoding,
                FolderGitCommandsTests,
                FolderGVFS,
                FolderScripts,
                FolderTest_MoveRenameFileTests,
                FolderTest_MoveRenameFileTests2,
                newFolder,
                FolderTrailingSlashTests
            });

            // New changes should exist in the recently added file
            string fileWithCherryPick = Path.Combine(newFolder, "AllNullObjectRedownloaded.txt");
            this.fileSystem.ReadAllText(fileWithCherryPick).ShouldContain("TestTest contents for AllNullObjectRedownloaded");
        }

        [TestCase, Order(9)]
        public void MergeChangesOutOfTheConeWithConflict()
        {
            // This test creates a merge conflict outside the cone
            // It shows that the flow results in the conflicting files being present on disk.
            // When the conflict is resolved the files are still present.  They should not be. *current scalar bug*
            // When the directory with the prior conflict is added, all files and directories should then be present
            string conflictSourceBranch = "FunctionalTests/20170206_Conflict_Source";
            string conflictTargetBranch = "FunctionalTests/20170206_Conflict_Target";
            GitProcess.Invoke(this.Enlistment.RepoRoot, $"checkout {conflictTargetBranch}");
            GitProcess.Invoke(this.Enlistment.RepoRoot, $"checkout {conflictSourceBranch}");
            ProcessResult checkoutResult = GitProcess.InvokeProcess(this.Enlistment.RepoRoot, $"merge {conflictTargetBranch}");
            checkoutResult.Output.ShouldContain("Merge conflict");

            // status should show conflicted files
            ProcessResult status = GitProcess.InvokeProcess(this.Enlistment.RepoRoot, "status");
            status.Output.Contains("Test_ConflictTests/AddedFiles/AddedByBothDifferentContent.txt");
            status.Output.Contains("Test_ConflictTests/ModifiedFiles/ChangeInSource.txt");
            status.Output.Contains("Test_ConflictTests/ModifiedFiles/ChangeInTarget.txt");
            status.Output.Contains("Test_ConflictTests/ModifiedFiles/ConflictingChange.txt");
            status.Output.Contains("Test_ConflictTests/ModifiedFiles/SuccessfulMerge.txt");

            // Verify files in conflict appear
            this.VerifyContentsMergeConflict();

            // Fix the merge conflict
            GitProcess.InvokeProcess(this.Enlistment.RepoRoot, $"merge --abort");

            // Conflicting files still appear.  What *should* happen is that the conflicting files are removed
            this.VerifyContentsMergeConflict();

            // Add the folder with the conflict out of the cone
            ScalarProcess scalar = new ScalarProcess(this.Enlistment);
            string[] sparseModeFolders = new string[] { FolderTest_ConflictTests };
            string result = scalar.SparseSet(sparseModeFolders);
            result.ShouldNotContain(true, AddFailureMessage);

            // Now *all* files and directories should appear
            this.VerifyDirectory(this.Enlistment.GetSourcePath(FolderTest_ConflictTests), new List<string>
            {
                "AddedFiles",
                "DeletedFiles",
                "ModifiedFiles",
                "RenamedFiles"
            });

            Directory.GetFiles(this.Enlistment.GetSourcePath(FolderTest_ConflictTests, "AddedFiles")).Count().ShouldEqual(4);
            Directory.GetFiles(this.Enlistment.GetSourcePath(FolderTest_ConflictTests, "DeletedFiles")).Count().ShouldEqual(1);
            Directory.GetFiles(this.Enlistment.GetSourcePath(FolderTest_ConflictTests, "ModifiedFiles")).Count().ShouldEqual(6);
            Directory.GetFiles(this.Enlistment.GetSourcePath(FolderTest_ConflictTests, "RenamedFiles")).Count().ShouldEqual(4);
        }

        private void VerifyContentsMergeConflict()
        {
            this.VerifyDirectory(this.Enlistment.GetSourcePath(FolderTest_ConflictTests), new List<string>
            {
                "AddedFiles",
                "ModifiedFiles"
            });

            this.VerifyFiles(this.Enlistment.GetSourcePath(FolderTest_ConflictTests, "AddedFiles"), new List<string>
            {
                "AddedByBothDifferentContent.txt"
            });

            this.VerifyFiles(this.Enlistment.GetSourcePath(FolderTest_ConflictTests, "ModifiedFiles"), new List<string>
            {
                "ChangeInSource.txt",
                "ChangeInTarget.txt",
                "ConflictingChange.txt",
                "SuccessfulMerge.txt"
            });
        }

        private void VerifyContentsSparseSetWithOneFolderConflict()
        {
            this.VerifyDirectory(this.Enlistment.RepoRoot, new List<string>
            {
                FolderDotGit,
                FolderEnumerateAndReadTestFiles,
                FolderFileNameEncoding,
                FolderGitCommandsTests,
                FolderGVFS,
                FolderScripts,
                FolderTest_MoveRenameFileTests,
                FolderTest_MoveRenameFileTests2
            });

            string folderRenameDirectory = this.Enlistment.GetSourcePath(FolderTest_MoveRenameFileTests);
            string folderRenameDirectory2 = this.Enlistment.GetSourcePath(FolderTest_MoveRenameFileTests2);
            this.VerifyDirectory(folderRenameDirectory, new List<string> { });
            this.VerifyDirectory(folderRenameDirectory2, new List<string> { });
            string[] filesfolderRenameDirectory = Directory.GetFiles(folderRenameDirectory);
            string[] filesfolderRenameDirectory2 = Directory.GetFiles(folderRenameDirectory2);
            filesfolderRenameDirectory.Length.ShouldEqual(0);
            filesfolderRenameDirectory2.Length.ShouldEqual(1);
        }

        private void VerifyContentsForSparseSetFileAlreadyExists()
        {
            string existingFile = this.Enlistment.GetSourcePath(FolderEnumerateAndReadTestFiles, "_a");

            this.VerifyDirectory(this.Enlistment.RepoRoot, new List<string>
            {
                FolderDotGit,
                FolderEnumerateAndReadTestFiles,
                FolderFileNameEncoding,
                FolderGitCommandsTests,
                FolderScripts
            });

            string folderEnumerateDirectory = this.Enlistment.GetSourcePath(FolderEnumerateAndReadTestFiles);
            string[] filesOneLevelDeepAfterAdd = Directory.GetFiles(folderEnumerateDirectory);
            filesOneLevelDeepAfterAdd.Length.ShouldEqual(1);
            filesOneLevelDeepAfterAdd[0].ShouldEqual(existingFile);
            this.fileSystem.ReadAllText(existingFile).ShouldBeEmpty();
            this.VerifyDirectory(folderEnumerateDirectory, new List<string> { });
        }

        private void VerifyDirectory(string directory, List<string> expectedFolders)
        {
            string[] dirs = Directory.GetDirectories(directory);
            dirs.Length.ShouldEqual(expectedFolders.Count);
            IEnumerable<string> expectedFoldersWithPath = expectedFolders.Select(x => Path.Combine(directory, x));
            Array.Sort(dirs);
            dirs.ShouldMatchInOrder(expectedFoldersWithPath);
        }

        private void VerifyFiles(string directory, List<string> expectedFiles)
        {
            string[] files = Directory.GetFiles(directory);
            files.Length.ShouldEqual(expectedFiles.Count);
            IEnumerable<string> expectedFilesWithPath = expectedFiles.Select(x => Path.Combine(directory, x));
            Array.Sort(files);
            files.ShouldMatchInOrder(expectedFilesWithPath);
        }
    }
}
