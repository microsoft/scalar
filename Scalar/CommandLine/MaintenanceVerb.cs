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
        private const string MaintenanceVerbName = "maintenance";

        [Option(
            't',
            "task",
            Required = true,
            Default = "",
            HelpText = "Maintenance task to run.  Allowed values are '"
                + ScalarConstants.VerbParameters.Maintenance.LooseObjectsTaskName + "', '"
                + ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName + "', '"
                + ScalarConstants.VerbParameters.Maintenance.CommitGraphTaskName + "'")]
        public string MaintenanceTask { get; set; }

        [Option(
            ScalarConstants.VerbParameters.Maintenance.BatchSizeOptionName,
            Required = false,
            Default = "",
            HelpText = "Batch size.  This option can only be used with the '" + ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName + "' task")]
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

                string logFileName = ScalarEnlistment.GetNewScalarLogFileName(
                                                        enlistment.ScalarLogsRoot,
                                                        ScalarConstants.LogFileTypes.Maintenance,
                                                        logId: this.StartedByService ? "service" : null);

                tracer.AddLogFileEventListener(
                    logFileName,
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
                            { nameof(this.StartedByService), this.StartedByService },
                        }));

                this.InitializeCachePaths(tracer, enlistment);
                PhysicalFileSystem fileSystem = new PhysicalFileSystem();
                using (ScalarContext context = new ScalarContext(tracer, fileSystem, enlistment))
                {
                    try
                    {
                        switch (this.MaintenanceTask)
                        {
                            case ScalarConstants.VerbParameters.Maintenance.LooseObjectsTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                (new LooseObjectsStep(context, forceRun: true)).Execute();
                                return;

                            case ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName:
                                (new PackfileMaintenanceStep(
                                    context,
                                    forceRun: true,
                                    batchSize: string.IsNullOrWhiteSpace(this.PackfileMaintenanceBatchSize) ?
                                        PackfileMaintenanceStep.DefaultBatchSize :
                                        this.PackfileMaintenanceBatchSize)).Execute();
                                return;

                            case ScalarConstants.VerbParameters.Maintenance.FetchTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                this.FetchCommitsAndTrees(tracer, enlistment, cacheServerUrl);
                                return;

                            case ScalarConstants.VerbParameters.Maintenance.CommitGraphTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                (new CommitGraphStep(context, requireObjectCacheLock: false)).Execute();
                                return;

                            case ScalarConstants.VerbParameters.Maintenance.ConfigTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                (new ConfigStep(context)).Execute();
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
            GitObjectsHttpRequestor objectRequestor = null;
            CacheServerInfo cacheServer = null;

            if (enlistment.UsesGvfsProtocol)
            {
                this.InitializeServerConnection(
                    tracer,
                    enlistment,
                    cacheServerUrl,
                    out objectRequestor,
                    out cacheServer);
            }

            this.RunFetchStep(tracer, enlistment, objectRequestor, cacheServer);
        }

        private void FailIfBatchSizeSet(ITracer tracer)
        {
            if (!string.IsNullOrWhiteSpace(this.PackfileMaintenanceBatchSize))
            {
                this.ReportErrorAndExit(
                    tracer,
                    ReturnCode.UnsupportedOption,
                    $"--{ScalarConstants.VerbParameters.Maintenance.BatchSizeOptionName} can only be used with the '{ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName}' task");
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

            this.InitializeCachePaths(tracer, enlistment);
            objectRequestor = new GitObjectsHttpRequestor(tracer, enlistment, cacheServer, retryConfig);
        }

        private void RunFetchStep(ITracer tracer, ScalarEnlistment enlistment, GitObjectsHttpRequestor objectRequestor, CacheServerInfo cacheServer)
        {
            bool success;
            string error = string.Empty;
            PhysicalFileSystem fileSystem = new PhysicalFileSystem();
            ScalarContext context = new ScalarContext(tracer, fileSystem, enlistment);
            GitObjects gitObjects = new GitObjects(tracer, enlistment, objectRequestor, fileSystem);

            success = this.ShowStatusWhileRunning(
                () => new FetchStep(context, gitObjects, requireCacheLock: false).TryFetch(out error),
                                    "Fetching " + this.GetCacheServerDisplay(cacheServer, enlistment.RepoUrl));

            if (!success)
            {
                this.ReportErrorAndExit(tracer, ReturnCode.GenericError, "Fetch failed: " + error);
            }
        }

        private string GetCacheServerDisplay(CacheServerInfo cacheServer, string repoUrl)
        {
            if (cacheServer == null)
            {
                return "from origin";
            }

            if (!cacheServer.IsNone(repoUrl))
            {
                return "from cache server";
            }

            return "from origin (no cache server)";
        }
    }
}
