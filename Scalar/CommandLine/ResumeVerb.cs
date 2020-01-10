using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using System;

namespace Scalar.CommandLine
{
    [Verb(ResumeVerb.ResumeVerbName, HelpText = "Resume repos registered with the Scalar service")]
    public class ResumeVerb : ScalarVerb.ForNoEnlistment
    {
        private const string ResumeVerbName = "resume";

        protected override string VerbName => ResumeVerb.ResumeVerbName;

        public override void Execute()
        {
            string repoRegistryLocation = ScalarPlatform.Instance.GetCommonAppDataRootForScalarComponent(ScalarConstants.RepoRegistry.RegistryDirectoryName);
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, ResumeVerb.ResumeVerbName))
            {
                ScalarRepoRegistry repoRegistry = new ScalarRepoRegistry(
                    tracer,
                    new PhysicalFileSystem(),
                    repoRegistryLocation);

                if (!repoRegistry.TryRemovePauseFile(out string error))
                {
                    this.ReportErrorAndExit(error);
                }
            }
        }
    }
}
