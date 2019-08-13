using Newtonsoft.Json;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.Maintenance;
using Scalar.Common.NamedPipes;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Scalar.Mount
{
    public class InProcessMount
    {
        private readonly bool showDebugWindow;

        private ScalarEnlistment enlistment;
        private ITracer tracer;
        private GitMaintenanceScheduler maintenanceScheduler;

        private CacheServerInfo cacheServer;
        private RetryConfig retryConfig;

        private ScalarContext context;
        private ScalarGitObjects gitObjects;

        private MountState currentState;
        private ManualResetEvent unmountEvent;

        public InProcessMount(ITracer tracer, ScalarEnlistment enlistment, CacheServerInfo cacheServer, RetryConfig retryConfig, bool showDebugWindow)
        {
            this.tracer = tracer;
            this.retryConfig = retryConfig;
            this.cacheServer = cacheServer;
            this.enlistment = enlistment;
            this.showDebugWindow = showDebugWindow;
            this.unmountEvent = new ManualResetEvent(false);
        }

        private enum MountState
        {
            Invalid = 0,

            Mounting,
            Ready,
            Unmounting,
            MountFailed
        }

        public void Mount(EventLevel verbosity, Keywords keywords)
        {
            this.currentState = MountState.Mounting;

            // We must initialize repo metadata before starting the pipe server so it
            // can immediately handle status requests
            string error;
            if (!RepoMetadata.TryInitialize(this.tracer, this.enlistment.DotScalarRoot, out error))
            {
                this.FailMountAndExit("Failed to load repo metadata: " + error);
            }

            string gitObjectsRoot;
            if (!RepoMetadata.Instance.TryGetGitObjectsRoot(out gitObjectsRoot, out error))
            {
                this.FailMountAndExit("Failed to determine git objects root from repo metadata: " + error);
            }

            string localCacheRoot;
            if (!RepoMetadata.Instance.TryGetLocalCacheRoot(out localCacheRoot, out error))
            {
                this.FailMountAndExit("Failed to determine local cache path from repo metadata: " + error);
            }

            string blobSizesRoot;
            if (!RepoMetadata.Instance.TryGetBlobSizesRoot(out blobSizesRoot, out error))
            {
                this.FailMountAndExit("Failed to determine blob sizes root from repo metadata: " + error);
            }

            this.tracer.RelatedEvent(
                EventLevel.Informational,
                "CachePathsLoaded",
                new EventMetadata
                {
                    { "gitObjectsRoot", gitObjectsRoot },
                    { "localCacheRoot", localCacheRoot },
                    { "blobSizesRoot", blobSizesRoot },
                });

            this.enlistment.InitializeCachePaths(localCacheRoot, gitObjectsRoot, blobSizesRoot);

            using (NamedPipeServer pipeServer = this.StartNamedPipe())
            {
                this.tracer.RelatedEvent(
                    EventLevel.Informational,
                    $"{nameof(this.Mount)}_StartedNamedPipe",
                    new EventMetadata { { "NamedPipeName", this.enlistment.NamedPipeName } });

                this.context = this.CreateContext();

                if (this.context.Unattended)
                {
                    this.tracer.RelatedEvent(EventLevel.Critical, ScalarConstants.UnattendedEnvironmentVariable, null);
                }

                this.ValidateMountPoints();

                string errorMessage;
                if (!HooksInstaller.TryUpdateHooks(this.context, out errorMessage))
                {
                    this.FailMountAndExit(errorMessage);
                }

                ScalarPlatform.Instance.ConfigureVisualStudio(this.enlistment.GitBinPath, this.tracer);

                this.MountAndStartWorkingDirectoryCallbacks(this.cacheServer);

                Console.Title = "Scalar " + ProcessHelper.GetCurrentProcessVersion() + " - " + this.enlistment.EnlistmentRoot;

                this.tracer.RelatedEvent(
                    EventLevel.Informational,
                    "Mount",
                    new EventMetadata
                    {
                        // Use TracingConstants.MessageKey.InfoMessage rather than TracingConstants.MessageKey.CriticalMessage
                        // as this message should not appear as an error
                        { TracingConstants.MessageKey.InfoMessage, "Virtual repo is ready" },
                    },
                    Keywords.Telemetry);

                this.currentState = MountState.Ready;

                this.unmountEvent.WaitOne();
            }
        }

        private ScalarContext CreateContext()
        {
            PhysicalFileSystem fileSystem = new PhysicalFileSystem();
            GitRepo gitRepo = this.CreateOrReportAndExit(
                () => new GitRepo(
                    this.tracer,
                    this.enlistment,
                    fileSystem),
                "Failed to read git repo");
            return new ScalarContext(this.tracer, fileSystem, gitRepo, this.enlistment);
        }

        private void ValidateMountPoints()
        {
            DirectoryInfo workingDirectoryRootInfo = new DirectoryInfo(this.enlistment.WorkingDirectoryBackingRoot);
            if (!workingDirectoryRootInfo.Exists)
            {
                this.FailMountAndExit("Failed to initialize file system callbacks. Directory \"{0}\" must exist.", this.enlistment.WorkingDirectoryBackingRoot);
            }

            string dotGitPath = Path.Combine(this.enlistment.WorkingDirectoryBackingRoot, ScalarConstants.DotGit.Root);
            DirectoryInfo dotGitPathInfo = new DirectoryInfo(dotGitPath);
            if (!dotGitPathInfo.Exists)
            {
                this.FailMountAndExit("Failed to mount. Directory \"{0}\" must exist.", dotGitPathInfo);
            }
        }

        private NamedPipeServer StartNamedPipe()
        {
            try
            {
                return NamedPipeServer.StartNewServer(this.enlistment.NamedPipeName, this.tracer, this.HandleRequest);
            }
            catch (PipeNameLengthException)
            {
                this.FailMountAndExit("Failed to create mount point. Mount path exceeds the maximum number of allowed characters");
                return null;
            }
        }

        private void FailMountAndExit(string error, params object[] args)
        {
            this.currentState = MountState.MountFailed;

            this.tracer.RelatedError(error, args);
            if (this.showDebugWindow)
            {
                Console.WriteLine("\nPress Enter to Exit");
                Console.ReadLine();
            }

            Environment.Exit((int)ReturnCode.GenericError);
        }

        private T CreateOrReportAndExit<T>(Func<T> factory, string reportMessage)
        {
            try
            {
                return factory();
            }
            catch (Exception e)
            {
                this.FailMountAndExit(reportMessage + " " + e.ToString());
                throw;
            }
        }

        private void HandleRequest(ITracer tracer, string request, NamedPipeServer.Connection connection)
        {
            NamedPipeMessages.Message message = NamedPipeMessages.Message.FromString(request);

            switch (message.Header)
            {
                case NamedPipeMessages.GetStatus.Request:
                    this.HandleGetStatusRequest(connection);
                    break;

                case NamedPipeMessages.Unmount.Request:
                    this.HandleUnmountRequest(connection);
                    break;

                case NamedPipeMessages.DownloadObject.DownloadRequest:
                    this.HandleDownloadObjectRequest(message, connection);
                    break;

                case NamedPipeMessages.RunPostFetchJob.PostFetchJob:
                    this.HandlePostFetchJobRequest(message, connection);
                    break;

                default:
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", "Mount");
                    metadata.Add("Header", message.Header);
                    this.tracer.RelatedError(metadata, "HandleRequest: Unknown request");

                    connection.TrySendResponse(NamedPipeMessages.UnknownRequest);
                    break;
            }
        }

        private void HandleDownloadObjectRequest(NamedPipeMessages.Message message, NamedPipeServer.Connection connection)
        {
            NamedPipeMessages.DownloadObject.Response response;

            NamedPipeMessages.DownloadObject.Request request = new NamedPipeMessages.DownloadObject.Request(message);
            string objectSha = request.RequestSha;
            if (this.currentState != MountState.Ready)
            {
                response = new NamedPipeMessages.DownloadObject.Response(NamedPipeMessages.MountNotReadyResult);
            }
            else
            {
                if (!SHA1Util.IsValidShaFormat(objectSha))
                {
                    response = new NamedPipeMessages.DownloadObject.Response(NamedPipeMessages.DownloadObject.InvalidSHAResult);
                }
                else
                {
                    Stopwatch downloadTime = Stopwatch.StartNew();
                    if (this.gitObjects.TryDownloadAndSaveObject(objectSha, ScalarGitObjects.RequestSource.NamedPipeMessage) == GitObjects.DownloadAndSaveObjectResult.Success)
                    {
                        response = new NamedPipeMessages.DownloadObject.Response(NamedPipeMessages.DownloadObject.SuccessResult);
                    }
                    else
                    {
                        response = new NamedPipeMessages.DownloadObject.Response(NamedPipeMessages.DownloadObject.DownloadFailed);
                    }

                    bool isBlob;
                    this.context.Repository.TryGetIsBlob(objectSha, out isBlob);
                    this.context.Repository.ScalarLock.Stats.RecordObjectDownload(isBlob, downloadTime.ElapsedMilliseconds);
                }
            }

            connection.TrySendResponse(response.CreateMessage());
        }

        private void HandlePostFetchJobRequest(NamedPipeMessages.Message message, NamedPipeServer.Connection connection)
        {
            NamedPipeMessages.RunPostFetchJob.Request request = new NamedPipeMessages.RunPostFetchJob.Request(message);

            this.tracer.RelatedInfo("Received post-fetch job request with body {0}", message.Body);

            NamedPipeMessages.RunPostFetchJob.Response response;
            if (this.currentState == MountState.Ready)
            {
                List<string> packIndexes = JsonConvert.DeserializeObject<List<string>>(message.Body);
                this.maintenanceScheduler.EnqueueOneTimeStep(new PostFetchStep(this.context, packIndexes));

                response = new NamedPipeMessages.RunPostFetchJob.Response(NamedPipeMessages.RunPostFetchJob.QueuedResult);
            }
            else
            {
                response = new NamedPipeMessages.RunPostFetchJob.Response(NamedPipeMessages.RunPostFetchJob.MountNotReadyResult);
            }

            connection.TrySendResponse(response.CreateMessage());
        }

        private void HandleGetStatusRequest(NamedPipeServer.Connection connection)
        {
            NamedPipeMessages.GetStatus.Response response = new NamedPipeMessages.GetStatus.Response();
            response.EnlistmentRoot = this.enlistment.EnlistmentRoot;
            response.LocalCacheRoot = !string.IsNullOrWhiteSpace(this.enlistment.LocalCacheRoot) ? this.enlistment.LocalCacheRoot : this.enlistment.GitObjectsRoot;
            response.RepoUrl = this.enlistment.RepoUrl;
            response.CacheServer = this.cacheServer.ToString();
            response.DiskLayoutVersion = $"{ScalarPlatform.Instance.DiskLayoutUpgrade.Version.CurrentMajorVersion}.{ScalarPlatform.Instance.DiskLayoutUpgrade.Version.CurrentMinorVersion}";

            switch (this.currentState)
            {
                case MountState.Mounting:
                    response.MountStatus = NamedPipeMessages.GetStatus.Mounting;
                    break;

                case MountState.Ready:
                    response.MountStatus = NamedPipeMessages.GetStatus.Ready;
                    break;

                case MountState.Unmounting:
                    response.MountStatus = NamedPipeMessages.GetStatus.Unmounting;
                    break;

                case MountState.MountFailed:
                    response.MountStatus = NamedPipeMessages.GetStatus.MountFailed;
                    break;

                default:
                    response.MountStatus = NamedPipeMessages.UnknownScalarState;
                    break;
            }

            connection.TrySendResponse(response.ToJson());
        }

        private void HandleUnmountRequest(NamedPipeServer.Connection connection)
        {
            switch (this.currentState)
            {
                case MountState.Mounting:
                    connection.TrySendResponse(NamedPipeMessages.Unmount.NotMounted);
                    break;

                // Even if the previous mount failed, attempt to unmount anyway.  Otherwise the user has no
                // recourse but to kill the process.
                case MountState.MountFailed:
                    goto case MountState.Ready;

                case MountState.Ready:
                    this.currentState = MountState.Unmounting;

                    connection.TrySendResponse(NamedPipeMessages.Unmount.Acknowledged);
                    this.UnmountAndStopWorkingDirectoryCallbacks();
                    connection.TrySendResponse(NamedPipeMessages.Unmount.Completed);

                    this.unmountEvent.Set();
                    Environment.Exit((int)ReturnCode.Success);
                    break;

                case MountState.Unmounting:
                    connection.TrySendResponse(NamedPipeMessages.Unmount.AlreadyUnmounting);
                    break;

                default:
                    connection.TrySendResponse(NamedPipeMessages.UnknownScalarState);
                    break;
            }
        }

        private void MountAndStartWorkingDirectoryCallbacks(CacheServerInfo cache)
        {
            string error;
            if (!this.context.Enlistment.Authentication.TryInitialize(this.context.Tracer, this.context.Enlistment, out error))
            {
                this.FailMountAndExit("Failed to obtain git credentials: " + error);
            }

            GitObjectsHttpRequestor objectRequestor = new GitObjectsHttpRequestor(this.context.Tracer, this.context.Enlistment, cache, this.retryConfig);
            this.gitObjects = new ScalarGitObjects(this.context, objectRequestor);

            this.maintenanceScheduler = this.CreateOrReportAndExit(() => new GitMaintenanceScheduler(this.context, this.gitObjects), "Failed to start maintenance scheduler");

            int majorVersion;
            int minorVersion;
            if (!RepoMetadata.Instance.TryGetOnDiskLayoutVersion(out majorVersion, out minorVersion, out error))
            {
                this.FailMountAndExit("Error: {0}", error);
            }

            if (majorVersion != ScalarPlatform.Instance.DiskLayoutUpgrade.Version.CurrentMajorVersion)
            {
                this.FailMountAndExit(
                    "Error: On disk version ({0}) does not match current version ({1})",
                    majorVersion,
                    ScalarPlatform.Instance.DiskLayoutUpgrade.Version.CurrentMajorVersion);
            }
        }

        private void UnmountAndStopWorkingDirectoryCallbacks()
        {
            if (this.maintenanceScheduler != null)
            {
                this.maintenanceScheduler.Dispose();
                this.maintenanceScheduler = null;
            }
        }
    }
}
