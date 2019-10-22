using Scalar.Common;
using Scalar.Common.Tracing;
using System.Diagnostics;
using System.IO;

namespace Scalar.Service
{
    public class ScalarVerbRunner : IScalarVerbRunner
    {
        private const string ExecutablePath = "/bin/launchctl";

        private readonly string scalarBinPath;
        private readonly string internalVerbJson;

        private MountLauncher processLauncher;
        private ITracer tracer;

        public ScalarVerbRunner(ITracer tracer, MountLauncher processLauncher = null)
        {
            this.tracer = tracer;
            this.processLauncher = processLauncher ?? new MountLauncher(tracer);

            this.scalarBinPath = Path.Combine(
                ScalarPlatform.Instance.Constants.ScalarBinDirectoryPath,
                ScalarPlatform.Instance.Constants.ScalarExecutableName);

            InternalVerbParameters internalParams = new InternalVerbParameters(startedByService: true);
            this.internalVerbJson = internalParams.ToJson();
        }

        public bool CallMaintenance(string task, string repoRoot, int sessionId)
        {
            string arguments =
                $"asuser {sessionId} {this.scalarBinPath} maintenance \"{repoRoot}\" {task} --{ScalarConstants.VerbParameters.InternalUseOnly} {this.internalVerbJson}";

            ProcessResult result = this.processLauncher.LaunchProcess(ExecutablePath, arguments, repoRoot);
            if (result.ExitCode != 0)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add("Area", nameof(ScalarVerbRunner));
                metadata.Add(nameof(ExecutablePath), ExecutablePath);
                metadata.Add(nameof(arguments), arguments);
                metadata.Add(nameof(repoRoot), repoRoot);
                metadata.Add(nameof(result.ExitCode), result.ExitCode);
                metadata.Add(nameof(result.Errors), result.Errors);

                this.tracer.RelatedError(metadata, $"{nameof(this.CallMaintenance)}: Maintenance verb failed");
                return false;
            }

            return true;
        }

        public class MountLauncher
        {
            private ITracer tracer;

            public MountLauncher(ITracer tracer)
            {
                this.tracer = tracer;
            }

            public virtual ProcessResult LaunchProcess(string executablePath, string arguments, string workingDirectory)
            {
                ProcessStartInfo processInfo = new ProcessStartInfo(executablePath);
                processInfo.Arguments = arguments;
                processInfo.WindowStyle = ProcessWindowStyle.Hidden;
                processInfo.WorkingDirectory = workingDirectory;
                processInfo.UseShellExecute = false;
                processInfo.RedirectStandardOutput = true;

                return ProcessHelper.Run(processInfo);
            }
        }
    }
}
