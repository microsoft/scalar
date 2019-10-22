using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using Scalar.PlatformLoader;
using Scalar.Service.Handlers;
using System;
using System.IO;
using System.Linq;

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
                CreateService(tracer, args).Run();
            }
        }

        private static ScalarService CreateService(JsonTracer tracer, string[] args)
        {
            string serviceName = args.FirstOrDefault(arg => arg.StartsWith(ScalarService.ServiceNameArgPrefix, StringComparison.OrdinalIgnoreCase));
            if (serviceName != null)
            {
                serviceName = serviceName.Substring(ScalarService.ServiceNameArgPrefix.Length);
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

            string serviceDataLocation = scalarPlatform.GetDataRootForScalarComponent(serviceName);
            RepoRegistry repoRegistry = new RepoRegistry(
                tracer,
                new PhysicalFileSystem(),
                serviceDataLocation,
                new ScalarVerbRunner(tracer),
                new NotificationHandler(tracer));

            return new ScalarService(tracer, serviceName, repoRegistry);
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.Service.ServiceName, ScalarConstants.Service.ServiceName))
            {
                tracer.RelatedError($"Unhandled exception in Scalar.Service: {e.ExceptionObject.ToString()}");
            }
        }
    }
}
