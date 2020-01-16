using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.Maintenance;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;

namespace Scalar.CommandLine
{
    [Verb(RunVerb.RunVerbName, HelpText = "Run a maintenance task in a Scalar repo")]
    public class RunVerb : ScalarVerb.ForExistingEnlistment
    {
        private const string RunVerbName = "run";

        [Value(
            0,
            Required = true,
            MetaName = "Task",
            Default = "",
            HelpText = "Maintenance task to run.  Allowed values are '"
                + ScalarConstants.VerbParameters.Maintenance.AllTasksName + "', '"
                + ScalarConstants.VerbParameters.Maintenance.ConfigTaskName + "', '"
                + ScalarConstants.VerbParameters.Maintenance.CommitGraphTaskName + "', '"
                + ScalarConstants.VerbParameters.Maintenance.FetchTaskName + "', '"
                + ScalarConstants.VerbParameters.Maintenance.LooseObjectsTaskName + "', '"
                + ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName + "'")]
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
            get { return RunVerb.RunVerbName; }
        }

        protected override void Execute(ScalarEnlistment enlistment)
        {
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, RunVerbName))
            {
                string cacheServerUrl = CacheServerResolver.GetUrlFromConfig(enlistment);

                string logFileName = ScalarEnlistment.GetNewScalarLogFileName(
                                                        enlistment.ScalarLogsRoot,
                                                        ScalarConstants.LogFileTypes.Maintenance,
                                                        logId: this.StartedByService ? "service" : null);

                List<ActionAndMessage> actions = new List<ActionAndMessage>();

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
                            case ScalarConstants.VerbParameters.Maintenance.AllTasksName:
                                actions.Add(this.GetConfigAction(context));
                                actions.Add(this.GetFetchAction(context, cacheServerUrl));
                                actions.Add(this.GetCommitGraphAction(context));
                                actions.Add(this.GetLooseObjectsAction(context));
                                actions.Add(this.GetPackfileAction(context));
                                break;

                            case ScalarConstants.VerbParameters.Maintenance.LooseObjectsTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                actions.Add(this.GetLooseObjectsAction(context));
                                break;

                            case ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName:
                                actions.Add(this.GetPackfileAction(context));
                                break;

                            case ScalarConstants.VerbParameters.Maintenance.FetchTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                actions.Add(this.GetFetchAction(context, cacheServerUrl));
                                break;

                            case ScalarConstants.VerbParameters.Maintenance.CommitGraphTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                actions.Add(this.GetCommitGraphAction(context));
                                break;

                            case ScalarConstants.VerbParameters.Maintenance.ConfigTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                actions.Add(this.GetConfigAction(context));
                                break;

                            default:
                                this.ReportErrorAndExit($"Unknown maintenance task requested: '{this.MaintenanceTask}'");
                                break;
                        }

                        foreach (ActionAndMessage item in actions)
                        {
                            this.ShowStatusWhileRunning(() => { item.Action.Invoke(); return true; }, item.Message);
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

        private ActionAndMessage GetConfigAction(ScalarContext context)
        {
            return new ActionAndMessage(
                        () => new ConfigStep(context).Execute(),
                        "Setting recommended config settings");
        }

        private ActionAndMessage GetFetchAction(ScalarContext context, string cacheServerUrl)
        {
            GitObjectsHttpRequestor objectRequestor = null;
            CacheServerInfo cacheServer = null;


            if (context.Enlistment.UsesGvfsProtocol)
            {
                this.InitializeServerConnection(
                    context.Tracer,
                    context.Enlistment,
                    cacheServerUrl,
                    out objectRequestor,
                    out cacheServer);
            }

            return new ActionAndMessage(() => this.RunFetchStep(context.Tracer, context.Enlistment, objectRequestor),
                                             "Fetching " + this.GetCacheServerDisplay(cacheServer, context.Enlistment.RepoUrl));
        }

        private ActionAndMessage GetCommitGraphAction(ScalarContext context)
        {
            return new ActionAndMessage(
                        () => new CommitGraphStep(context, requireObjectCacheLock: false).Execute(),
                        "Updating commit-graph");
        }

        private ActionAndMessage GetLooseObjectsAction(ScalarContext context)
        {
            return new ActionAndMessage(
                        () => new LooseObjectsStep(context, forceRun: !this.StartedByService).Execute(),
                        "Cleaning up loose objects");
        }

        private ActionAndMessage GetPackfileAction(ScalarContext context)
        {
            return new ActionAndMessage(
                            () => new PackfileMaintenanceStep(
                                        context,
                                        forceRun: !this.StartedByService,
                                        batchSize: string.IsNullOrWhiteSpace(this.PackfileMaintenanceBatchSize) ?
                                            PackfileMaintenanceStep.DefaultBatchSizeBytes.ToString() :
                                            this.PackfileMaintenanceBatchSize).Execute(),
                            "Cleaning up pack-files");
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

        private void RunFetchStep(ITracer tracer, ScalarEnlistment enlistment, GitObjectsHttpRequestor objectRequestor)
        {
            bool success;
            string error;
            PhysicalFileSystem fileSystem = new PhysicalFileSystem();
            ScalarContext context = new ScalarContext(tracer, fileSystem, enlistment);
            GitObjects gitObjects = new GitObjects(tracer, enlistment, objectRequestor, fileSystem);

            success = new FetchStep(context, gitObjects, requireCacheLock: false, forceRun: !this.StartedByService).TryFetch(out error);

            if (!success)
            {
                this.ReportErrorAndExit(tracer, ReturnCode.GenericError, "Fetch failed: " + error);
            }
        }

        private string GetCacheServerDisplay(CacheServerInfo cacheServer, string repoUrl)
        {
            if (cacheServer == null)
            {
                return "from remotes";
            }

            if (!cacheServer.IsNone(repoUrl))
            {
                return "from cache server";
            }

            return "from origin (no cache server)";
        }

        private struct ActionAndMessage
        {
            public ActionAndMessage(Action action, string message)
            {
                this.Action = action;
                this.Message = message;
            }

            public readonly Action Action { get; }
            public readonly string Message { get; }
        }
    }
}
