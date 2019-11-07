using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using System.Collections.Generic;
using System.Linq;

namespace Scalar.CommandLine
{
    [Verb(ServiceVerbName, HelpText = "Runs commands for the Scalar service.")]
    public class ServiceVerb : ScalarVerb.ForNoEnlistment
    {
        private const string ServiceVerbName = "service";

        [Option(
            "list-registered",
            Default = false,
            Required = false,
            HelpText = "Prints a list of all repos registered with the service")]
        public bool List { get; set; }

        protected override string VerbName
        {
            get { return ServiceVerbName; }
        }

        public override void Execute()
        {
            int optionCount = new[] { this.List }.Count(flag => flag);
            if (optionCount == 0)
            {
                this.ReportErrorAndExit($"Error: You must specify an argument.  Run 'scalar {ServiceVerbName} --help' for details.");
            }
            else if (optionCount > 1)
            {
                this.ReportErrorAndExit($"Error: You cannot specify multiple arguments.  Run 'scalar {ServiceVerbName} --help' for details.");
            }

            foreach (string repoRoot in this.GetRepoList())
            {
                this.Output.WriteLine(repoRoot);
            }
        }

        private IEnumerable<string> GetRepoList()
        {
            string repoRegistryLocation = ScalarPlatform.Instance.GetDataRootForScalarComponent(ScalarConstants.RepoRegistry.RegistryDirectoryName);
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, "ServiceVerb"))
            {
                ScalarRepoRegistry repoRegistry = new ScalarRepoRegistry(
                    tracer,
                    new PhysicalFileSystem(),
                    repoRegistryLocation);

                return repoRegistry.GetRegisteredRepos().Select(x => x.NormalizedRepoRoot);
            }
        }
    }
}
