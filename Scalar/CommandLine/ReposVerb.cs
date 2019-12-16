using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scalar.CommandLine
{
    [Verb(ReposVerb.ReposVerbName, HelpText = "Track repos registered with the Scalar service")]
    public class ReposVerb : ScalarVerb.ForExistingEnlistment
    {
        private const string ReposVerbName = "repos";

        protected override string VerbName => ReposVerb.ReposVerbName;

        [Value(
            0,
            Required = true,
            MetaName = "Subcommand",
            HelpText = "The subcommand to execute")]
        public string Subcommand { get; set; }

        protected override void Execute(ScalarEnlistment enlistment)
        {
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, ReposVerbName))
            {
                tracer.AddLogFileEventListener(
                    ScalarEnlistment.GetNewScalarLogFileName(enlistment.ScalarLogsRoot, ScalarConstants.LogFileTypes.Watch),
                    EventLevel.Informational,
                    Keywords.Any);
                PhysicalFileSystem fileSystem = new PhysicalFileSystem();

                switch (this.Subcommand)
                {
                    case "add":
                        if (this.TryRegisterRepo(tracer, enlistment, fileSystem, out string error))
                        {
                            Console.Out.WriteLine($"Successfully registered repo at '{enlistment.EnlistmentRoot}'");
                        }
                        else
                        {
                            string message = $"Failed to register repo: {error}";
                            tracer.RelatedError(message);
                            this.ReportErrorAndExit(message);
                        }
                        break;

                    case "list":
                        foreach (string repoRoot in this.GetRepoList())
                        {
                            this.Output.WriteLine(repoRoot);
                        }
                        break;

                    default:
                        this.ReportErrorAndExit($"Unknown subcommand '{this.Subcommand}'");
                        break;
                }
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
