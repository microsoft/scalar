using Scalar.Common;
using Scalar.Common.Tracing;
using Scalar.PlatformLoader;
using System;
using System.Diagnostics;
using System.ServiceProcess;

namespace Scalar.Service
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            ScalarPlatformLoader.Initialize();

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

            using (JsonTracer tracer = new JsonTracer(ScalarConstants.Service.ServiceName, ScalarConstants.Service.ServiceName))
            {
                using (ScalarService service = new ScalarService(tracer))
                {
                    // This will fail with a popup from a command prompt. To install as a service, run:
                    // %windir%\Microsoft.NET\Framework64\v4.0.30319\installutil Scalar.Service.exe
                    ServiceBase.Run(service);
                }
            }
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = "Application";
                eventLog.WriteEntry(
                    "Unhandled exception in Scalar.Service: " + e.ExceptionObject.ToString(),
                    EventLogEntryType.Error);
            }
        }
    }
}
