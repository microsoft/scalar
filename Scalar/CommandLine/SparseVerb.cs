using CommandLine;
using Scalar.Common;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Scalar.CommandLine
{
    [Verb(
        SparseVerb.SparseVerbName,
        HelpText = @"Add to the list of folders that are included in git's sparse-checkout.
Folders need to be relative to the repo's root directory.")
    ]
    public class SparseVerb : ScalarVerb.ForExistingEnlistment
    {
        private const string SparseVerbName = "sparse";
        private const string FolderListSeparator = ";";

        [Option(
            's',
            "set",
            Required = false,
            Default = "",
            HelpText = "A semicolon-delimited list of repo root relative folders to include in the sparse-checkout. Wildcards are not supported.")]
        public string FoldersToSet { get; set; }

        [Option(
            "set-stdin",
            Required = false,
            Default = false,
            HelpText = "Specify this flag to load folder list from stdin. Folders must be line-delimited and wildcards are not supported.")]
        public bool SetFromStdIn { get; set; }

        protected override string VerbName => SparseVerbName;

        protected override void Execute(ScalarEnlistment enlistment)
        {
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, SparseVerbName))
            {
                try
                {
                    CacheServerInfo cacheServer = CacheServerResolver.GetCacheServerFromConfig(enlistment);

                    tracer.AddLogFileEventListener(
                        ScalarEnlistment.GetNewScalarLogFileName(enlistment.ScalarLogsRoot, ScalarConstants.LogFileTypes.Sparse),
                        EventLevel.Informational,
                        Keywords.Any);
                    tracer.WriteStartEvent(
                            enlistment.EnlistmentRoot,
                            enlistment.RepoUrl,
                            cacheServer.Url,
                            new EventMetadata
                            {
                                { "Unattended", this.Unattended },
                                { "IsElevated", ScalarPlatform.Instance.IsElevated() },
                                { "NamedPipeName", enlistment.NamedPipeName },
                                { "ProcessID", Process.GetCurrentProcess().Id },
                                { nameof(this.EnlistmentRootPathParameter), this.EnlistmentRootPathParameter },
                                { nameof(this.FoldersToSet), this.FoldersToSet },
                                { nameof(this.SetFromStdIn), this.SetFromStdIn },
                            });

                    if (!this.SetFromStdIn && string.IsNullOrWhiteSpace(this.FoldersToSet))
                    {
                        this.ReportErrorAndExit(tracer, "You must specify folders to set with --set or --set-stdin");
                    }

                    List<string> foldersToSet = null;

                    // Pre-fetch the blobs for the folders that will be added and
                    // rely on PrefetchVerb for parsing the the list of folders
                    ReturnCode result = this.Execute<PrefetchVerb>(
                            enlistment,
                            verb =>
                            {
                                verb.Commits = false;
                                verb.ResolvedCacheServer = cacheServer;
                                verb.SkipVersionCheck = false;
                                verb.Files = string.Empty;
                                verb.Folders = this.FoldersToSet;
                                verb.FoldersFromStdIn = this.SetFromStdIn;
                            },
                            verb =>
                            {
                                foldersToSet = verb.ParsedFoldersList;
                            });

                    if (result != ReturnCode.Success)
                    {
                        this.ReportErrorAndExit(tracer, "\r\nError during prefetch @ {0}", enlistment.EnlistmentRoot);
                    }

                    this.SparseCheckoutSet(tracer, enlistment, foldersToSet);
                }
                catch (Exception e)
                {
                    this.ReportErrorAndExit(tracer, "Failed to set folders to sparse-checkout {0}", e.Message);
                }
            }
        }

        private void SparseCheckoutSet(ITracer tracer, ScalarEnlistment enlistment, List<string> foldersToSet)
        {
            GitProcess git = new GitProcess(enlistment);
            GitProcess.Result sparseResult = git.SparseCheckoutSet(foldersToSet);

            if (sparseResult.ExitCodeIsFailure)
            {
                this.WriteMessage(tracer, $"Failed to set folders to sparse-checkout: {sparseResult.Errors}");
            }
        }

        private void WriteMessage(ITracer tracer, string message)
        {
            this.Output.WriteLine(message);
            tracer.RelatedEvent(
                EventLevel.Informational,
                SparseVerbName,
                new EventMetadata
                {
                    { TracingConstants.MessageKey.InfoMessage, message }
                });
        }
    }
}
