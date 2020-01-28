using Scalar.Common.NamedPipes;
using Scalar.Common.Tracing;
using System;
using System.Diagnostics;
using System.IO;

namespace Scalar.Service.UI
{
    public class ScalarToastRequestHandler
    {
        private const string ScalarUpgradeTitleFormat = "New version {0} is available";
        private const string ScalarUpgradeMessage = "When ready, click Upgrade button to run upgrade.";
        private const string ScalarUpgradeButtonTitle = "Upgrade";

        private const string ScalarUpgradeActionPrefix = "scalar upgrade --confirm";

        private readonly ITracer tracer;
        private readonly IToastNotifier toastNotifier;

        public ScalarToastRequestHandler(IToastNotifier toastNotifier, ITracer tracer)
        {
            this.toastNotifier = toastNotifier;
            this.toastNotifier.UserResponseCallback = this.UserResponseCallback;
            this.tracer = tracer;
        }

        public void HandleToastRequest(ITracer tracer, NamedPipeMessages.Notification.Request request)
        {
            string title = null;
            string message = null;
            string buttonTitle = null;
            string args = null;

            switch (request.Id)
            {
                case NamedPipeMessages.Notification.Request.Identifier.UpgradeAvailable:
                    title = string.Format(ScalarUpgradeTitleFormat, request.NewVersion);
                    message = string.Format(ScalarUpgradeMessage);
                    buttonTitle = ScalarUpgradeButtonTitle;
                    args = $"{ScalarUpgradeActionPrefix}";
                    break;
            }

            if (title != null && message != null)
            {
                this.toastNotifier.Notify(title, message, buttonTitle, args);
            }
        }

        public void UserResponseCallback(string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                this.tracer.RelatedError($"{nameof(this.UserResponseCallback)}: Received null arguments in Toaster callback.");
                return;
            }

            using (ITracer activity = this.tracer.StartActivity("ScalarToastCallback", EventLevel.Informational))
            {
                string command = null;
                bool elevate = false;

                if (args.StartsWith(ScalarUpgradeActionPrefix))
                {
                    this.tracer.RelatedInfo($"scalar upgrade action.");
                    command = "scalar upgrade --confirm";
                    elevate = true;
                }
                else
                {
                    this.tracer.RelatedError($"{nameof(this.UserResponseCallback)}- Unknown action({args}) specified in Toaster callback.");
                }

                if (!string.IsNullOrEmpty(command))
                {
                    this.LaunchCommandInCommandPrompt(command, elevate, activity);
                }
            }
        }

        private bool TryValidatePath(string path, out string validatedPath, ITracer tracer)
        {
            try
            {
                validatedPath = Path.GetFullPath(path);
                return true;
            }
            catch (Exception ex)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add("Exception", ex.ToString());
                metadata.Add("Path", path);

                tracer.RelatedError(metadata, $"{nameof(this.TryValidatePath)}: {path}. {ex.ToString()}");
            }

            validatedPath = null;
            return false;
        }

        private void LaunchCommandInCommandPrompt(string fullCommand, bool elevate, ITracer tracer)
        {
            const string cmdPath = "CMD.exe";
            ProcessStartInfo processInfo = new ProcessStartInfo(cmdPath);
            processInfo.UseShellExecute = true;
            processInfo.RedirectStandardInput = false;
            processInfo.RedirectStandardOutput = false;
            processInfo.RedirectStandardError = false;
            processInfo.WindowStyle = ProcessWindowStyle.Normal;
            processInfo.CreateNoWindow = false;

            // /K option is so the user gets the time to read the output of the command and
            // manually close the cmd window after that.
            processInfo.Arguments = "/K " + fullCommand;
            if (elevate)
            {
                processInfo.Verb = "runas";
            }

            tracer.RelatedInfo($"{nameof(this.UserResponseCallback)}- Running {cmdPath} /K {fullCommand}");

            try
            {
                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add("Exception", ex.ToString());
                metadata.Add(nameof(fullCommand), fullCommand);
                metadata.Add(nameof(elevate), elevate);

                tracer.RelatedError(metadata, $"{nameof(this.LaunchCommandInCommandPrompt)}: Error launching {fullCommand}. {ex.ToString()}");
            }
        }
    }
}
