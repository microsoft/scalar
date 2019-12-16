using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Scalar.CommandLine
{
    [Verb(ReposVerb.ReposVerbName, HelpText = "Track repos registered with the Scalar service")]
    public class ReposVerb : ScalarVerb
    {
        private const string ReposVerbName = "repos";

        private const string AddSubcommand = "add";
        private const string ListSubcommand = "list";

        protected override string VerbName => ReposVerb.ReposVerbName;

        [Value(
            0,
            Required = true,
            MetaName = "Subcommand",
            HelpText = "The subcommand to execute")]
        public string Subcommand { get; set; }

        [Value(
            1,
            Required = false,
            Default = null,
            MetaName = "Enlistment Root Path",
            HelpText = "Full or relative path to the Scalar enlistment root")]
        public override string EnlistmentRootPathParameter { get; set; }

        public override void Execute()
        {
            PhysicalFileSystem fileSystem = new PhysicalFileSystem();

            switch (this.Subcommand)
            {
                case ReposVerb.AddSubcommand:
                    this.ValidatePathParameter(this.EnlistmentRootPathParameter);

                    ScalarEnlistment enlistment = this.CreateEnlistment(this.EnlistmentRootPathParameter ?? Directory.GetCurrentDirectory(), null);

                    using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, ReposVerb.ReposVerbName))
                    {
                        if (this.TryRegisterRepo(tracer, enlistment, fileSystem, out string error))
                        {
                            this.Output.WriteLine($"Successfully registered repo at '{enlistment.EnlistmentRoot}'");
                        }
                        else
                        {
                            string message = $"Failed to register repo: {error}";
                            tracer.RelatedError(message);
                            this.ReportErrorAndExit(message);
                        }
                    }
                    break;

                case ReposVerb.ListSubcommand:
                    foreach (string repoRoot in this.GetRepoList())
                    {
                        this.Output.WriteLine(repoRoot);
                    }
                    break;

                default:
                    StringBuilder messageBuilder = new StringBuilder();
                    messageBuilder.AppendLine($"Unknown subcommand '{this.Subcommand}'");
                    messageBuilder.AppendLine("Options are:");
                    messageBuilder.AppendLine($"\t{ReposVerb.AddSubcommand}");
                    messageBuilder.AppendLine($"\t{ReposVerb.ListSubcommand}");

                    this.ReportErrorAndExit(messageBuilder.ToString());
                    break;
            }
        }

        private IEnumerable<string> GetRepoList()
        {
            string repoRegistryLocation = ScalarPlatform.Instance.GetCommonAppDataRootForScalarComponent(ScalarConstants.RepoRegistry.RegistryDirectoryName);
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, ReposVerb.ReposVerbName))
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
