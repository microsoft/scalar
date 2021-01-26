using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using System.Collections.Generic;
using System.Linq;

namespace Scalar.CommandLine
{
    [Verb(ListVerb.ListVerbName, HelpText = "List repos registered with Scalar")]
    public class ListVerb : ScalarVerb.ForNoEnlistment
    {
        private const string ListVerbName = "list";

        protected override string VerbName => ListVerb.ListVerbName;

        public override void Execute()
        {
            foreach (string repoRoot in this.GetRepoList())
            {
                this.Output.WriteLine(repoRoot);
            }
        }

        private IEnumerable<string> GetRepoList()
        {
            string repoRegistryLocation = ScalarPlatform.Instance.GetCommonAppDataRootForScalarComponent(ScalarConstants.RepoRegistry.RegistryDirectoryName);
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, ListVerb.ListVerbName))
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
