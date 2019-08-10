using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerTestCase
{
    [TestFixture]
    [Category(Categories.ExtraCoverage)]
    [Category(Categories.NeedsUpdatesForNonVirtualizedMode)]
    public class RepairTests : TestsWithEnlistmentPerTestCase
    {
        [TestCase]
        public void NoFixesNeeded()
        {
            this.Enlistment.UnmountScalar();
            this.Enlistment.Repair(confirm: false);
            this.Enlistment.Repair(confirm: true);
        }

        [TestCase]
        public void FixesCorruptHeadSha()
        {
            this.Enlistment.UnmountScalar();

            string headFilePath = Path.Combine(this.Enlistment.RepoRoot, ".git", "HEAD");
            File.WriteAllText(headFilePath, "0000");
            this.Enlistment.TryMountScalar().ShouldEqual(false, "Scalar shouldn't mount when HEAD is corrupt");

            this.RepairWithoutConfirmShouldNotFix();

            this.RepairWithConfirmShouldFix();
        }

        [TestCase]
        public void FixesCorruptHeadSymRef()
        {
            this.Enlistment.UnmountScalar();

            string headFilePath = Path.Combine(this.Enlistment.RepoRoot, ".git", "HEAD");
            File.WriteAllText(headFilePath, "ref: refs");
            this.Enlistment.TryMountScalar().ShouldEqual(false, "Scalar shouldn't mount when HEAD is corrupt");

            this.RepairWithoutConfirmShouldNotFix();

            this.RepairWithConfirmShouldFix();
        }

        [TestCase]
        public void FixesMissingGitIndex()
        {
            this.Enlistment.UnmountScalar();

            string gitIndexPath = Path.Combine(this.Enlistment.RepoRoot, ".git", "index");
            File.Delete(gitIndexPath);
            this.Enlistment.TryMountScalar().ShouldEqual(false, "Scalar shouldn't mount when git index is missing");

            this.RepairWithoutConfirmShouldNotFix();

            this.RepairWithConfirmShouldFix();
        }

        [TestCase]
        public void FixesGitIndexCorruptedWithBadData()
        {
            this.Enlistment.UnmountScalar();

            string gitIndexPath = Path.Combine(this.Enlistment.RepoRoot, ".git", "index");
            this.CreateCorruptIndexAndRename(
                gitIndexPath,
                (current, temp) =>
                {
                    byte[] badData = Encoding.ASCII.GetBytes("BAD_INDEX");
                    temp.Write(badData, 0, badData.Length);
                });

            string output;
            this.Enlistment.TryMountScalar(out output).ShouldEqual(false, "Scalar shouldn't mount when index is corrupt");
            output.ShouldContain("Index validation failed");

            this.RepairWithoutConfirmShouldNotFix();

            this.RepairWithConfirmShouldFix();
        }

        [TestCase]
        public void FixesGitIndexContainingAllNulls()
        {
            this.Enlistment.UnmountScalar();

            string gitIndexPath = Path.Combine(this.Enlistment.RepoRoot, ".git", "index");

            // Set the contents of the index file to gitIndexPath NULL
            this.CreateCorruptIndexAndRename(
                gitIndexPath,
                (current, temp) =>
                {
                    temp.Write(Enumerable.Repeat<byte>(0, (int)current.Length).ToArray(), 0, (int)current.Length);
                });

            string output;
            this.Enlistment.TryMountScalar(out output).ShouldEqual(false, "Scalar shouldn't mount when index is corrupt");
            output.ShouldContain("Index validation failed");

            this.RepairWithoutConfirmShouldNotFix();

            this.RepairWithConfirmShouldFix();
        }

        [TestCase]
        public void FixesGitIndexCorruptedByTruncation()
        {
            this.Enlistment.UnmountScalar();

            string gitIndexPath = Path.Combine(this.Enlistment.RepoRoot, ".git", "index");

            // Truncate the contents of the index
            this.CreateCorruptIndexAndRename(
                gitIndexPath,
                (current, temp) =>
                {
                    // 20 will truncate the file in the middle of the first entry in the index
                    byte[] currentStartOfIndex = new byte[20];
                    current.Read(currentStartOfIndex, 0, currentStartOfIndex.Length);
                    temp.Write(currentStartOfIndex, 0, currentStartOfIndex.Length);
                });

            string output;
            this.Enlistment.TryMountScalar(out output).ShouldEqual(false, "Scalar shouldn't mount when index is corrupt");
            output.ShouldContain("Index validation failed");

            this.RepairWithoutConfirmShouldNotFix();

            this.RepairWithConfirmShouldFix();
        }

        [TestCase]
        public void FixesCorruptGitConfig()
        {
            this.Enlistment.UnmountScalar();

            string gitIndexPath = Path.Combine(this.Enlistment.RepoRoot, ".git", "config");
            File.WriteAllText(gitIndexPath, "[cor");

            this.Enlistment.TryMountScalar().ShouldEqual(false, "Scalar shouldn't mount when git config is missing");

            this.RepairWithoutConfirmShouldNotFix();

            this.Enlistment.Repair(confirm: true);
            ProcessResult result = GitProcess.InvokeProcess(this.Enlistment.RepoRoot, "remote add origin " + this.Enlistment.RepoUrl);
            result.ExitCode.ShouldEqual(0, result.Errors);
            this.Enlistment.MountScalar();
        }

        private void CreateCorruptIndexAndRename(string indexPath, Action<FileStream, FileStream> corruptionAction)
        {
            string tempIndexPath = indexPath + ".lock";
            using (FileStream currentIndexStream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream tempIndexStream = new FileStream(tempIndexPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                corruptionAction(currentIndexStream, tempIndexStream);
            }

            File.Delete(indexPath);
            File.Move(tempIndexPath, indexPath);
        }

        private void RepairWithConfirmShouldFix()
        {
            this.Enlistment.Repair(confirm: true);
            this.Enlistment.MountScalar();
        }

        private void RepairWithoutConfirmShouldNotFix()
        {
            this.Enlistment.Repair(confirm: false);
            this.Enlistment.TryMountScalar().ShouldEqual(false, "Repair without confirm should not fix the enlistment");
        }
    }
}
