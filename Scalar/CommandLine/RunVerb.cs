using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.Maintenance;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;

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

                List<GitMaintenanceStep> steps = new List<GitMaintenanceStep>();

                try
                {
                    tracer.AddLogFileEventListener(
                        logFileName,
                        EventLevel.Informational,
                        Keywords.Any);
                }
                catch (IOException e1)
                {
                    // There was likely difficulty loading the log file.
                    if (this.StartedByService)
                    {
                        // Regenerate a log file name using a timestamp
                        string newFileName = ScalarEnlistment.GetNewScalarLogFileName(
                                                    enlistment.ScalarLogsRoot,
                                                    ScalarConstants.LogFileTypes.Maintenance);

                        try
                        {
                            tracer.AddLogFileEventListener(
                                newFileName,
                                EventLevel.Informational,
                                Keywords.Any);
                            tracer.RelatedWarning($"Failed to use service log file '{logFileName}': {e1.Message}");
                        }
                        catch (IOException e2)
                        {
                            tracer.RelatedError($"Failed to use either log file '{logFileName}' or '{newFileName}': {e2.Message}");
                        }
                    }
                }

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
                        GitObjectsHttpRequestor objectRequestor = null;
                        CacheServerInfo cacheServer;
                        GitObjects gitObjects;
                        GitFeatureFlags gitFeatures = this.GetAvailableGitFeatures(tracer);

                        switch (this.MaintenanceTask)
                        {
                            case ScalarConstants.VerbParameters.Maintenance.AllTasksName:
                                steps.Add(new ConfigStep(context));
                                this.InitializeServerConnection(tracer, enlistment, cacheServerUrl, out objectRequestor, out cacheServer);
                                gitObjects = new GitObjects(tracer, enlistment, objectRequestor, fileSystem);
                                steps.Add(new FetchStep(context, gitObjects, requireCacheLock: false, gitFeatures: gitFeatures, forceRun: !this.StartedByService));
                                steps.Add(new CommitGraphStep(context, gitFeatures, requireObjectCacheLock: false));
                                steps.Add(new LooseObjectsStep(context, forceRun: !this.StartedByService, gitFeatures: gitFeatures));
                                steps.Add(new PackfileMaintenanceStep(
                                        context,
                                        forceRun: !this.StartedByService,
                                        batchSize: string.IsNullOrWhiteSpace(this.PackfileMaintenanceBatchSize) ?
                                            PackfileMaintenanceStep.DefaultBatchSizeBytes.ToString() :
                                            this.PackfileMaintenanceBatchSize,
                                        gitFeatures: gitFeatures));
                                break;

                            case ScalarConstants.VerbParameters.Maintenance.LooseObjectsTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                steps.Add(new LooseObjectsStep(context, forceRun: !this.StartedByService, gitFeatures: gitFeatures));
                                break;

                            case ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName:
                                steps.Add(new PackfileMaintenanceStep(
                                        context,
                                        forceRun: !this.StartedByService,
                                        batchSize: string.IsNullOrWhiteSpace(this.PackfileMaintenanceBatchSize) ?
                                            PackfileMaintenanceStep.DefaultBatchSizeBytes.ToString() :
                                            this.PackfileMaintenanceBatchSize,
                                        gitFeatures: gitFeatures));
                                break;

                            case ScalarConstants.VerbParameters.Maintenance.FetchTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                this.InitializeServerConnection(tracer, enlistment, cacheServerUrl, out objectRequestor, out cacheServer);
                                gitObjects = new GitObjects(tracer, enlistment, objectRequestor, fileSystem);
                                steps.Add(new FetchStep(context, gitObjects, requireCacheLock: false, gitFeatures: gitFeatures, forceRun: !this.StartedByService));
                                break;

                            case ScalarConstants.VerbParameters.Maintenance.CommitGraphTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                steps.Add(new CommitGraphStep(context, gitFeatures, requireObjectCacheLock: false));
                                break;

                            case ScalarConstants.VerbParameters.Maintenance.ConfigTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                steps.Add(new ConfigStep(context));
                                break;

                            default:
                                this.ReportErrorAndExit($"Unknown maintenance task requested: '{this.MaintenanceTask}'");
                                break;
                        }

                        foreach (GitMaintenanceStep step in steps)
                        {
                            this.ShowStatusWhileRunning(() => { step.Execute(); return true; }, step.ProgressMessage);
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
            if (!enlistment.UsesGvfsProtocol)
            {
                objectRequestor = null;
                cacheServer = null;
                return;
            }

            RetryConfig retryConfig = this.GetRetryConfig(tracer, enlistment, TimeSpan.FromMinutes(RetryConfig.FetchAndCloneTimeoutMinutes));

            cacheServer = this.ResolvedCacheServer;
            ServerScalarConfig serverScalarConfig = this.ServerScalarConfig;
            if (!this.SkipVersionCheck)
            {
                string authErrorMessage;
                if (this.TryAuthenticate(tracer, enlistment, out authErrorMessage) != GitAuthentication.Result.Success)
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

                this.Output.WriteLine("Configured cache server: " + cacheServer);
            }

            this.InitializeCachePaths(tracer, enlistment);
            objectRequestor = new GitObjectsHttpRequestor(tracer, enlistment, cacheServer, retryConfig);
        }
    }
}
