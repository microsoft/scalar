using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using System;

namespace Scalar.CommandLine
{
    [Verb(WatchVerb.WatchVerbName, HelpText = "Register a repo to be watched by the Scalar service")]
    public class WatchVerb : ScalarVerb.ForExistingEnlistment
    {
        private const string WatchVerbName = "watch";

        protected override string VerbName => WatchVerb.WatchVerbName;

        protected override void Execute(ScalarEnlistment enlistment)
        {
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, WatchVerbName))
            {
                tracer.AddLogFileEventListener(
                    ScalarEnlistment.GetNewScalarLogFileName(enlistment.ScalarLogsRoot, ScalarConstants.LogFileTypes.Watch),
                    EventLevel.Informational,
                    Keywords.Any);
                PhysicalFileSystem fileSystem = new PhysicalFileSystem();

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
            }
        }
    }
}
