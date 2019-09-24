using Scalar.Common.NamedPipes;
using Scalar.Common.Tracing;
using System;
using System.Diagnostics;
using System.IO;

namespace Scalar.Service.UI
{
    public class ScalarToastRequestHandler
    {
        private const string ScalarAutomountStartTitle= "Scalar Automount";
        private const string ScalarAutomountStartMessageFormat = "Attempting to mount {0} Scalar {1}";
        private const string ScalarMultipleRepos = "repos";
        private const string ScalarSingleRepo = "repo";

        private const string ScalarAutomountSuccessTitle = "Scalar Automount";
        private const string ScalarAutomountSuccessMessageFormat = "The following Scalar repo is now mounted: {0}{1}";

        private const string ScalarAutomountErrorTitle = "Scalar Automount";
        private const string ScalarAutomountErrorMessageFormat = "The following Scalar repo failed to mount: {0}{1}";
        private const string ScalarAutomountButtonTitle = "Retry";

        private const string ScalarUpgradeTitleFormat = "New version {0} is available";
        private const string ScalarUpgradeMessage = "Upgrade will unmount and remount scalar repos, ensure you are at a stopping point. When ready, click Upgrade button to run upgrade.";
        private const string ScalarUpgradeButtonTitle = "Upgrade";

        private const string ScalarRemountActionPrefix = "scalar mount";
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
            string path = null;

            switch (request.Id)
            {
                case NamedPipeMessages.Notification.Request.Identifier.AutomountStart:
                    string reposSuffix = request.EnlistmentCount <= 1 ? ScalarSingleRepo : ScalarMultipleRepos;
                    title = ScalarAutomountStartTitle;
                    message = string.Format(ScalarAutomountStartMessageFormat, request.EnlistmentCount, reposSuffix);
                    break;

                case NamedPipeMessages.Notification.Request.Identifier.MountSuccess:
                    if (this.TryValidatePath(request.Enlistment, out path, this.tracer))
                    {
                        title = ScalarAutomountSuccessTitle;
                        message = string.Format(ScalarAutomountSuccessMessageFormat, Environment.NewLine, path);
                    }

                    break;

                case NamedPipeMessages.Notification.Request.Identifier.MountFailure:
                    if (this.TryValidatePath(request.Enlistment, out path, this.tracer))
                    {
                        title = ScalarAutomountErrorTitle;
                        message = string.Format(ScalarAutomountErrorMessageFormat, Environment.NewLine, path);
                        buttonTitle = ScalarAutomountButtonTitle;
                        args = $"{ScalarRemountActionPrefix} {path}";
                    }

                    break;

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

            using (ITracer activity = this.tracer.StartActivity("GVFSToastCallback", EventLevel.Informational))
            {
                string gvfsCmd = null;
                bool elevate = false;

                if (args.StartsWith(ScalarUpgradeActionPrefix))
                {
                    this.tracer.RelatedInfo($"scalar upgrade action.");
                    gvfsCmd = "scalar upgrade --confirm";
                    elevate = true;
                }
                else if (args.StartsWith(ScalarRemountActionPrefix))
                {
                    string path = args.Substring(ScalarRemountActionPrefix.Length, args.Length - ScalarRemountActionPrefix.Length);
                    if (this.TryValidatePath(path, out string enlistment, activity))
                    {
                        this.tracer.RelatedInfo($"scalar mount action {enlistment}.");
                        gvfsCmd = $"scalar mount \"{enlistment}\"";
                    }
                    else
                    {
                        EventMetadata metadata = new EventMetadata();
                        metadata.Add(nameof(args), args);
                        metadata.Add(nameof(path), path);
                        this.tracer.RelatedError(metadata, $"{nameof(this.UserResponseCallback)}- Invalid enlistment path specified in Toaster callback.");
                    }
                }
                else
                {
                    this.tracer.RelatedError($"{nameof(this.UserResponseCallback)}- Unknown action({args}) specified in Toaster callback.");
                }

                if (!string.IsNullOrEmpty(gvfsCmd))
                {
                    this.launchGVFSInCommandPrompt(gvfsCmd, elevate, activity);
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

        private void launchGVFSInCommandPrompt(string fullGvfsCmd, bool elevate, ITracer tracer)
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
            processInfo.Arguments = "/K " + fullGvfsCmd;
            if (elevate)
            {
                processInfo.Verb = "runas";
            }

            tracer.RelatedInfo($"{nameof(this.UserResponseCallback)}- Running {cmdPath} /K {fullGvfsCmd}");

            try
            {
                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add("Exception", ex.ToString());
                metadata.Add(nameof(fullGvfsCmd), fullGvfsCmd);
                metadata.Add(nameof(elevate), elevate);

                tracer.RelatedError(metadata, $"{nameof(this.launchGVFSInCommandPrompt)}: Error launching {fullGvfsCmd}. {ex.ToString()}");
            }
        }
    }
}
