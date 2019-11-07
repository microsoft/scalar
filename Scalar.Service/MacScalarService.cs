using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.NamedPipes;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using Scalar.Service.Handlers;
using System;
using System.Threading;

namespace Scalar.Service
{
    public class MacScalarService
    {
        public const string ServiceNameArgPrefix = "--servicename=";

        private const string EtwArea = "ScalarService";

        private ITracer tracer;
        private Thread serviceThread;
        private ManualResetEvent serviceStopped;
        private string serviceName;
        private IScalarRepoRegistry repoRegistry;
        private RequestHandler requestHandler;
        private MaintenanceTaskScheduler maintenanceTaskScheduler;

        public MacScalarService(
            ITracer tracer,
            string serviceName,
            IScalarRepoRegistry repoRegistry)
        {
            this.tracer = tracer;
            this.repoRegistry = repoRegistry;
            this.serviceName = serviceName;

            this.serviceStopped = new ManualResetEvent(false);
            this.serviceThread = new Thread(this.ServiceThreadMain);
            this.requestHandler = new RequestHandler(this.tracer, EtwArea);
        }

        public void Run()
        {
            try
            {
                if (!string.IsNullOrEmpty(this.serviceName))
                {
                    string pipeName = ScalarPlatform.Instance.GetScalarServiceNamedPipeName(this.serviceName);
                    this.tracer.RelatedInfo("Starting pipe server with name: " + pipeName);

                    using (NamedPipeServer pipeServer = NamedPipeServer.StartNewServer(
                        pipeName,
                        this.tracer,
                        this.requestHandler.HandleRequest))
                    {
                        this.serviceThread.Start();
                        this.serviceThread.Join();
                    }
                }
                else
                {
                    this.tracer.RelatedError("No name specified for Service Pipe.");
                }
            }
            catch (Exception e)
            {
                this.LogExceptionAndExit(e, nameof(this.Run));
            }
        }

        private static EventMetadata CreateEventMetadata(Exception e = null)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add("Area", EtwArea);
            if (e != null)
            {
                metadata.Add("Exception", e.ToString());
            }

            return metadata;
        }

        private void ServiceThreadMain()
        {
            try
            {
                string currentUser = ScalarPlatform.Instance.GetCurrentUser();

                EventMetadata metadata = new EventMetadata();
                metadata.Add("Version", ProcessHelper.GetCurrentProcessVersion());
                metadata.Add(nameof(currentUser), currentUser);
                this.tracer.RelatedEvent(EventLevel.Informational, $"ScalarService_{nameof(this.ServiceThreadMain)}", metadata);

                if (int.TryParse(currentUser, out int sessionId))
                {
                    try
                    {
                        this.maintenanceTaskScheduler = new MaintenanceTaskScheduler(
                            this.tracer,
                            new PhysicalFileSystem(),
                            new MacScalarVerbRunner(this.tracer),
                            this.repoRegistry);

                        // On Mac, there is no separate session Id. currentUser is used as sessionId
                        this.maintenanceTaskScheduler.RegisterUser(new UserAndSession(currentUser, sessionId));
                    }
                    catch (Exception e)
                    {
                        this.tracer.RelatedError(CreateEventMetadata(e), "Failed to start maintenance scheduler");
                    }
                }
                else
                {
                    EventMetadata errorMetadata = CreateEventMetadata();
                    errorMetadata.Add(nameof(currentUser), currentUser);
                    this.tracer.RelatedError(
                        errorMetadata,
                        $"{nameof(this.ServiceThreadMain)}: Failed to parse current user as int.");
                }

                this.serviceStopped.WaitOne();
                this.serviceStopped.Dispose();
                this.serviceStopped = null;

                if (this.maintenanceTaskScheduler != null)
                {
                    this.maintenanceTaskScheduler.Dispose();
                    this.maintenanceTaskScheduler = null;
                }
            }
            catch (Exception e)
            {
                this.LogExceptionAndExit(e, nameof(this.ServiceThreadMain));
            }
        }

        private void LogExceptionAndExit(Exception e, string method)
        {
            this.tracer.RelatedError(CreateEventMetadata(e), "Unhandled exception in " + method);
            Environment.Exit((int)ReturnCode.GenericError);
        }
    }
}
