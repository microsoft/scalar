using Scalar.Common;
using Scalar.Common.Tracing;
using System.Diagnostics;
using System.IO;

namespace Scalar.Service
{
    public class MacScalarMountProcess : IRepoMounter
    {
        private const string ExecutablePath = "/bin/launchctl";

        private MountLauncher processLauncher;
        private ITracer tracer;

        public MacScalarMountProcess(ITracer tracer, MountLauncher processLauncher = null)
        {
            this.tracer = tracer;
            this.processLauncher = processLauncher ?? new MountLauncher(tracer);
        }

        public bool MountRepository(string repoRoot, int sessionId)
        {
            string arguments = string.Format(
                "asuser {0} {1} mount {2}",
                sessionId,
                Path.Combine(ScalarPlatform.Instance.Constants.ScalarBinDirectoryPath, ScalarPlatform.Instance.Constants.ScalarExecutableName),
                repoRoot);

            if (!this.processLauncher.LaunchProcess(ExecutablePath, arguments, repoRoot))
            {
                this.tracer.RelatedError($"{nameof(this.MountRepository)}: Unable to start the Scalar process.");
                return false;
            }

            string errorMessage;
            if (!this.processLauncher.WaitUntilMounted(this.tracer, repoRoot, false, out errorMessage))
            {
                this.tracer.RelatedError(errorMessage);
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
                    metadata.Add("Area", "ScalarMountProcess");
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

            public virtual bool WaitUntilMounted(ITracer tracer, string enlistmentRoot, bool unattended, out string errorMessage)
            {
                return ScalarEnlistment.WaitUntilMounted(tracer, enlistmentRoot, unattended: false, errorMessage: out errorMessage);
            }
        }
    }
}
