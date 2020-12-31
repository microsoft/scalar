using Scalar.Common;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Scalar.Upgrader
{
    public class InstallerPreRunChecker
    {
        private ITracer tracer;

        public InstallerPreRunChecker(ITracer tracer, string commandToRerun)
        {
            this.tracer = tracer;
            this.CommandToRerun = commandToRerun;
        }

        protected string CommandToRerun { private get; set; }

        public virtual bool TryRunPreUpgradeChecks(out string consoleError)
        {
            using (ITracer activity = this.tracer.StartActivity(nameof(this.TryRunPreUpgradeChecks), EventLevel.Informational))
            {
                if (this.IsUnattended())
                {
                    consoleError = $"{ScalarConstants.UpgradeVerbMessages.ScalarUpgrade} is not supported in unattended mode";
                    this.tracer.RelatedWarning($"{nameof(this.TryRunPreUpgradeChecks)}: {consoleError}");
                    return false;
                }

                if (!this.IsScalarUpgradeAllowed(out consoleError))
                {
                    return false;
                }

                activity.RelatedInfo($"Successfully finished pre upgrade checks. Okay to run {ScalarConstants.UpgradeVerbMessages.ScalarUpgrade}.");
            }

            consoleError = null;
            return true;
        }

        public virtual bool IsInstallationBlockedByRunningProcess(out string consoleError)
        {
            consoleError = null;

            // The retry loop below is to help account for the delay between calls to stop Scalar
            // processes and those processes exiting.
            this.tracer.RelatedInfo("Checking if Scalar or dependent processes are running.");
            int retryCount = 10;
            HashSet<string> processList = null;
            while (retryCount > 0)
            {
                if (!this.IsBlockingProcessRunning(out processList))
                {
                    break;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(250));
                retryCount--;
            }

            if (processList.Count > 0)
            {
                consoleError = string.Join(
                    Environment.NewLine,
                    "Blocking processes are running.",
                    $"Run {this.CommandToRerun} again after quitting these processes - " + string.Join(", ", processList.ToArray()));
                this.tracer.RelatedWarning($"{nameof(this.IsInstallationBlockedByRunningProcess)}: {consoleError}");
                return false;
            }

            return true;
        }

        protected virtual bool IsElevated()
        {
            return ScalarPlatform.Instance.IsElevated();
        }

        protected virtual bool IsScalarUpgradeSupported()
        {
            return true;
        }

        protected virtual bool IsUnattended()
        {
            return ScalarEnlistment.IsUnattended(this.tracer);
        }

        protected virtual bool IsBlockingProcessRunning(out HashSet<string> processes)
        {
            int currentProcessId = Process.GetCurrentProcess().Id;
            Process[] allProcesses = Process.GetProcesses();
            HashSet<string> matchingNames = new HashSet<string>();

            foreach (Process process in allProcesses)
            {
                if (process.Id == currentProcessId || !ScalarPlatform.Instance.Constants.UpgradeBlockingProcesses.Contains(process.ProcessName))
                {
                    continue;
                }

                matchingNames.Add(process.ProcessName + " pid:" + process.Id);
            }

            processes = matchingNames;
            return processes.Count > 0;
        }

        protected virtual bool TryRunScalarWithArgs(string args, out string consoleError)
        {
            string scalarDirectory = ProcessHelper.GetProgramLocation(ScalarPlatform.Instance.Constants.ProgramLocaterCommand, ScalarPlatform.Instance.Constants.ScalarExecutableName);
            if (!string.IsNullOrEmpty(scalarDirectory))
            {
                string scalarPath = Path.Combine(scalarDirectory, ScalarPlatform.Instance.Constants.ScalarExecutableName);

                ProcessResult processResult = ProcessHelper.Run(scalarPath, args);
                if (processResult.ExitCode == 0)
                {
                    consoleError = null;
                    return true;
                }
                else
                {
                    consoleError = string.IsNullOrEmpty(processResult.Errors) ? $"`scalar {args}` failed." : processResult.Errors;
                    return false;
                }
            }
            else
            {
                consoleError = $"Could not locate {ScalarPlatform.Instance.Constants.ScalarExecutableName}";
                return false;
            }
        }

        private bool IsScalarUpgradeAllowed(out string consoleError)
        {
            bool isConfirmed = string.Equals(this.CommandToRerun, ScalarConstants.UpgradeVerbMessages.ScalarUpgradeConfirm, StringComparison.OrdinalIgnoreCase);
            string adviceText = null;
            if (!this.IsElevated())
            {
                adviceText = isConfirmed ? $"Run {this.CommandToRerun} again from an elevated command prompt." : $"To install, run {ScalarConstants.UpgradeVerbMessages.ScalarUpgradeConfirm} from an elevated command prompt.";
                consoleError = string.Join(
                    Environment.NewLine,
                    "The installer needs to be run from an elevated command prompt.",
                    adviceText);
                this.tracer.RelatedWarning($"{nameof(this.IsScalarUpgradeAllowed)}: Upgrade is not installable. {consoleError}");
                return false;
            }

            if (!this.IsScalarUpgradeSupported())
            {
                consoleError = string.Join(
                    Environment.NewLine,
                    $"{ScalarConstants.UpgradeVerbMessages.ScalarUpgrade} is only supported after the \"Windows Projected File System\" optional feature has been enabled by a manual installation of Scalar, and only on versions of Windows that support this feature.",
                    "Check your team's documentation for how to upgrade.");
                this.tracer.RelatedWarning(metadata: null, message: $"{nameof(this.IsScalarUpgradeAllowed)}: Upgrade is not installable. {consoleError}", keywords: Keywords.Telemetry);
                return false;
            }

            consoleError = null;
            return true;
        }
    }
}
