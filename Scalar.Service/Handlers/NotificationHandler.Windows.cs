using Scalar.Common;
using Scalar.Common.NamedPipes;
using Scalar.Common.Tracing;
using Scalar.Platform.Windows;
using System;
using System.Diagnostics;

namespace Scalar.Service.Handlers
{
    public class NotificationHandler : INotificationHandler
    {
        private ITracer tracer;

        public NotificationHandler(ITracer tracer)
        {
            this.tracer = tracer;
        }

        public void SendNotification(int sessionId, NamedPipeMessages.Notification.Request request)
        {
            NamedPipeClient client;
            if (!this.TryOpenConnectionToUIProcess(out client))
            {
                this.TerminateExistingProcess(ScalarConstants.Service.UIName);

                CurrentUser currentUser = new CurrentUser(this.tracer, sessionId);
                if (!currentUser.RunAs(
                    Configuration.Instance.ScalarServiceUILocation,
                    string.Empty))
                {
                    this.tracer.RelatedError("Could not start " + ScalarConstants.Service.UIName);
                    return;
                }

                this.TryOpenConnectionToUIProcess(out client);
            }

            if (client == null)
            {
                this.tracer.RelatedError("Failed to connect to " + ScalarConstants.Service.UIName);
                return;
            }

            try
            {
                if (!client.TrySendRequest(request.ToMessage()))
                {
                    this.tracer.RelatedInfo("Failed to send notification request to " + ScalarConstants.Service.UIName);
                }
            }
            finally
            {
                client.Dispose();
            }
        }

        private bool TryOpenConnectionToUIProcess(out NamedPipeClient client)
        {
            client = new NamedPipeClient(ScalarConstants.Service.UIName);
            if (client.Connect())
            {
                return true;
            }

            client.Dispose();
            client = null;
            return false;
        }

        private void TerminateExistingProcess(string processName)
        {
            try
            {
                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                this.tracer.RelatedError("Could not find and kill existing instances of {0}: {1}", processName, ex.Message);
            }
        }
    }
}
