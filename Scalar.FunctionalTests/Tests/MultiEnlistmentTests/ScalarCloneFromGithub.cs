using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System;
using System.IO;
using System.Linq;

namespace Scalar.FunctionalTests.Tests.MultiEnlistmentTests
{
    [Category(Categories.GitRepository)]
    public class ScalarCloneFromGithub : TestsWithMultiEnlistment
    {
        private static readonly string MicrosoftScalarHttp = "https://github.com/microsoft/scalar";
        private static readonly string MicrosoftScalarSsh = "git@github.com:microsoft/scalar.git";

        private FileSystemRunner fileSystem;

        public ScalarCloneFromGithub()
        {
            this.fileSystem = new SystemIORunner();
        }

        [TestCase]
        public void PartialCloneHttps()
        {
            ScalarFunctionalTestEnlistment enlistment = this.CreateNewEnlistment(
                                                                url: MicrosoftScalarHttp,
                                                                branch: "master",
                                                                fullClone: false);

            VerifyPartialCloneBehavior(enlistment);
        }

        private void VerifyPartialCloneBehavior(ScalarFunctionalTestEnlistment enlistment)
        {
            this.fileSystem.DirectoryExists(enlistment.RepoRoot).ShouldBeTrue($"'{enlistment.RepoRoot}' does not exist");

            string gitPack = Path.Combine(enlistment.RepoRoot, ".git", "objects", "pack");
            this.fileSystem.DirectoryExists(gitPack).ShouldBeTrue($"'{gitPack}' does not exist");

            void checkPacks(string dir, int count, string when)
            {
                string dirContents = this.fileSystem
                    .EnumerateDirectory(dir);

                dirContents
                    .Split()
                    .Where(file => string.Equals(Path.GetExtension(file), ".pack", StringComparison.OrdinalIgnoreCase))
                    .Count()
                    .ShouldEqual(count, $"'{dir}' after '{when}': '{dirContents}'");
            }

            // Two packs for clone: commits and trees, blobs at root
            checkPacks(gitPack, 2, "clone");

            string srcScalar = Path.Combine(enlistment.RepoRoot, "Scalar");
            this.fileSystem.DirectoryExists(srcScalar).ShouldBeFalse($"'{srcScalar}' should not exist due to sparse-checkout");

            ProcessResult sparseCheckoutResult = GitProcess.InvokeProcess(enlistment.RepoRoot, "sparse-checkout disable");
            sparseCheckoutResult.ExitCode.ShouldEqual(0, "git sparse-checkout disable exit code");

            this.fileSystem.DirectoryExists(srcScalar).ShouldBeTrue($"'{srcScalar}' should exist after sparse-checkout");

            // Three packs for sparse-chekcout: commits and trees, blobs at root, blobs outside root
            checkPacks(gitPack, 3, "sparse-checkout");

            ProcessResult checkoutResult = GitProcess.InvokeProcess(enlistment.RepoRoot, "checkout HEAD~10");
            checkoutResult.ExitCode.ShouldEqual(0, "git checkout exit code");

            // Four packs for chekcout: commits and trees, blobs at root, blobs outside root, checkout diff
            checkPacks(gitPack, 4, "checkout");
        }
    }
}
