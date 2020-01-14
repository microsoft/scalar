using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using System;

namespace Scalar.CommandLine
{
    [Verb(PauseVerb.PauseVerbName, HelpText = "Pause repos registered with the Scalar service")]
    public class PauseVerb : ScalarVerb.ForNoEnlistment
    {
        private const string PauseVerbName = "pause";

        protected override string VerbName => PauseVerb.PauseVerbName;

        [Value(
            0,
            Default = 12,
            HelpText = "The number of hours to delay background maintenance")]
        public long HoursToPause { get; set; }

        public override void Execute()
        {
            DateTime pauseTime = DateTime.Now.AddHours(this.HoursToPause);

            string repoRegistryLocation = ScalarPlatform.Instance.GetCommonAppDataRootForScalarComponent(ScalarConstants.RepoRegistry.RegistryDirectoryName);
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, PauseVerb.PauseVerbName))
            {
                ScalarRepoRegistry repoRegistry = new ScalarRepoRegistry(
                    tracer,
                    new PhysicalFileSystem(),
                    repoRegistryLocation);

                if (!repoRegistry.TryPauseMaintenanceUntil(pauseTime, out string error))
                {
                    this.ReportErrorAndExit($"Failed to set pause time: {error}");
                }
            }
        }
    }
}
