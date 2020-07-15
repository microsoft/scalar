using Scalar.Tests.Should;

namespace Scalar.FunctionalTests.Tools
{
    public static class ScalarHelpers
    {
        public const string GitConfigObjectCache = "gvfs.sharedCache";

        public static string GetObjectsRootFromGitConfig(string repoRoot)
        {
            ProcessResult result = GitProcess.InvokeProcess(repoRoot, $"config --local {ScalarHelpers.GitConfigObjectCache}");
            result.ExitCode.ShouldEqual(0, $"Failed to read git object root from config, error: {result.ExitCode}");
            string.IsNullOrWhiteSpace(result.Output).ShouldBeFalse($"{ScalarHelpers.GitConfigObjectCache} should be set");
            return result.Output.TrimEnd('\n');
        }

        public static string GetInternalParameter()
        {
            return $"\"{{\\\"ServiceName\\\":\\\"{ScalarServiceProcess.TestServiceName}\\\"," +
                    "\\\"StartedByService\\\":false}\"";
        }
    }
}
