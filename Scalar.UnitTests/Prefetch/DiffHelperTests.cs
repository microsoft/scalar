using NUnit.Framework;
using Scalar.Common.Git;
using Scalar.Common.Prefetch.Git;
using Scalar.Tests;
using Scalar.Tests.Should;
using Scalar.UnitTests.Mock.Common;
using Scalar.UnitTests.Mock.Git;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Scalar.UnitTests.Prefetch
{
    [TestFixtureSource(typeof(DataSources), nameof(DataSources.AllBools))]
    public class DiffHelperTests
    {
        public DiffHelperTests(bool symLinkSupport)
        {
            this.IncludeSymLinks = symLinkSupport;
        }

        public bool IncludeSymLinks { get; set; }

        // Make two commits. The first should look like this:
        // recursiveDelete
        // recursiveDelete/subfolder
        // recursiveDelete/subfolder/childFile.txt
        // fileToBecomeFolder
        // fileToDelete.txt
        // fileToEdit.txt
        // fileToRename.txt
        // fileToRenameEdit.txt
        // folderToBeFile
        // folderToBeFile/childFile.txt
        // folderToDelete
        // folderToDelete/childFile.txt
        // folderToEdit
        // folderToEdit/childFile.txt
        // folderToRename
        // folderToRename/childFile.txt
        // symLinkToBeCreated.txt
        //
        // The second should follow the action indicated by the file/folder name:
        // eg. recursiveDelete should run "rmdir /s/q recursiveDelete"
        // eg. folderToBeFile should be deleted and replaced with a file of the same name
        // Note that each childFile.txt should have unique contents, but is only a placeholder to force git to add a folder.
        //
        // Then to generate the diffs, run:
        // git diff-tree -r -t Head~1 Head > forward.txt
        // git diff-tree -r -t Head Head ~1 > backward.txt
        [TestCase]
        public void CanParseDiffForwards()
        {
            MockTracer tracer = new MockTracer();
            DiffHelper diffForwards = new DiffHelper(tracer, new MockScalarEnlistment(), new List<string>(), new List<string>(), includeSymLinks: this.IncludeSymLinks);
            diffForwards.ParseDiffFile(GetDataPath("forward.txt"));

            // File added, file edited, file renamed, folder => file, edit-rename file, SymLink added (if applicable)
            // Children of: Add folder, Renamed folder, edited folder, file => folder
            diffForwards.RequiredBlobs.Count.ShouldEqual(diffForwards.ShouldIncludeSymLinks ? 10 : 9);

            diffForwards.FileAddOperations.ContainsKey("3bd509d373734a9f9685d6a73ba73324f72931e3").ShouldEqual(diffForwards.ShouldIncludeSymLinks);

            // File deleted, folder deleted, file > folder, edit-rename
            diffForwards.FileDeleteOperations.Count.ShouldEqual(4);

            // Includes children of: Recursive delete folder, deleted folder, renamed folder, and folder => file
            diffForwards.TotalFileDeletes.ShouldEqual(8);

            // Folder created, folder edited, folder deleted, folder renamed (add + delete),
            // folder => file, file => folder, recursive delete (top-level only)
            diffForwards.DirectoryOperations.Count.ShouldEqual(8);

            // Should also include the deleted folder of recursive delete
            diffForwards.TotalDirectoryOperations.ShouldEqual(9);
        }

        // Parses Diff B => A
        [TestCase]
        public void CanParseBackwardsDiff()
        {
            MockTracer tracer = new MockTracer();
            DiffHelper diffBackwards = new DiffHelper(tracer, new Mock.Common.MockScalarEnlistment(), new List<string>(), new List<string>(), includeSymLinks: this.IncludeSymLinks);
            diffBackwards.ParseDiffFile(GetDataPath("backward.txt"));

            // File > folder, deleted file, edited file, renamed file, rename-edit file
            // Children of file > folder, renamed folder, deleted folder, recursive delete file, edited folder
            diffBackwards.RequiredBlobs.Count.ShouldEqual(10);

            // File added, folder > file, moved folder, added folder
            diffBackwards.FileDeleteOperations.Count.ShouldEqual(4);

            // Also includes, the children of: Folder added, folder renamed, file => folder
            diffBackwards.TotalFileDeletes.ShouldEqual(7);

            // Folder created, folder edited, folder deleted, folder renamed (add + delete),
            // folder => file, file => folder, recursive delete (include subfolder)
            diffBackwards.TotalDirectoryOperations.ShouldEqual(9);
        }

        // Delete a folder with two sub folders each with a single file
        // Readd it with a different casing and same contents
        [TestCase]
        public void ParsesCaseChangesAsAdds()
        {
            MockTracer tracer = new MockTracer();
            DiffHelper diffBackwards = new DiffHelper(tracer, new Mock.Common.MockScalarEnlistment(), new List<string>(), new List<string>(), includeSymLinks: this.IncludeSymLinks);
            diffBackwards.ParseDiffFile(GetDataPath("caseChange.txt"));

            diffBackwards.RequiredBlobs.Count.ShouldEqual(2);
            diffBackwards.FileAddOperations.Sum(list => list.Value.Count).ShouldEqual(2);

            diffBackwards.FileDeleteOperations.Count.ShouldEqual(0);
            diffBackwards.TotalFileDeletes.ShouldEqual(0);

            diffBackwards.DirectoryOperations.ShouldNotContain(entry => entry.Operation == DiffTreeResult.Operations.Delete);
            diffBackwards.TotalDirectoryOperations.ShouldEqual(3);
        }

        [TestCase]
        public void DetectsFailuresInDiffTree()
        {
            MockTracer tracer = new MockTracer();
            MockGitProcess gitProcess = new MockGitProcess();
            gitProcess.SetExpectedCommandResult("diff-tree -r -t sha1 sha2", () => new GitProcess.Result(string.Empty, string.Empty, 1));

            DiffHelper diffBackwards = new DiffHelper(tracer, new Mock.Common.MockScalarEnlistment(), gitProcess, new List<string>(), new List<string>(), includeSymLinks: this.IncludeSymLinks);
            diffBackwards.PerformDiff("sha1", "sha2");
            diffBackwards.HasFailures.ShouldEqual(true);
        }

        [TestCase]
        public void DetectsFailuresInLsTree()
        {
            MockTracer tracer = new MockTracer();
            MockGitProcess gitProcess = new MockGitProcess();
            gitProcess.SetExpectedCommandResult("ls-tree -r -t sha1", () => new GitProcess.Result(string.Empty, string.Empty, 1));

            DiffHelper diffBackwards = new DiffHelper(tracer, new Mock.Common.MockScalarEnlistment(), gitProcess, new List<string>(), new List<string>(), includeSymLinks: this.IncludeSymLinks);
            diffBackwards.PerformDiff(null, "sha1");
            diffBackwards.HasFailures.ShouldEqual(true);
        }

        [TestCase]
        public void GenerateRecursiveAndParentPathSets()
        {
            char dir = Path.DirectorySeparatorChar;
            List<string> folderList = new List<string>();
            CheckGenerateRecursiveAndParentPathSets(
                folderList,
                expectedParentFolders: new HashSet<string>(),
                expectedMaxFolderPathDepth: 0);

            folderList = new List<string> { $"A{dir}" };
            CheckGenerateRecursiveAndParentPathSets(
                folderList,
                expectedParentFolders: new HashSet<string>(),
                expectedMaxFolderPathDepth: 1);

            folderList = new List<string> { $"A{dir}", $"A{dir}B{dir}" };
            CheckGenerateRecursiveAndParentPathSets(
                folderList,
                expectedParentFolders: new HashSet<string> { $"A{dir}" },
                expectedMaxFolderPathDepth: 2);

            folderList = new List<string> { $"A{dir}", $"A{dir}B{dir}", $"C{dir}" };
            CheckGenerateRecursiveAndParentPathSets(
                folderList,
                expectedParentFolders: new HashSet<string> { $"A{dir}" },
                expectedMaxFolderPathDepth: 2);

            folderList = new List<string> { $"A{dir}", $"A{dir}B{dir}", $"C{dir}", $"G{dir}H{dir}I{dir}J{dir}" };
            CheckGenerateRecursiveAndParentPathSets(
                folderList,
                expectedParentFolders: new HashSet<string> { $"A{dir}", $"G{dir}", $"G{dir}H{dir}", $"G{dir}H{dir}I{dir}" },
                expectedMaxFolderPathDepth: 4);
        }

        [TestCase]
        public void PathMatchesFoldersReturnsFalseWithNoFoldersList()
        {
            MockTracer tracer = new MockTracer();
            DiffHelper diffHelper = new DiffHelper(tracer, new MockScalarEnlistment(), fileList: new List<string>(), folderList: new List<string>(), includeSymLinks: false);

            diffHelper.PathMatchesFolders("a.txt").ShouldBeFalse("Paths (even in root) should not match if there is no folders list");
        }

        [TestCase]
        public void PathMatchesFoldersTests()
        {
            char dir = Path.DirectorySeparatorChar;
            List<string> folderList = new List<string>
            {
                $"A{dir}",
                $"a{dir}b{dir}",
                $"C{dir}",
                $"G{dir}H{dir}I{dir}J{dir}"
            };

            MockTracer tracer = new MockTracer();
            DiffHelper diffHelper = new DiffHelper(tracer, new MockScalarEnlistment(), fileList: new List<string>(), folderList: folderList, includeSymLinks: false);

            diffHelper.PathMatchesFolders("a").ShouldBeTrue("Paths in the root should always be included");
            diffHelper.PathMatchesFolders("C.txt").ShouldBeTrue("Paths in the root should always be included");

            diffHelper.PathMatchesFolders($"A{dir}D{dir}foo.txt").ShouldBeTrue("Descendants of folders in the list should be included");
            diffHelper.PathMatchesFolders($"A{dir}D{dir}E{dir}foo.txt").ShouldBeTrue("Descendants of folders in the list should be included");
            diffHelper.PathMatchesFolders($"a{dir}d{dir}e{dir}FOO.txt").ShouldBeTrue("Descendants of folders in the list should be included");
            diffHelper.PathMatchesFolders($"C{dir}bar.txt").ShouldBeTrue("Descendants of folders in the list should be included");

            diffHelper.PathMatchesFolders($"G{dir}foo.txt").ShouldBeTrue("Immediate children of intermediate folders in the list should be included");
            diffHelper.PathMatchesFolders($"G{dir}H{dir}foo.txt").ShouldBeTrue("Immediate children of intermediate folders in the list should be included");
            diffHelper.PathMatchesFolders($"G{dir}H{dir}I{dir}foo.txt").ShouldBeTrue("Immediate children of intermediate folders in the list should be included");
            diffHelper.PathMatchesFolders($"g{dir}h{dir}i{dir}foo.txt").ShouldBeTrue("Immediate children of intermediate folders in the list should be included");

            // Paths that are not children/descendants should not match
            diffHelper.PathMatchesFolders($"B{dir}foo.txt").ShouldBeFalse("Files that are not children/descendants should not be included");
            diffHelper.PathMatchesFolders($"B{dir}D{dir}baz.txt").ShouldBeFalse("Files that are not children/descendants should not be included");

            // Paths that are not descendants (and not immediate children) of intermediate folders should not match
            diffHelper.PathMatchesFolders($"G{dir}H{dir}Z{dir}foo.txt").ShouldBeFalse("Files that are not children/descendants should not be included");
            diffHelper.PathMatchesFolders($"G{dir}H{dir}I{dir}Z{dir}foo.txt").ShouldBeFalse("Files that are not children/descendants should not be included");
        }

        private static void CheckGenerateRecursiveAndParentPathSets(
            IEnumerable<string> folderList,
            HashSet<string> expectedParentFolders,
            int expectedMaxFolderPathDepth)
        {
            HashSet<string> parentFolders;
            HashSet<string> recursiveParents;
            int maxRecursiveDepth;

            HashSet<string> expectedRecursiveFolders = new HashSet<string>(folderList);
            DiffHelper.GenerateRecursiveAndParentPathSets(folderList, out parentFolders, out recursiveParents, out maxRecursiveDepth);
            parentFolders.ShouldMatchInOrder(expectedParentFolders);
            recursiveParents.ShouldMatchInOrder(expectedRecursiveFolders);
            maxRecursiveDepth.ShouldEqual(expectedMaxFolderPathDepth);
        }

        private static string GetDataPath(string fileName)
        {
            string workingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(workingDirectory, "Data", fileName);
        }
    }
}
