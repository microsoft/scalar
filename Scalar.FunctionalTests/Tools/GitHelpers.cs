using Scalar.Tests.Should;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Scalar.FunctionalTests.Tools
{
    public static class GitHelpers
    {
        private const string WindowsPathSeparator = "\\";
        private const string GitPathSeparator = "/";

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
            bool removeWaitingMessages = true,
            bool removeUpgradeMessages = true,
            string input = null)
        {
            ProcessResult result = GitProcess.InvokeProcess(scalarRepoRoot, command, input, environmentVariables);
            string errors = result.Errors;

            if (!string.IsNullOrEmpty(errors) && (removeWaitingMessages || removeUpgradeMessages))
            {
                IEnumerable<string> errorLines = errors.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                IEnumerable<string> filteredErrorLines = errorLines.Where(line =>
                {
                    if (string.IsNullOrWhiteSpace(line) ||
                        (removeUpgradeMessages && line.StartsWith("A new version of Scalar is available.")) ||
                        (removeWaitingMessages && line.StartsWith("Waiting for ")))
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                });

                errors = filteredErrorLines.Any() ? string.Join(Environment.NewLine, filteredErrorLines) : string.Empty;
            }

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
            command = string.Format(command, args);
            string controlRepoRoot = controlGitRepo.RootPath;
            string scalarRepoRoot = enlistment.RepoRoot;

            Dictionary<string, string> environmentVariables = new Dictionary<string, string>();
            environmentVariables["GIT_QUIET"] = "true";

            ProcessResult expectedResult = GitProcess.InvokeProcess(controlRepoRoot, command, environmentVariables);
            ProcessResult actualResult = GitHelpers.InvokeGitAgainstScalarRepo(scalarRepoRoot, command, environmentVariables);

            LinesShouldMatch(command + " Errors Lines", actualResult.Errors, expectedResult.Errors);
            LinesShouldMatch(command + " Output Lines", actualResult.Output, expectedResult.Output);

            if (command != "status")
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
                    .Where(s => !string.IsNullOrWhiteSpace(s));
        }

        private static bool LinesAreEqual(string actualLine, string expectedLine)
        {
            return actualLine.Equals(expectedLine);
        }
    }
}
