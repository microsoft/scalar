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
        private const int ScalarGenericError = 3;

        [TestCase]
        public void CloneInsideExistingEnlistment()
        {
            this.SubfolderCloneShouldFail();
        }

        [TestCase]
        public void CloneWithLocalCachePathWithinSrc()
        {
            string newEnlistmentRoot = ScalarFunctionalTestEnlistment.GetUniqueEnlistmentRoot();

            ProcessStartInfo processInfo = new ProcessStartInfo(ScalarTestConfig.PathToScalar);
            processInfo.Arguments = $"clone {Properties.Settings.Default.RepoToClone} {newEnlistmentRoot} --local-cache-path {Path.Combine(newEnlistmentRoot, "src", ".scalarCache")}";
            processInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processInfo.CreateNoWindow = true;
            processInfo.WorkingDirectory = Path.GetDirectoryName(this.Enlistment.EnlistmentRoot);
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;

            ProcessResult result = ProcessHelper.Run(processInfo);
            result.ExitCode.ShouldEqual(ScalarGenericError);
            result.Output.ShouldContain("'--local-cache-path' cannot be inside the src folder");
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

            ProcessStartInfo processInfo = new ProcessStartInfo(ScalarTestConfig.PathToScalar);

            processInfo.Arguments = $"clone {Properties.Settings.Default.RepoToClone} {newEnlistmentRoot} --no-fetch-commits-and-trees";
            processInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.WorkingDirectory = Properties.Settings.Default.EnlistmentRoot;

            ProcessResult result = ProcessHelper.Run(processInfo);
            result.ExitCode.ShouldEqual(0, result.Errors);

            string gitObjectsRoot = ScalarHelpers.GetObjectsRootFromGitConfig(Path.Combine(newEnlistmentRoot, "src"));

            gitObjectsRoot.StartsWith(defaultLocalCacheRoot, StringComparison.Ordinal).ShouldBeTrue($"Git objects root did not default to using {defaultLocalCacheRoot}");

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
                directories.ShouldContain(x => Path.GetFileName(x).Equals("src", StringComparison.Ordinal));
            }
            finally
            {
                enlistment.DeleteAll();
            }
        }

        private void SubfolderCloneShouldFail()
        {
            ProcessStartInfo processInfo = new ProcessStartInfo(ScalarTestConfig.PathToScalar);
            processInfo.Arguments = "clone " + ScalarTestConfig.RepoToClone + " src\\scalar\\test1";
            processInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processInfo.CreateNoWindow = true;
            processInfo.WorkingDirectory = this.Enlistment.EnlistmentRoot;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;

            ProcessResult result = ProcessHelper.Run(processInfo);
            result.ExitCode.ShouldEqual(ScalarGenericError);
            result.Output.ShouldContain("You can't clone inside an existing Scalar repo");
        }
    }
}
