using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Properties;
using Scalar.FunctionalTests.Should;
using System;
using System.IO;
using System.Threading;

namespace Scalar.FunctionalTests.Tests.GitCommands
{
    [TestFixtureSource(typeof(GitRepoTests), nameof(GitRepoTests.ValidateWorkingTree))]
    [Category(Categories.GitCommands)]
    public class StatusTests : GitRepoTests
    {
        public StatusTests(Settings.ValidateWorkingTreeMode validateWorkingTree)
            : base(enlistmentPerTest: true, validateWorkingTree: validateWorkingTree)
        {
        }

        [TestCase]
        public void MoveFileIntoDotGitDirectory()
        {
            string srcPath = @"Readme.md";
            string dstPath = Path.Combine(".git", "destination.txt");

            this.MoveFile(srcPath, dstPath);
            this.ValidateGitCommand("status");
        }

        [TestCase]
        public void DeleteThenCreateThenDeleteFile()
        {
            string srcPath = @"Readme.md";

            this.DeleteFile(srcPath);
            this.ValidateGitCommand("status");
            this.CreateFile("Testing", srcPath);
            this.ValidateGitCommand("status");
            this.DeleteFile(srcPath);
            this.ValidateGitCommand("status");
        }

        [TestCase]
        public void CreateFileWithoutClose()
        {
            string srcPath = @"CreateFileWithoutClose.md";
            this.CreateFileWithoutClose(srcPath);
            this.ValidGitStatusWithRetry(srcPath);
        }

        [TestCase]
        public void WriteWithoutClose()
        {
            string srcPath = @"Readme.md";
            using (IDisposable file = this.ReadFileAndWriteWithoutClose(srcPath, "More Stuff"))
            {
                this.ValidGitStatusWithRetry(srcPath);
            }
        }

        [TestCase]
        public void AppendFileUsingBash()
        {
            // Bash will perform the append using '>>' which will cause KAUTH_VNODE_APPEND_DATA to be sent without hydration
            // Other Runners may cause hydration before append
            BashRunner bash = new BashRunner();
            string filePath = Path.Combine("Test_EPF_UpdatePlaceholderTests", "LockToPreventUpdate", "test.txt");
            string content = "Apended Data";
            string virtualFile = Path.Combine(this.Enlistment.RepoRoot, filePath);
            string controlFile = Path.Combine(this.ControlGitRepo.RootPath, filePath);
            bash.AppendAllText(virtualFile, content);
            bash.AppendAllText(controlFile, content);

            this.ValidateGitCommand("status");

            // We check the contents after status, to ensure this check didn't cause the hydration
            string appendedContent = string.Concat("Commit2LockToPreventUpdate \r\n", content);
            virtualFile.ShouldBeAFile(this.FileSystem).WithContents(appendedContent);
            controlFile.ShouldBeAFile(this.FileSystem).WithContents(appendedContent);
        }

        private void ValidGitStatusWithRetry(string srcPath)
        {
            try
            {
                this.ValidateGitCommand("status");
            }
            catch (Exception ex)
            {
                Thread.Sleep(1000);
                this.ValidateGitCommand("status");
                Assert.Fail("{0} was succesful on the second try, but failed on first: {1}", nameof(this.ValidateGitCommand), ex.Message);
            }
        }
    }
}
