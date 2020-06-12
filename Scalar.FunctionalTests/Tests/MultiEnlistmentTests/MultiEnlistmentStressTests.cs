using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Scalar.FunctionalTests.Tests.MultiEnlistmentTests
{
    [Category(Categories.Stress)]
    public class MultiEnlistmentStressTests : TestsWithMultiEnlistment
    {
        private static readonly string MicrosoftScalarHttp = "https://github.com/microsoft/scalar";

        private const int enlistmentCount = 10;
        private const int worktreeCount = 10;
        private const int parallelCount = 3;
        private const int loopCount = 25;
        private const int filesToCreate = 25;
        private const int timeout = 30;

        private FileSystemRunner fileSystem;
        private int numSuccesses;

        public MultiEnlistmentStressTests()
        {
            this.fileSystem = new SystemIORunner();
        }

        [TestCase]
        public void SingleEnlistmentStatus()
        {
            Environment.SetEnvironmentVariable("GIT_TRACE2_EVENT", "C:\\_git\\scalar\\stress.txt");
            ScalarFunctionalTestEnlistment enlistment = this.CreateNewEnlistment(
                                                                url: MicrosoftScalarHttp,
                                                                branch: "main",
                                                                fullClone: false);

            List<ScalarFunctionalTestEnlistment> enlistments = new List<ScalarFunctionalTestEnlistment>();

            TaskFactory factory = new TaskFactory();
            List<Task> tasks = new List<Task>();

            tasks.Add(factory.StartNew(() => VerifyWorktreeBehaviorLoop(enlistment, enlistment.RepoRoot)));

            for (int i = 0; i < parallelCount; i++)
            {
                tasks.Add(factory.StartNew(() => VerifyStatusBehaviorLoop(enlistment.RepoRoot)));
            }

            for (int i = 0; i < tasks.Count; i++)
            {
                tasks[i].Wait();
            }

            this.numSuccesses.ShouldEqual(tasks.Count, "Not all threads succeeded");
        }

        [TestCase]
        public void MultiEnlistmentStatus()
        {
            Environment.SetEnvironmentVariable("GIT_TRACE2_EVENT", "C:\\_git\\scalar\\stress.txt");

            List<ScalarFunctionalTestEnlistment> enlistments = new List<ScalarFunctionalTestEnlistment>();

            for (int i = 0; i < enlistmentCount; i++)
            {
                ScalarFunctionalTestEnlistment enlistment = this.CreateNewEnlistment(
                                                                    url: MicrosoftScalarHttp,
                                                                    branch: "main",
                                                                    fullClone: false);

                enlistments.Add(enlistment);
            }

            TaskFactory factory = new TaskFactory();
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < enlistments.Count; i++)
            {
                ScalarFunctionalTestEnlistment enlistment = enlistments[i];
                tasks.Add(factory.StartNew(() => VerifyWorktreeBehaviorLoop(enlistment, enlistment.RepoRoot)));
            }

            for (int i = 0; i < tasks.Count; i++)
            {
                tasks[i].Wait();
            }

            this.numSuccesses.ShouldEqual(enlistmentCount, "Not all threads succeeded");
        }

        [TestCase]
        public void MultiWorktreeStatus()
        {
            Environment.SetEnvironmentVariable("GIT_TRACE2_EVENT", "C:\\_git\\scalar\\stress.txt");
            ScalarFunctionalTestEnlistment enlistment = this.CreateNewEnlistment(
                                                                url: MicrosoftScalarHttp,
                                                                branch: "main",
                                                                fullClone: false);

            List<string> worktrees = new List<string>();
            for (int i = 0; i < worktreeCount; i++)
            {
                string workdir = Path.Combine(enlistment.EnlistmentRoot, $"src-{i}");
                this.VerifySuccessfulGitCommand(enlistment.RepoRoot, $"-c core.fsmonitor= worktree add {workdir} origin/main");
                worktrees.Add(workdir);
            }

            TaskFactory factory = new TaskFactory();
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < worktrees.Count; i++)
            {
                string worktree = worktrees[i];
                tasks.Add(factory.StartNew(() => VerifyWorktreeBehaviorLoop(enlistment, worktree)));
            }

            for (int i = 0; i < tasks.Count; i++)
            {
                tasks[i].Wait();
            }

            this.numSuccesses.ShouldEqual(worktreeCount, "Not all threads succeeded");
        }

        private void VerifyWorktreeBehaviorLoop(ScalarFunctionalTestEnlistment enlistment, string worktree)
        {
            for (int i = 0; i < loopCount; i++)
            {
                this.VerifyWorktreeBehavior(enlistment, worktree, i);
            }

            Interlocked.Increment(ref this.numSuccesses);
        }

        private void VerifyStatusBehaviorLoop(string worktree)
        {
            for (int i = 0; i < loopCount; i++)
            {
                this.VerifySuccessfulGitCommand(worktree, "status");
            }

            Interlocked.Increment(ref this.numSuccesses);
        }

        private void VerifyWorktreeBehavior(ScalarFunctionalTestEnlistment enlistment, string worktree, int iteration)
        {
            string dirName = $"dir{iteration}";
            string iterationDir = Path.Combine(worktree, dirName);

            this.fileSystem.CreateDirectory(iterationDir);

            for (int i = 0; i < filesToCreate; i++)
            {
                string nameI = $"file{i}.txt";
                string localI = Path.Combine(dirName, nameI);
                string fileI = Path.Combine(iterationDir, nameI);
                this.fileSystem.WriteAllText(fileI, $"{iteration} {worktree} {i}\n");

                for (int j = 0; j < i; j++)
                {
                    string fileJ = Path.Combine(iterationDir, $"file{j}.txt");
                    this.fileSystem.AppendAllText(fileJ, $"{iteration} {worktree} {i} {j}\n");
                }

                this.VerifySuccessfulGitCommand(worktree, $"add {localI}");
                this.VerifySuccessfulGitCommand(worktree, "status");
                this.VerifySuccessfulGitCommand(worktree, $"commit -m staged{i}");
                this.VerifySuccessfulGitCommand(worktree, "status");
                this.VerifySuccessfulGitCommand(worktree, $"commit --allow-empty -a -m all{i}");
                this.VerifySuccessfulGitCommand(worktree, "status");

                // Disable fsmonitor and add all files, which should not do anything.
                // If the "git commit" command succeeds, then we had some extra modified files
                // that were not included in this run
                this.VerifySuccessfulGitCommand(worktree, $"-c core.fsmonitor=\"\" add .");
                ProcessResult result = GitProcess.InvokeProcess(worktree, "commit -m empty-should-fail", timeoutSeconds: timeout);
                result.ExitCode.ShouldNotEqual(0, $"'git commit -m empty-should-fail' succeeded?");
            }
        }

        private void VerifySuccessfulGitCommand(string dir, string args)
        {
            ProcessResult result = GitProcess.InvokeProcess(dir, args, timeoutSeconds: timeout);
            result.ExitCode.ShouldEqual(0, $"'git {args}' in '{dir}' failed.\n\nOutput: {result.Output}\n\nErrors: {result.Errors}");
        }
    }
}
