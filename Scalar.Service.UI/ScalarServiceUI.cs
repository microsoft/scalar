using Scalar.Common;
using Scalar.Common.NamedPipes;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Scalar.Service.UI
{
    public class GVFSServiceUI
    {
        private readonly ITracer tracer;
        private readonly ScalarToastRequestHandler toastRequestHandler;

        public GVFSServiceUI(ITracer tracer, ScalarToastRequestHandler toastRequestHandler)
        {
            this.tracer = tracer;
            this.toastRequestHandler = toastRequestHandler;
        }

        public void Start(string[] args)
        {
            using (ITracer activity = this.tracer.StartActivity("Start", EventLevel.Informational))
            using (NamedPipeServer server = NamedPipeServer.StartNewServer(ScalarConstants.Service.UIName, this.tracer, this.HandleRequest))
            {
                ManualResetEvent mre = new ManualResetEvent(false);
                mre.WaitOne();
            }
        }

        private void HandleRequest(ITracer tracer, string request, NamedPipeServer.Connection connection)
        {
            try
            {
                NamedPipeMessages.Message message = NamedPipeMessages.Message.FromString(request);
                switch (message.Header)
                {
                    case NamedPipeMessages.Notification.Request.Header:
                        NamedPipeMessages.Notification.Request toastRequest = NamedPipeMessages.Notification.Request.FromMessage(message);
                        if (toastRequest != null)
                        {
                            using (ITracer activity = this.tracer.StartActivity("SendToast", EventLevel.Informational))
                            {
                                this.toastRequestHandler.HandleToastRequest(activity, toastRequest);
                            }
                        }

                        break;
                }
            }
            catch (Exception e)
            {
                this.tracer.RelatedError("Unhandled exception: {0}", e.ToString());
            }
        }
    }
}
