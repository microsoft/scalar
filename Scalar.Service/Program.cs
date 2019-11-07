using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using Scalar.PlatformLoader;
using Scalar.Service.Handlers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace Scalar.Service
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            ScalarPlatformLoader.Initialize();

            using (JsonTracer tracer = new JsonTracer(ScalarConstants.Service.ServiceName, ScalarConstants.Service.ServiceName))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    AppDomain.CurrentDomain.UnhandledException += EventLogUnhandledExceptionHandler;

                    using (WindowsScalarService service = new WindowsScalarService(tracer))
                    {
                        // This will fail with a popup from a command prompt. To install as a service, run:
                        // %windir%\Microsoft.NET\Framework64\v4.0.30319\installutil Scalar.Service.exe
                        ServiceBase.Run(service);
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    AppDomain.CurrentDomain.UnhandledException += JsonUnhandledExceptionHandler;

                    CreateMacService(tracer, args).Run();
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        private static MacScalarService CreateMacService(JsonTracer tracer, string[] args)
        {
            string serviceName = args.FirstOrDefault(arg => arg.StartsWith(MacScalarService.ServiceNameArgPrefix, StringComparison.OrdinalIgnoreCase));
            if (serviceName != null)
            {
                serviceName = serviceName.Substring(MacScalarService.ServiceNameArgPrefix.Length);
            }
            else
            {
                serviceName = ScalarConstants.Service.ServiceName;
            }

            ScalarPlatform scalarPlatform = ScalarPlatform.Instance;

            string logFilePath = Path.Combine(
                scalarPlatform.GetDataRootForScalarComponent(serviceName),
                ScalarConstants.Service.LogDirectory);
            Directory.CreateDirectory(logFilePath);

            tracer.AddLogFileEventListener(
                ScalarEnlistment.GetNewScalarLogFileName(logFilePath, ScalarConstants.LogFileTypes.Service),
                EventLevel.Informational,
                Keywords.Any);

            string repoRegistryLocation = scalarPlatform.GetDataRootForScalarComponent(ScalarConstants.RepoRegistry.RegistryDirectoryName);
            ScalarRepoRegistry repoRegistry = new ScalarRepoRegistry(
                tracer,
                new PhysicalFileSystem(),
                repoRegistryLocation);

            return new MacScalarService(tracer, serviceName, repoRegistry);
        }

        private static void JsonUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.Service.ServiceName, ScalarConstants.Service.ServiceName))
            {
                tracer.RelatedError($"Unhandled exception in Scalar.Service: {e.ExceptionObject.ToString()}");
            }
        }

        private static void EventLogUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
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
