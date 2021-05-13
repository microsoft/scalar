using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Should;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System;
using System.Diagnostics;
using System.IO;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    public class CloneTests : TestsWithEnlistmentPerFixture
    {
        private const int ScalarGenericError = 128;

        [TestCase]
        public void CloneWithLocalCachePathWithinSrc()
        {
            string newEnlistmentRoot = ScalarFunctionalTestEnlistment.GetUniqueEnlistmentRoot();
            string localCachePath = Path.Combine(newEnlistmentRoot, "src", ".scalarCache");
            ProcessResult result = this.RunCloneCommand(
                Path.GetDirectoryName(this.Enlistment.EnlistmentRoot),
                newEnlistmentRoot,
                $"--local-cache-path {localCachePath}");
            result.ExitCode.ShouldEqual(ScalarGenericError);

            localCachePath = Path.Combine(newEnlistmentRoot, "SRC", ".scalarCache");

            result = this.RunCloneCommand(
                Path.GetDirectoryName(this.Enlistment.EnlistmentRoot),
                newEnlistmentRoot,
                $"--local-cache-path {localCachePath}");
            if (FileSystemHelpers.CaseSensitiveFileSystem)
            {
                result.ExitCode.ShouldEqual(0, result.Errors);
            }
            else
            {
                result.ExitCode.ShouldEqual(ScalarGenericError);
            }

            RepositoryHelpers.DeleteTestDirectory(newEnlistmentRoot);
        }

        [TestCase]
        public void SparseCloneWithNoFetchOfCommitsAndTreesSucceeds()
        {
            ScalarFunctionalTestEnlistment enlistment = null;

            try
            {
                enlistment = ScalarFunctionalTestEnlistment.CloneWithPerRepoCache(ScalarTestConfig.PathToScalar, skipFetchCommitsAndTrees: true);

                ProcessResult result = GitProcess.InvokeProcess(enlistment.RepoRoot, "status");
                result.ExitCode.ShouldEqual(0, result.Errors);
            }
            finally
            {
                enlistment?.DeleteAll();
            }
        }

        [TestCase]
        [Category(Categories.POSIXOnly)]
        public void CloneWithDefaultLocalCacheLocation()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;
            string defaultLocalCacheRoot = ScalarTestConfig.DefaultLocalCacheRoot;
            fileSystem.CreateDirectory(defaultLocalCacheRoot);
            defaultLocalCacheRoot.ShouldBeADirectory(fileSystem);

            string newEnlistmentRoot = ScalarFunctionalTestEnlistment.GetUniqueEnlistmentRoot();

            ProcessResult result = this.RunCloneCommand(
                Properties.Settings.Default.EnlistmentRoot,
                newEnlistmentRoot,
                "--no-fetch-commits-and-trees");
            result.ExitCode.ShouldEqual(0, result.Errors);

            string gitObjectsRoot = ScalarHelpers.GetObjectsRootFromGitConfig(Path.Combine(newEnlistmentRoot, "src"));

            gitObjectsRoot.StartsWith(defaultLocalCacheRoot, FileSystemHelpers.PathComparison).ShouldBeTrue($"Git objects root did not default to using {defaultLocalCacheRoot}");

            RepositoryHelpers.DeleteTestDirectory(newEnlistmentRoot);
        }

        [TestCase]
        public void CloneToPathWithSpaces()
        {
            ScalarFunctionalTestEnlistment enlistment = ScalarFunctionalTestEnlistment.CloneEnlistmentWithSpacesInPath(ScalarTestConfig.PathToScalar);
            enlistment.DeleteAll();
        }

        [TestCase]
        public void CloneCreatesCorrectFilesInRoot()
        {
            ScalarFunctionalTestEnlistment enlistment = ScalarFunctionalTestEnlistment.Clone(ScalarTestConfig.PathToScalar);
            try
            {
                Directory.GetFiles(enlistment.EnlistmentRoot).ShouldBeEmpty("There should be no files in the enlistment root after cloning");
                string[] directories = Directory.GetDirectories(enlistment.EnlistmentRoot);
                directories.Length.ShouldEqual(1);
                directories.ShouldContain(x => Path.GetFileName(x).Equals("src", FileSystemHelpers.PathComparison));
            }
            finally
            {
                enlistment.DeleteAll();
            }
        }

        private ProcessResult RunCloneCommand(string workingDirectoryPath, string enlistmentRootPath, string extraArgs = null)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo(ScalarTestConfig.PathToScalar);
            processInfo.Arguments = $"clone {ScalarTestConfig.RepoToClone} {enlistmentRootPath} {extraArgs}";
            processInfo.WorkingDirectory = workingDirectoryPath;
            processInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;

            return ProcessHelper.Run(processInfo);
        }
    }
}
