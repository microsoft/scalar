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

        public void SendNotification(NamedPipeMessages.Notification.Request request)
        {
            using (NamedPipeClient client = new NamedPipeClient(ScalarConstants.Service.UIName))
            {
                if (client.Connect())
                {
                    try
                    {
                        if (!client.TrySendRequest(request.ToMessage()))
                        {
                            this.tracer.RelatedInfo("Failed to send notification request to " + ScalarConstants.Service.UIName);
                        }
                    }
                    catch (Exception ex)
                    {
                        EventMetadata metadata = new EventMetadata();
                        metadata.Add("Exception", ex.ToString());
                        metadata.Add("Identifier", request.Id);
                        this.tracer.RelatedError(metadata, $"{nameof(this.SendNotification)}- Could not send notification request({request.Id}. {ex.ToString()}");
                    }
                }
                else
                {
                    this.tracer.RelatedError($"{nameof(this.SendNotification)}- Could not connect with GVFS.Service.UI, failed to send notification request({request.Id}.");
                }
            }
        }
    }
}
