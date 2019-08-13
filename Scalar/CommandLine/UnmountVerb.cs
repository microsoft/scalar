using CommandLine;
using Scalar.Common;
using Scalar.Common.NamedPipes;

namespace Scalar.CommandLine
{
    [Verb(UnmountVerb.UnmountVerbName, HelpText = "Unmount a Scalar virtual repo")]
    public class UnmountVerb : ScalarVerb
    {
        private const string UnmountVerbName = "unmount";

        [Value(
            0,
            Required = false,
            Default = "",
            MetaName = "Enlistment Root Path",
            HelpText = "Full or relative path to the Scalar enlistment root")]
        public override string EnlistmentRootPathParameter { get; set; }

        [Option(
            ScalarConstants.VerbParameters.Unmount.SkipLock,
            Default = false,
            Required = false,
            HelpText = "Force unmount even if the lock is not available.")]
        public bool SkipLock { get; set; }

        public bool SkipUnregister { get; set; }

        protected override string VerbName
        {
            get { return UnmountVerbName; }
        }

        public override void Execute()
        {
            this.ValidatePathParameter(this.EnlistmentRootPathParameter);

            string errorMessage;
            string root;
            if (!ScalarPlatform.Instance.TryGetScalarEnlistmentRoot(this.EnlistmentRootPathParameter, out root, out errorMessage))
            {
                this.ReportErrorAndExit(
                   "Error: '{0}' is not a valid Scalar enlistment",
                   this.EnlistmentRootPathParameter);
            }

            if (!this.ShowStatusWhileRunning(
                () => { return this.Unmount(root, out errorMessage); },
                "Unmounting"))
            {
                this.ReportErrorAndExit(errorMessage);
            }

            if (!this.Unattended && !this.SkipUnregister && ScalarPlatform.Instance.UnderConstruction.SupportsScalarService)
            {
                if (!this.ShowStatusWhileRunning(
                    () => { return this.UnregisterRepo(root, out errorMessage); },
                    "Unregistering automount"))
                {
                    this.Output.WriteLine("    WARNING: " + errorMessage);
                }
            }
        }

        private bool Unmount(string enlistmentRoot, out string errorMessage)
        {
            errorMessage = string.Empty;

            string pipeName = ScalarPlatform.Instance.GetNamedPipeName(enlistmentRoot);
            string rawGetStatusResponse = string.Empty;

            try
            {
                using (NamedPipeClient pipeClient = new NamedPipeClient(pipeName))
                {
                    if (!pipeClient.Connect())
                    {
                        errorMessage = "Unable to connect to Scalar.Mount";
                        return false;
                    }

                    pipeClient.SendRequest(NamedPipeMessages.GetStatus.Request);
                    rawGetStatusResponse = pipeClient.ReadRawResponse();
                    NamedPipeMessages.GetStatus.Response getStatusResponse =
                        NamedPipeMessages.GetStatus.Response.FromJson(rawGetStatusResponse);

                    switch (getStatusResponse.MountStatus)
                    {
                        case NamedPipeMessages.GetStatus.Mounting:
                            errorMessage = "Still mounting, please try again later";
                            return false;

                        case NamedPipeMessages.GetStatus.Unmounting:
                            errorMessage = "Already unmounting, please wait";
                            return false;

                        case NamedPipeMessages.GetStatus.Ready:
                            break;

                        case NamedPipeMessages.GetStatus.MountFailed:
                            break;

                        default:
                            errorMessage = "Unrecognized response to GetStatus: " + rawGetStatusResponse;
                            return false;
                    }

                    pipeClient.SendRequest(NamedPipeMessages.Unmount.Request);
                    string unmountResponse = pipeClient.ReadRawResponse();

                    switch (unmountResponse)
                    {
                        case NamedPipeMessages.Unmount.Acknowledged:
                            string finalResponse = pipeClient.ReadRawResponse();
                            if (finalResponse == NamedPipeMessages.Unmount.Completed)
                            {
                                errorMessage = string.Empty;
                                return true;
                            }
                            else
                            {
                                errorMessage = "Unrecognized final response to unmount: " + finalResponse;
                                return false;
                            }

                        case NamedPipeMessages.Unmount.NotMounted:
                            errorMessage = "Unable to unmount, repo was not mounted";
                            return false;

                        case NamedPipeMessages.Unmount.MountFailed:
                            errorMessage = "Unable to unmount, previous mount attempt failed";
                            return false;

                        default:
                            errorMessage = "Unrecognized response to unmount: " + unmountResponse;
                            return false;
                    }
                }
            }
            catch (BrokenPipeException e)
            {
                errorMessage = "Unable to communicate with Scalar: " + e.ToString();
                return false;
            }
        }

        private bool UnregisterRepo(string rootPath, out string errorMessage)
        {
            errorMessage = string.Empty;
            NamedPipeMessages.UnregisterRepoRequest request = new NamedPipeMessages.UnregisterRepoRequest();
            request.EnlistmentRoot = rootPath;

            using (NamedPipeClient client = new NamedPipeClient(this.ServicePipeName))
            {
                if (!client.Connect())
                {
                    errorMessage = "Unable to unregister repo because Scalar.Service is not responding. " + ScalarVerb.StartServiceInstructions;
                    return false;
                }

                try
                {
                    client.SendRequest(request.ToMessage());
                    NamedPipeMessages.Message response = client.ReadResponse();
                    if (response.Header == NamedPipeMessages.UnregisterRepoRequest.Response.Header)
                    {
                        NamedPipeMessages.UnregisterRepoRequest.Response message = NamedPipeMessages.UnregisterRepoRequest.Response.FromMessage(response);

                        if (message.State != NamedPipeMessages.CompletionState.Success)
                        {
                            errorMessage = message.ErrorMessage;
                            return false;
                        }
                        else
                        {
                            errorMessage = string.Empty;
                            return true;
                        }
                    }
                    else
                    {
                        errorMessage = string.Format("Scalar.Service responded with unexpected message: {0}", response);
                        return false;
                    }
                }
                catch (BrokenPipeException e)
                {
                    errorMessage = "Unable to communicate with Scalar.Service: " + e.ToString();
                    return false;
                }
            }
        }
    }
}
