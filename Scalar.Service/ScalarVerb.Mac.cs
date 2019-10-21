using Scalar.Common;
using Scalar.Common.Tracing;
using System.Diagnostics;
using System.IO;

namespace Scalar.Service
{
    public class ScalarVerb : IScalarVerb
    {
        private const string ExecutablePath = "/bin/launchctl";

        private MountLauncher processLauncher;
        private ITracer tracer;

        public ScalarVerb(ITracer tracer, MountLauncher processLauncher = null)
        {
            this.tracer = tracer;
            this.processLauncher = processLauncher ?? new MountLauncher(tracer);
        }

        public bool CallMaintenance(string task, string repoRoot, int sessionId)
        {
            // TODO: Here and in Windows only compute these once
            string scalarBinPath = Path.Combine(
                ScalarPlatform.Instance.Constants.ScalarBinDirectoryPath,
                ScalarPlatform.Instance.Constants.ScalarExecutableName);

            InternalVerbParameters internalParams = new InternalVerbParameters(startedByService: true);

            string arguments =
                $"asuser {sessionId} {scalarBinPath} maintenance \"{repoRoot}\" {task} --{ScalarConstants.VerbParameters.InternalUseOnly} {internalParams.ToJson()}";

            if (!this.processLauncher.LaunchProcess(ExecutablePath, arguments, repoRoot))
            {
                this.tracer.RelatedError($"{nameof(this.CallMaintenance)}: Unable to start the Scalar process.");
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

            public virtual bool LaunchProcess(string executablePath, string arguments, string workingDirectory)
            {
                ProcessStartInfo processInfo = new ProcessStartInfo(executablePath);
                processInfo.Arguments = arguments;
                processInfo.WindowStyle = ProcessWindowStyle.Hidden;
                processInfo.WorkingDirectory = workingDirectory;
                processInfo.UseShellExecute = false;
                processInfo.RedirectStandardOutput = true;

                ProcessResult result = ProcessHelper.Run(processInfo);
                if (result.ExitCode != 0)
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", nameof(ScalarVerb));
                    metadata.Add(nameof(executablePath), executablePath);
                    metadata.Add(nameof(arguments), arguments);
                    metadata.Add(nameof(workingDirectory), workingDirectory);
                    metadata.Add(nameof(result.ExitCode), result.ExitCode);
                    metadata.Add(nameof(result.Errors), result.Errors);

                    this.tracer.RelatedError(metadata, $"{nameof(this.LaunchProcess)} ERROR: Could not launch {executablePath}");
                    return false;
                }

                return true;
            }
        }
    }
}
