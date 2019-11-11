using Scalar.Common;
using Scalar.Common.Maintenance;
using Scalar.Common.Tracing;
using System.Diagnostics;
using System.IO;

namespace Scalar.Service
{
    public class MacScalarVerbRunner : IScalarVerbRunner
    {
        private readonly string scalarBinPath;
        private readonly string internalVerbJson;

        private ScalarProcessLauncher processLauncher;
        private ITracer tracer;

        public MacScalarVerbRunner(ITracer tracer, ScalarProcessLauncher processLauncher = null)
        {
            this.tracer = tracer;
            this.processLauncher = processLauncher ?? new ScalarProcessLauncher(tracer);

            this.scalarBinPath = Path.Combine(
                ScalarPlatform.Instance.Constants.ScalarBinDirectoryPath,
                ScalarPlatform.Instance.Constants.ScalarExecutableName);

            InternalVerbParameters internalParams = new InternalVerbParameters(startedByService: true);
            this.internalVerbJson = internalParams.ToJson();
        }

        /// <summary>
        /// Calls the 'scalar maintenance' verb
        /// </summary>
        /// <param name="task">Maintenance task to run</param>
        /// <param name="repoRoot">Repo to maintain</param>
        /// <param name="sessionId">Ignored</param>
        /// <returns>
        /// true if the maintenance verb succeeded, and false otherwise
        /// </returns>
        /// <remarks>
        /// 'CallMaintenance' should only be called for repos that are owned by
        /// the owner of the current process.
        ///
        /// 'launchctl asuser' *could* be used to launch has an arbitrary user,
        /// however, it is not used because it does not pass back the output/errors
        /// of the maintenance verb correctly.
        ///
        /// On Mac this method:
        /// 
        ///   - Is only called by Scalar.Service
        ///   - Is only called for repos owned by the same user that's running Scalar.Service
        ///   
        /// And so there is no need to use 'launchctl'.
        /// </remarks>
        public bool CallMaintenance(MaintenanceTasks.Task task, string repoRoot, int sessionId)
        {
            string taskVerbName = MaintenanceTasks.GetVerbTaskName(task);
            string arguments =
                $"maintenance \"{repoRoot}\" --{ScalarConstants.VerbParameters.Maintenance.Task} {taskVerbName} --{ScalarConstants.VerbParameters.InternalUseOnly} {this.internalVerbJson}";

            ProcessResult result = this.processLauncher.LaunchProcess(this.scalarBinPath, arguments, repoRoot);
            if (result.ExitCode != 0)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add("Area", "ScalarVerbRunner");
                metadata.Add(nameof(this.scalarBinPath), this.scalarBinPath);
                metadata.Add(nameof(arguments), arguments);
                metadata.Add(nameof(repoRoot), repoRoot);
                metadata.Add(nameof(result.ExitCode), result.ExitCode);
                metadata.Add(nameof(result.Output), result.Output);
                metadata.Add(nameof(result.Errors), result.Errors);

                this.tracer.RelatedError(metadata, $"{nameof(this.CallMaintenance)}: Maintenance verb failed");
                return false;
            }

            return true;
        }

        public class ScalarProcessLauncher
        {
            private ITracer tracer;

            public ScalarProcessLauncher(ITracer tracer)
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
