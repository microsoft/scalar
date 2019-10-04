using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.Maintenance;
using Scalar.Common.Tracing;
using System;

namespace Scalar.CommandLine
{
    [Verb(PrefetchVerb.PrefetchVerbName, HelpText = "Prefetch remote objects for the current head")]
    public class PrefetchVerb : ScalarVerb.ForExistingEnlistment
    {
        private const string PrefetchVerbName = "prefetch";

        [Option(
            "verbose",
            Required = false,
            Default = false,
            HelpText = "Show all outputs on the console in addition to writing them to a log file.")]
        public bool Verbose { get; set; }

        public bool SkipVersionCheck { get; set; }
        public CacheServerInfo ResolvedCacheServer { get; set; }
        public ServerScalarConfig ServerScalarConfig { get; set; }

        protected override string VerbName
        {
            get { return PrefetchVerbName; }
        }

        protected override void Execute(ScalarEnlistment enlistment)
        {
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, "Prefetch"))
            {
                if (this.Verbose)
                {
                    tracer.AddDiagnosticConsoleEventListener(EventLevel.Informational, Keywords.Any);
                }

                string cacheServerUrl = CacheServerResolver.GetUrlFromConfig(enlistment);

                tracer.AddLogFileEventListener(
                    ScalarEnlistment.GetNewScalarLogFileName(enlistment.ScalarLogsRoot, ScalarConstants.LogFileTypes.Prefetch),
                    EventLevel.Informational,
                    Keywords.Any);
                tracer.WriteStartEvent(
                    enlistment.EnlistmentRoot,
                    enlistment.RepoUrl,
                    cacheServerUrl);

                try
                {
                    EventMetadata metadata = new EventMetadata();
                    tracer.RelatedEvent(EventLevel.Informational, "PerformPrefetch", metadata);

                    GitObjectsHttpRequestor objectRequestor;
                    CacheServerInfo cacheServer;
                    this.InitializeServerConnection(
                        tracer,
                        enlistment,
                        cacheServerUrl,
                        out objectRequestor,
                        out cacheServer);
                    this.PrefetchCommits(tracer, enlistment, objectRequestor, cacheServer);
                }
                catch (VerbAbortedException)
                {
                    throw;
                }
                catch (AggregateException aggregateException)
                {
                    this.Output.WriteLine(
                        "Cannot prefetch {0}. " + ConsoleHelper.GetScalarLogMessage(enlistment.EnlistmentRoot),
                        enlistment.EnlistmentRoot);
                    foreach (Exception innerException in aggregateException.Flatten().InnerExceptions)
                    {
                        tracer.RelatedError(
                            new EventMetadata
                            {
                                { "Verb", typeof(PrefetchVerb).Name },
                                { "Exception", innerException.ToString() }
                            },
                            $"Unhandled {innerException.GetType().Name}: {innerException.Message}");
                    }

                    Environment.ExitCode = (int)ReturnCode.GenericError;
                }
                catch (Exception e)
                {
                    this.Output.WriteLine(
                        "Cannot prefetch {0}. " + ConsoleHelper.GetScalarLogMessage(enlistment.EnlistmentRoot),
                        enlistment.EnlistmentRoot);
                    tracer.RelatedError(
                        new EventMetadata
                        {
                            { "Verb", typeof(PrefetchVerb).Name },
                            { "Exception", e.ToString() }
                        },
                        $"Unhandled {e.GetType().Name}: {e.Message}");

                    Environment.ExitCode = (int)ReturnCode.GenericError;
                }
            }
        }

        private void InitializeServerConnection(
            ITracer tracer,
            ScalarEnlistment enlistment,
            string cacheServerUrl,
            out GitObjectsHttpRequestor objectRequestor,
            out CacheServerInfo cacheServer)
        {
            RetryConfig retryConfig = this.GetRetryConfig(tracer, enlistment, TimeSpan.FromMinutes(RetryConfig.FetchAndCloneTimeoutMinutes));

            cacheServer = this.ResolvedCacheServer;
            ServerScalarConfig serverScalarConfig = this.ServerScalarConfig;
            if (!this.SkipVersionCheck)
            {
                string authErrorMessage;
                if (!this.TryAuthenticate(tracer, enlistment, out authErrorMessage))
                {
                    this.ReportErrorAndExit(tracer, "Unable to prefetch because authentication failed: " + authErrorMessage);
                }

                if (serverScalarConfig == null)
                {
                    serverScalarConfig = this.QueryScalarConfig(tracer, enlistment, retryConfig);
                }

                if (cacheServer == null)
                {
                    CacheServerResolver cacheServerResolver = new CacheServerResolver(tracer, enlistment);
                    cacheServer = cacheServerResolver.ResolveNameFromRemote(cacheServerUrl, serverScalarConfig);
                }

                this.ValidateClientVersions(tracer, enlistment, serverScalarConfig, showWarnings: false);

                this.Output.WriteLine("Configured cache server: " + cacheServer);
            }

            this.InitializeLocalCacheAndObjectsPaths(tracer, enlistment, retryConfig, serverScalarConfig, cacheServer);
            objectRequestor = new GitObjectsHttpRequestor(tracer, enlistment, cacheServer, retryConfig);
        }

        private void PrefetchCommits(ITracer tracer, ScalarEnlistment enlistment, GitObjectsHttpRequestor objectRequestor, CacheServerInfo cacheServer)
        {
            bool success;
            string error = string.Empty;
            PhysicalFileSystem fileSystem = new PhysicalFileSystem();
            GitRepo repo = new GitRepo(tracer, enlistment, fileSystem);
            ScalarContext context = new ScalarContext(tracer, fileSystem, repo, enlistment);
            GitObjects gitObjects = new ScalarGitObjects(context, objectRequestor);

            if (this.Verbose)
            {
                success = new PrefetchStep(context, gitObjects, requireCacheLock: false).TryPrefetchCommitsAndTrees(out error);
            }
            else
            {
                success = this.ShowStatusWhileRunning(
                    () => new PrefetchStep(context, gitObjects, requireCacheLock: false).TryPrefetchCommitsAndTrees(out error),
                "Fetching commits and trees " + this.GetCacheServerDisplay(cacheServer, enlistment.RepoUrl));
            }

            if (!success)
            {
                this.ReportErrorAndExit(tracer, "Prefetching commits and trees failed: " + error);
            }
        }

        private string GetCacheServerDisplay(CacheServerInfo cacheServer, string repoUrl)
        {
            if (!cacheServer.IsNone(repoUrl))
            {
                return "from cache server";
            }

            return "from origin (no cache server)";
        }
    }
}
