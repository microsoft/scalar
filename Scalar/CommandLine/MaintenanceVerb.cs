using System;
using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Http;
using Scalar.Common.Maintenance;
using Scalar.Common.Tracing;

namespace Scalar.CommandLine
{
    [Verb(MaintenanceVerb.MaintenanceVerbName, HelpText = "Perform a maintenance task in a Scalar repo")]
    public class MaintenanceVerb : ScalarVerb.ForExistingEnlistment
    {
        private const string MaintenanceVerbName = "maintenance";

        private const string LooseObjectsTaskName = "loose-objects";
        private const string PackfilesTaskName = "pack-files";
        private const string CommitGraphTaskName = "commit-graph";

        private const string BatchSizeOptionName = "batch-size";

        [Option(
            't',
            "task",
            Required = true,
            Default = "",
            HelpText = "Maintenance task to run.  Allowed values are '"
                + LooseObjectsTaskName + "', '"
                + PackfilesTaskName + "', '"
                + CommitGraphTaskName + "'")]
        public string MaintenanceTask { get; set; }

        [Option(
            BatchSizeOptionName,
            Required = false,
            Default = "",
            HelpText = "Batch size.  This option can only be used with the '" + PackfilesTaskName + "' task")]
        public string PackfileMaintenanceBatchSize { get; set; }

        protected override string VerbName
        {
            get { return MaintenanceVerb.MaintenanceVerbName; }
        }

        protected override void Execute(ScalarEnlistment enlistment)
        {
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, MaintenanceVerbName))
            {
                tracer.AddLogFileEventListener(
                    ScalarEnlistment.GetNewScalarLogFileName(enlistment.ScalarLogsRoot, ScalarConstants.LogFileTypes.Maintenance),
                    EventLevel.Informational,
                    Keywords.Any);
                tracer.WriteStartEvent(
                    enlistment.EnlistmentRoot,
                    enlistment.RepoUrl,
                    CacheServerResolver.GetUrlFromConfig(enlistment),
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

                            case PackfilesTaskName:
                                (new PackfileMaintenanceStep(
                                    context,
                                    forceRun: true,
                                    batchSize: string.IsNullOrWhiteSpace(this.PackfileMaintenanceBatchSize) ?
                                        PackfileMaintenanceStep.DefaultBatchSize :
                                        this.PackfileMaintenanceBatchSize)).Execute();
                                return;

                            case CommitGraphTaskName:
                                this.FailIfBatchSizeSet(tracer);
                                (new PostFetchStep(context, new System.Collections.Generic.List<string>(), requireObjectCacheLock: false)).Execute();
                                return;

                            default:
                                this.ReportErrorAndExit($"Unknown maintenance task requested: '{this.MaintenanceTask}'");
                                break;
                        }
                    }
                    catch (Exception e) when (!(e is VerbAbortedException))
                    {
                        string error = $"Exception thrown while running {this.MaintenanceTask} task: {e.Message}";
                        EventMetadata metadata = this.CreateEventMetadata(e);
                        tracer.RelatedError(metadata, error);
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
                    $"--{BatchSizeOptionName} can only be used with the {PackfilesTaskName} task");
            }
        }
    }
}
