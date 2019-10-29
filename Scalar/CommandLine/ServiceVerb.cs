using CommandLine;
using Scalar.Common;
using Scalar.Common.NamedPipes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scalar.CommandLine
{
    [Verb(ServiceVerbName, HelpText = "Runs commands for the Scalar service.")]
    public class ServiceVerb : ScalarVerb.ForNoEnlistment
    {
        private const string ServiceVerbName = "service";

        [Option(
            "list-registered",
            Default = false,
            Required = false,
            HelpText = "Prints a list of all repos registered with the service")]
        public bool List { get; set; }

        protected override string VerbName
        {
            get { return ServiceVerbName; }
        }

        public override void Execute()
        {
            int optionCount = new[] { this.List }.Count(flag => flag);
            if (optionCount == 0)
            {
                this.ReportErrorAndExit($"Error: You must specify an argument.  Run 'scalar {ServiceVerbName} --help' for details.");
            }
            else if (optionCount > 1)
            {
                this.ReportErrorAndExit($"Error: You cannot specify multiple arguments.  Run 'scalar {ServiceVerbName} --help' for details.");
            }

            string errorMessage;
            List<string> repoList;
            if (!this.TryGetRepoList(out repoList, out errorMessage))
            {
                this.ReportErrorAndExit("Error getting repo list: " + errorMessage);
            }

            if (this.List)
            {
                foreach (string repoRoot in repoList)
                {
                    this.Output.WriteLine(repoRoot);
                }
            }
        }

        private bool TryGetRepoList(out List<string> repoList, out string errorMessage)
        {
            repoList = null;
            errorMessage = string.Empty;

            NamedPipeMessages.GetActiveRepoListRequest request = new NamedPipeMessages.GetActiveRepoListRequest();

            using (NamedPipeClient client = new NamedPipeClient(this.ServicePipeName))
            {
                if (!client.Connect())
                {
                    errorMessage = "Scalar.Service is not responding.";
                    return false;
                }

                try
                {
                    client.SendRequest(request.ToMessage());
                    NamedPipeMessages.Message response = client.ReadResponse();
                    if (response.Header == NamedPipeMessages.GetActiveRepoListRequest.Response.Header)
                    {
                        NamedPipeMessages.GetActiveRepoListRequest.Response message = NamedPipeMessages.GetActiveRepoListRequest.Response.FromMessage(response);

                        if (!string.IsNullOrEmpty(message.ErrorMessage))
                        {
                            errorMessage = message.ErrorMessage;
                        }
                        else
                        {
                            if (message.State != NamedPipeMessages.CompletionState.Success)
                            {
                                errorMessage = "Unable to retrieve repo list.";
                            }
                            else
                            {
                                repoList = message.RepoList;
                                return true;
                            }
                        }
                    }
                    else
                    {
                        errorMessage = string.Format("Scalar.Service responded with unexpected message: {0}", response);
                    }
                }
                catch (BrokenPipeException e)
                {
                    errorMessage = "Unable to communicate with Scalar.Service: " + e.ToString();
                }

                return false;
            }
        }
    }
}
