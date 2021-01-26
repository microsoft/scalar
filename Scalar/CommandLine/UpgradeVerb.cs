using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Scalar.CommandLine
{
    [Verb(UpgradeVerbName, HelpText = "Checks for new Scalar release, downloads and installs it when available.")]
    public class UpgradeVerb : ScalarVerb.ForNoEnlistment
    {
        private const string UpgradeVerbName = "upgrade";
        private const string DryRunOption = "--dry-run";
        private const string NoVerifyOption = "--no-verify";
        private const string ConfirmOption = "--confirm";

        private ITracer tracer;
        private PhysicalFileSystem fileSystem;
        private ProcessLauncher processLauncher;

        public UpgradeVerb(
            ITracer tracer,
            PhysicalFileSystem fileSystem,
            ProcessLauncher processWrapper,
            TextWriter output)
        {
            this.tracer = tracer;
            this.fileSystem = fileSystem;
            this.processLauncher = processWrapper;
            this.Output = output;
        }

        public UpgradeVerb()
        {
            this.fileSystem = new PhysicalFileSystem();
            this.processLauncher = new ProcessLauncher();
            this.Output = Console.Out;
        }

        [Option(
            "confirm",
            Default = false,
            Required = false,
            HelpText = "Pass in this flag to actually install the newest release")]
        public bool Confirmed { get; set; }

        [Option(
            "dry-run",
            Default = false,
            Required = false,
            HelpText = "Display progress and errors, but don't install Scalar")]
        public bool DryRun { get; set; }

        [Option(
            "no-verify",
            Default = false,
            Required = false,
            HelpText = "This parameter is reserved for internal use.")]
        public bool NoVerify { get; set; }

        protected override string VerbName
        {
            get { return UpgradeVerbName; }
        }

        public override void Execute()
        {
            this.TryRunProductUpgrade();
        }

        private bool TryGetBrewOutput(string args, out string output, out string error)
        {
            this.Output.WriteLine($"Running 'brew {args}'");
            var launcher = new ProcessLauncher();
            bool result = launcher.TryStart("brew", args, useShellExecute: false, out Exception ex);

            if (!result)
            {
                this.tracer.RelatedEvent(EventLevel.Warning, $"Failure during 'brew {args}'", this.CreateEventMetadata(ex));
                output = null;
                error = "Failed to start 'brew' process";
                return false;
            }

            output = launcher.Process.StandardOutput.ReadToEnd().Trim();
            error = launcher.Process.StandardError.ReadToEnd().Trim();
            launcher.WaitForExit();
            return true;
        }

        private bool TryUpgradeWithBrew(out string error)
        {
            string output;
            string stderr;
            if (!this.TryGetBrewOutput("cask list", out output, out stderr))
            {
                error = $"Failed to check 'brew' casks: '{stderr}' Is brew installed?";
                return false;
            }

            string packageName = string.Empty;

            if (output.IndexOf(ScalarConstants.HomebrewCasks.Scalar + "\n") >= 0)
            {
                packageName = ScalarConstants.HomebrewCasks.Scalar;
            }
            else if (output.IndexOf(ScalarConstants.HomebrewCasks.ScalarWithGVFS + "\n") >= 0)
            {
                packageName = ScalarConstants.HomebrewCasks.ScalarWithGVFS;
            }
            else
            {
                error = $"Scalar does not appear to be installed with 'brew': {stderr}";
                return false;
            }

            this.Output.WriteLine($"Found brew package '{packageName}'");

            if (!this.TryGetBrewOutput("update", out output, out stderr))
            {
                error = "An error occurred while updating 'brew' packages";
                return false;
            }

            if (!this.TryGetBrewOutput($"upgrade --cask {packageName}", out output, out stderr))
            {
                error = $"An error occurred while updating the {packageName} package: {stderr}";
                return false;
            }

            error = null;
            return true;
        }

        private bool TryRunProductUpgrade()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (!this.TryUpgradeWithBrew(out string error)) {
                    this.tracer.RelatedError(error);
                    return false;
                }
                return true;
            }

            this.ReportInfoToConsole("'scalar upgrade' is not implemented on this platform");
            return false;
        }

        private void ReportInfoToConsole(string message, params object[] args)
        {
            this.Output.WriteLine(message, args);
        }

        public class ProcessLauncher
        {
            public ProcessLauncher()
            {
                this.Process = new Process();
            }

            public Process Process { get; private set; }

            public virtual bool HasExited
            {
                get { return this.Process.HasExited; }
            }

            public virtual int ExitCode
            {
                get { return this.Process.ExitCode; }
            }

            public virtual void WaitForExit()
            {
                this.Process.WaitForExit();
            }

            public virtual bool TryStart(string path, string args, bool useShellExecute, out Exception exception)
            {
                this.Process.StartInfo = new ProcessStartInfo(path)
                {
                    UseShellExecute = useShellExecute,
                    WorkingDirectory = Environment.SystemDirectory,
                    WindowStyle = ProcessWindowStyle.Normal,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Arguments = args
                };

                exception = null;

                try
                {
                    return this.Process.Start();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                return false;
            }
        }
    }
}
