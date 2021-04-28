using Scalar.Tests.Should;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Scalar.FunctionalTests.Tools
{
    public static class GitHelpers
    {
        private const string WindowsPathSeparator = "\\";
        private const string GitPathSeparator = "/";

        // A command sequence number added to the Trace2 stream to help tie the control
        // and the corresponding enlistment commands together.
        private static int TraceCommandSequenceId = 0;

        public static string ConvertPathToGitFormat(string relativePath)
        {
            return relativePath.Replace(WindowsPathSeparator, GitPathSeparator);
        }

        public static void CheckGitCommand(string virtualRepoRoot, string command, params string[] expectedLinesInResult)
        {
            ProcessResult result = GitProcess.InvokeProcess(virtualRepoRoot, command);
            result.Errors.ShouldBeEmpty();
            foreach (string line in expectedLinesInResult)
            {
                result.Output.ShouldContain(line);
            }
        }

        public static void CheckGitCommandAgainstScalarRepo(string virtualRepoRoot, string command, params string[] expectedLinesInResult)
        {
            ProcessResult result = InvokeGitAgainstScalarRepo(virtualRepoRoot, command);
            result.Errors.ShouldBeEmpty();
            foreach (string line in expectedLinesInResult)
            {
                result.Output.ShouldContain(line);
            }
        }

        public static ProcessResult InvokeGitAgainstScalarRepo(
            string scalarRepoRoot,
            string command,
            Dictionary<string, string> environmentVariables = null,
            string input = null)
        {
            ProcessResult result = GitProcess.InvokeProcess(scalarRepoRoot, command, input, environmentVariables);
            string errors = result.Errors;

            return new ProcessResult(
                result.Output,
                errors,
                result.ExitCode);
        }

        public static void ValidateGitCommand(
            ScalarFunctionalTestEnlistment enlistment,
            ControlGitRepo controlGitRepo,
            string command,
            params object[] args)
        {
            bool isStatus = command.StartsWith("status");
            // Avoid sparse-checkout percentage in "status" calls.
            if (isStatus) {
                command = command + " --porcelain=v2";
            }
            command = string.Format(command, args);
            string controlRepoRoot = controlGitRepo.RootPath;
            string scalarRepoRoot = enlistment.RepoRoot;
            int pair_id = Interlocked.Increment(ref TraceCommandSequenceId);

            Dictionary<string, string> environmentVariables = new Dictionary<string, string>();
            environmentVariables["GIT_QUIET"] = "true";
            environmentVariables["GIT_COMMITTER_DATE"] = "Thu Feb 16 10:07:35 2017 -0700";
            environmentVariables["XXX_SEQUENCE_ID"] = pair_id.ToString();

            ProcessResult expectedResult = GitProcess.InvokeProcess(controlRepoRoot, command, environmentVariables);
            ProcessResult actualResult = GitHelpers.InvokeGitAgainstScalarRepo(scalarRepoRoot, command, environmentVariables);

            LinesShouldMatch(command + " Errors Lines", expectedResult.Errors, actualResult.Errors);
            LinesShouldMatch(command + " Output Lines", expectedResult.Output, actualResult.Output);

            if (!isStatus)
            {
                ValidateGitCommand(enlistment, controlGitRepo, "status");
            }
        }

        public static void LinesShouldMatch(string message, string expected, string actual)
        {
            IEnumerable<string> actualLines = NonEmptyLines(actual);
            IEnumerable<string> expectedLines = NonEmptyLines(expected);
            actualLines.ShouldMatchInOrder(expectedLines, LinesAreEqual, message);
        }

        private static IEnumerable<string> NonEmptyLines(string data)
        {
            return data
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Where(s => !s.Contains("gvfs-helper error: '(curl"));
        }

        private static bool LinesAreEqual(string actualLine, string expectedLine)
        {
            return actualLine.Equals(expectedLine);
        }
    }
}
