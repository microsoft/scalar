using Scalar.Common;
using Scalar.Common.NamedPipes;
using Scalar.Common.Tracing;
using Scalar.Service.Handlers;
using System;
using System.Threading;

namespace Scalar.Service
{
    public class ScalarService
    {
        public const string ServiceNameArgPrefix = "--servicename=";

        private const string EtwArea = nameof(ScalarService);

        private ITracer tracer;
        private Thread serviceThread;
        private ManualResetEvent serviceStopped;
        private string serviceName;
        private IRepoRegistry repoRegistry;
        private RequestHandler requestHandler;
        private MaintenanceTaskScheduler maintenanceTaskScheduler;

        public ScalarService(
            ITracer tracer,
            string serviceName,
            IRepoRegistry repoRegistry)
        {
            this.tracer = tracer;
            this.repoRegistry = repoRegistry;
            this.serviceName = serviceName;

            this.serviceStopped = new ManualResetEvent(false);
            this.serviceThread = new Thread(this.ServiceThreadMain);
            this.requestHandler = new RequestHandler(this.tracer, EtwArea, this.repoRegistry);
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

        private void ServiceThreadMain()
        {
            try
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add("Version", ProcessHelper.GetCurrentProcessVersion());
                this.tracer.RelatedEvent(EventLevel.Informational, $"{nameof(ScalarService)}_{nameof(this.ServiceThreadMain)}", metadata);

                try
                {
                    this.maintenanceTaskScheduler = new MaintenanceTaskScheduler(this.repoRegistry);

                    string currentUser = ScalarPlatform.Instance.GetCurrentUser();
                    if (int.TryParse(currentUser, out int sessionId))
                    {
                        // On Mac, there is no separate session Id. currentUser is used as sessionId
                        this.maintenanceTaskScheduler.RegisterActiveUser(currentUser, sessionId);
                    }
                    else
                    {
                        this.tracer.RelatedError($"{nameof(this.ServiceThreadMain)} Error: could not parse current user '{currentUser}' as int.");
                    }
                }
                catch (Exception e)
                {
                    EventMetadata exceptionMetadata = new EventMetadata();
                    exceptionMetadata.Add("Area", EtwArea);
                    exceptionMetadata.Add("Exception", e.ToString());
                    this.tracer.RelatedError(exceptionMetadata, "Failed to start maintenance scheduler");
                }

                this.serviceStopped.WaitOne();
                this.serviceStopped.Dispose();
            }
            catch (Exception e)
            {
                this.LogExceptionAndExit(e, nameof(this.ServiceThreadMain));
            }
        }

        private void LogExceptionAndExit(Exception e, string method)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add("Area", EtwArea);
            metadata.Add("Exception", e.ToString());
            this.tracer.RelatedError(metadata, "Unhandled exception in " + method);
            Environment.Exit((int)ReturnCode.GenericError);
        }
    }
}
