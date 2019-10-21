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
    [Verb(MaintenanceVerb.MaintenanceVerbName, HelpText = "Perform a maintenance task in a Scalar repo")]
    public class MaintenanceVerb : ScalarVerb.ForExistingEnlistment
    {
        public const string FetchCommitsAndTreesTaskName = "fetch-commits-and-trees";

        private const string MaintenanceVerbName = "maintenance";

        private const string LooseObjectsTaskName = "loose-objects";
        private const string PackFilesTaskName = "pack-files";
        private const string CommitGraphTaskName = "commit-graph";

        private const string BatchSizeOptionName = "batch-size";

        [Option(
            't',
            "task",
            Required = true,
            Default = "",
            HelpText = "Maintenance task to run.  Allowed values are '"
                + LooseObjectsTaskName + "', '"
                + PackFilesTaskName + "', '"
                + CommitGraphTaskName + "'")]
        public string MaintenanceTask { get; set; }

        [Option(
            BatchSizeOptionName,
            Required = false,
            Default = "",
            HelpText = "Batch size.  This option can only be used with the '" + PackFilesTaskName + "' task")]
        public string PackfileMaintenanceBatchSize { get; set; }

        public bool SkipVersionCheck { get; set; }
        public CacheServerInfo ResolvedCacheServer { get; set; }
        public ServerScalarConfig ServerScalarConfig { get; set; }

        protected override string VerbName
        {
            get { return MaintenanceVerb.MaintenanceVerbName; }
        }

        protected override void Execute(ScalarEnlistment enlistment)
        {
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, MaintenanceVerbName))
            {
                string cacheServerUrl = CacheServerResolver.GetUrlFromConfig(enlistment);

                tracer.AddLogFileEventListener(
                    ScalarEnlistment.GetNewScalarLogFileName(enlistment.ScalarLogsRoot, ScalarConstants.LogFileTypes.Maintenance),
                    EventLevel.Informational,
                    Keywords.Any);
                tracer.WriteStartEvent(
                    enlistment.EnlistmentRoot,
                    enlistment.RepoUrl,
                    cacheServerUrl,
                    this.AddVerbDataToMetadata(
                        new EventMetadata
                        {
                            { nameof(this.MaintenanceTask), this.MaintenanceTask },
                            { nameof(this.PackfileMaintenanceBatchSize), this.PackfileMaintenanceBatchSize },
                            { nameof(this.EnlistmentRootPathParameter), this.EnlistmentRootPathParameter },
                        }));

                this.InitializeLocalCacheAndObjectsPaths(tracer, enlistment, retryConfig: null, serverScalarConfig: null, cacheServer: null);
                PhysicalFileSystem fileSystem = new PhysicalFileSystem();
                using (ScalarContext context = new ScalarContext(tracer, fileSystem, enlistment))
                {
                    try
                    {
                        switch (this.MaintenanceTask)
                        {
                            case LooseObjectsTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                (new LooseObjectsStep(context, forceRun: true)).Execute();
                                return;

                            case PackFilesTaskName:
                                (new PackFileMaintenanceStep(
                                    context,
                                    forceRun: true,
                                    batchSize: string.IsNullOrWhiteSpace(this.PackfileMaintenanceBatchSize) ?
                                        PackFileMaintenanceStep.DefaultBatchSize :
                                        this.PackfileMaintenanceBatchSize)).Execute();
                                return;

                            case FetchCommitsAndTreesTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                this.FetchCommitsAndTrees(tracer, enlistment, cacheServerUrl);
                                return;

                            case CommitGraphTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                (new CommitGraphStep(context, requireObjectCacheLock: false)).Execute();
                                return;

                            default:
                                this.ReportErrorAndExit($"Unknown maintenance task requested: '{this.MaintenanceTask}'");
                                break;
                        }
                    }
                    catch (VerbAbortedException)
                    {
                        throw;
                    }
                    catch (AggregateException aggregateException)
                    {
                        string error = $"AggregateException thrown while running '{this.MaintenanceTask}' task: {aggregateException.Message}";
                        tracer.RelatedError(this.CreateEventMetadata(aggregateException), error);
                        foreach (Exception innerException in aggregateException.Flatten().InnerExceptions)
                        {
                            tracer.RelatedError(
                                this.CreateEventMetadata(innerException),
                                $"Unhandled {innerException.GetType().Name}: {innerException.Message}");
                        }

                        this.ReportErrorAndExit(tracer, ReturnCode.GenericError, error);
                    }
                    catch (Exception e)
                    {
                        string error = $"Exception thrown while running '{this.MaintenanceTask}' task: {e.Message}";
                        tracer.RelatedError(this.CreateEventMetadata(e), error);
                        this.ReportErrorAndExit(tracer, ReturnCode.GenericError, error);
                    }
                }
            }
        }

        private void FetchCommitsAndTrees(ITracer tracer, ScalarEnlistment enlistment, string cacheServerUrl)
        {
            GitObjectsHttpRequestor objectRequestor;
            CacheServerInfo cacheServer;
            this.InitializeServerConnection(
                tracer,
                enlistment,
                cacheServerUrl,
                out objectRequestor,
                out cacheServer);
            this.RunFetchCommitsAndTreesStep(tracer, enlistment, objectRequestor, cacheServer);
        }

        private void FailIfBatchSizeSet(ITracer tracer)
        {
            if (!string.IsNullOrWhiteSpace(this.PackfileMaintenanceBatchSize))
            {
                this.ReportErrorAndExit(
                    tracer,
                    ReturnCode.UnsupportedOption,
                    $"--{BatchSizeOptionName} can only be used with the '{PackFilesTaskName}' task");
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
                    this.ReportErrorAndExit(tracer, "Unable to fetch because authentication failed: " + authErrorMessage);
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

        private void RunFetchCommitsAndTreesStep(ITracer tracer, ScalarEnlistment enlistment, GitObjectsHttpRequestor objectRequestor, CacheServerInfo cacheServer)
        {
            bool success;
            string error = string.Empty;
            PhysicalFileSystem fileSystem = new PhysicalFileSystem();
            ScalarContext context = new ScalarContext(tracer, fileSystem, enlistment);
            GitObjects gitObjects = new ScalarGitObjects(context, objectRequestor);

            success = this.ShowStatusWhileRunning(
                () => new FetchCommitsAndTreesStep(context, gitObjects, requireCacheLock: false).TryFetchCommitsAndTrees(out error),
            "Fetching commits and trees " + this.GetCacheServerDisplay(cacheServer, enlistment.RepoUrl));

            if (!success)
            {
                this.ReportErrorAndExit(tracer, ReturnCode.GenericError, "Fetching commits and trees failed: " + error);
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
