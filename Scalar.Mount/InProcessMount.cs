using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.Maintenance;
using Scalar.Common.NamedPipes;
using Scalar.Common.Tracing;
using System;
using System.IO;
using System.Threading;

namespace Scalar.Mount
{
    public class InProcessMount
    {
        private readonly bool showDebugWindow;

        private ScalarEnlistment enlistment;
        private ITracer tracer;

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
            GitProcess process = new GitProcess(this.enlistment);
            GitProcess.ConfigResult result = process.GetFromLocalConfig(ScalarConstants.GitConfig.ObjectCache);
            if (!result.TryParseAsString(out gitObjectsRoot, out error))
            {
                this.FailMountAndExit("Failed to determine git objects root from git config: " + error);
            }

            string localCacheRoot = Path.GetDirectoryName(gitObjectsRoot);
            this.tracer.RelatedEvent(
                EventLevel.Informational,
                "CachePathsLoaded",
                new EventMetadata
                {
                    { "gitObjectsRoot", gitObjectsRoot },
                    { "localCacheRoot", localCacheRoot },
                });

            this.enlistment.InitializeCachePaths(localCacheRoot, gitObjectsRoot);

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
            return new ScalarContext(this.tracer, fileSystem, this.enlistment);
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

        private void HandleRequest(ITracer requestTracer, string request, NamedPipeServer.Connection connection)
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

                default:
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", "Mount");
                    metadata.Add("Header", message.Header);
                    this.tracer.RelatedError(metadata, "HandleRequest: Unknown request");

                    connection.TrySendResponse(NamedPipeMessages.UnknownRequest);
                    break;
            }
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
    }
}
