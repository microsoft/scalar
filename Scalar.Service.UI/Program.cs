using Scalar.Common;
using Scalar.Common.NamedPipes;
using Scalar.Common.Tracing;
using Scalar.PlatformLoader;
using Scalar.Service.UI.Data;
using System;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Xml;
using System.Xml.Serialization;
using Windows.UI.Notifications;
using XmlDocument = Windows.Data.Xml.Dom.XmlDocument;

namespace Scalar.Service.UI
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            ScalarPlatformLoader.Initialize();

            using (JsonTracer tracer = new JsonTracer("Microsoft.Git.GVFS.Service.UI", "Service.UI"))
            {
                string error;
                string serviceUILogDirectory = ScalarPlatform.Instance.GetLogsDirectoryForGVFSComponent(ScalarConstants.Service.UIName);
                if (!ScalarPlatform.Instance.FileSystem.TryCreateDirectoryWithAdminAndUserModifyPermissions(serviceUILogDirectory, out error))
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add(nameof(serviceUILogDirectory), serviceUILogDirectory);
                    metadata.Add(nameof(error), error);
                    tracer.RelatedWarning(
                        metadata,
                        "Failed to create service UI logs directory",
                        Keywords.Telemetry);
                }
                else
                {
                    string logFilePath = ScalarEnlistment.GetNewScalarLogFileName(
                        serviceUILogDirectory,
                        ScalarConstants.LogFileTypes.ServiceUI,
                        logId: Environment.UserName);

                    tracer.AddLogFileEventListener(logFilePath, EventLevel.Informational, Keywords.Any);
                }

                WinToastNotifier winToastNotifier = new WinToastNotifier(tracer);
                ScalarToastRequestHandler toastRequestHandler = new ScalarToastRequestHandler(winToastNotifier, tracer);
                GVFSServiceUI process = new GVFSServiceUI(tracer, toastRequestHandler);

                process.Start(args);
            }
        }
    }
}
