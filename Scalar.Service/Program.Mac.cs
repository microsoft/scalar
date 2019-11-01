using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using Scalar.PlatformLoader;
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

            string repoRegistryLocation = scalarPlatform.GetDataRootForScalarComponent(ScalarConstants.RepoRegistry.RegistryDirectoryName);
            ScalarRepoRegistry repoRegistry = new ScalarRepoRegistry(
                tracer,
                new PhysicalFileSystem(),
                repoRegistryLocation);

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
