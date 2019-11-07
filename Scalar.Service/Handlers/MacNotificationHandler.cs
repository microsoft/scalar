using Scalar.Common.NamedPipes;
using Scalar.Common.Tracing;
using System;
using System.IO;

namespace Scalar.Service.Handlers
{
    public class MacNotificationHandler : INotificationHandler
    {
        private const string NotificationServerPipeName = "scalar.notification";
        private ITracer tracer;

        public MacNotificationHandler(ITracer tracer)
        {
            this.tracer = tracer;
        }

        public void SendNotification(NamedPipeMessages.Notification.Request request)
        {
            string pipeName = Path.Combine(Path.GetTempPath(), NotificationServerPipeName);
            using (NamedPipeClient client = new NamedPipeClient(pipeName))
            {
                if (client.Connect())
                {
                    try
                    {
                        client.SendRequest(request.ToMessage());
                    }
                    catch (Exception ex)
                    {
                        EventMetadata metadata = new EventMetadata();
                        metadata.Add("Area", "NotificationHandler");
                        metadata.Add("Exception", ex.ToString());
                        metadata.Add(TracingConstants.MessageKey.ErrorMessage, "MacOS notification display error");
                        this.tracer.RelatedError(metadata, $"MacOS notification: {request.Title} - {request.Message}.");
                    }
                }
                else
                {
                    this.tracer.RelatedError($"ERROR: Communication failure with native notification display tool. Notification info: {request.Title} - {request.Message}.");
                }
            }
        }
    }
}
