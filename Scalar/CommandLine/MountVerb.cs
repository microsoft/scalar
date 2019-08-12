using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.NamedPipes;
using Scalar.Common.Tracing;
using Scalar.DiskLayoutUpgrades;
using System;
using System.IO;

namespace Scalar.CommandLine
{
    [Verb(MountVerb.MountVerbName, HelpText = "Mount a Scalar virtual repo")]
    public class MountVerb : ScalarVerb.ForExistingEnlistment
    {
        private const string MountVerbName = "mount";

        [Option(
            'v',
            ScalarConstants.VerbParameters.Mount.Verbosity,
            Default = ScalarConstants.VerbParameters.Mount.DefaultVerbosity,
            Required = false,
            HelpText = "Sets the verbosity of console logging. Accepts: Verbose, Informational, Warning, Error")]
        public string Verbosity { get; set; }

        [Option(
            'k',
            ScalarConstants.VerbParameters.Mount.Keywords,
            Default = ScalarConstants.VerbParameters.Mount.DefaultKeywords,
            Required = false,
            HelpText = "A CSV list of logging filter keywords. Accepts: Any, Network")]
        public string KeywordsCsv { get; set; }

        public bool SkipMountedCheck { get; set; }
        public bool SkipVersionCheck { get; set; }
        public CacheServerInfo ResolvedCacheServer { get; set; }
        public ServerScalarConfig DownloadedScalarConfig { get; set; }

        protected override string VerbName
        {
            get { return MountVerbName; }
        }

        public override void InitializeDefaultParameterValues()
        {
            this.Verbosity = ScalarConstants.VerbParameters.Mount.DefaultVerbosity;
            this.KeywordsCsv = ScalarConstants.VerbParameters.Mount.DefaultKeywords;
        }

        protected override void PreCreateEnlistment()
        {
            string errorMessage;
            string enlistmentRoot;
            if (!ScalarPlatform.Instance.TryGetScalarEnlistmentRoot(this.EnlistmentRootPathParameter, out enlistmentRoot, out errorMessage))
            {
                this.ReportErrorAndExit("Error: '{0}' is not a valid Scalar enlistment", this.EnlistmentRootPathParameter);
            }

            if (!this.SkipMountedCheck)
            {
                if (this.IsExistingPipeListening(enlistmentRoot))
                {
                    this.ReportErrorAndExit(tracer: null, exitCode: ReturnCode.Success, error: $"The repo at '{enlistmentRoot}' is already mounted.");
                }
            }

            if (!DiskLayoutUpgrade.TryRunAllUpgrades(enlistmentRoot))
            {
                this.ReportErrorAndExit("Failed to upgrade repo disk layout. " + ConsoleHelper.GetScalarLogMessage(enlistmentRoot));
            }

            string error;
            if (!DiskLayoutUpgrade.TryCheckDiskLayoutVersion(tracer: null, enlistmentRoot: enlistmentRoot, error: out error))
            {
                this.ReportErrorAndExit("Error: " + error);
            }
        }

        protected override void Execute(ScalarEnlistment enlistment)
        {
            string errorMessage = null;
            string mountExecutableLocation = null;
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, "ExecuteMount"))
            {
                PhysicalFileSystem fileSystem = new PhysicalFileSystem();
                GitRepo gitRepo = new GitRepo(tracer, enlistment, fileSystem);
                ScalarContext context = new ScalarContext(tracer, fileSystem, gitRepo, enlistment);

                if (!HooksInstaller.InstallHooks(context, out errorMessage))
                {
                    this.ReportErrorAndExit("Error installing hooks: " + errorMessage);
                }

                CacheServerInfo cacheServer = this.ResolvedCacheServer ?? CacheServerResolver.GetCacheServerFromConfig(enlistment);

                tracer.AddLogFileEventListener(
                    ScalarEnlistment.GetNewScalarLogFileName(enlistment.ScalarLogsRoot, ScalarConstants.LogFileTypes.MountVerb),
                    EventLevel.Verbose,
                    Keywords.Any);
                tracer.WriteStartEvent(
                    enlistment.EnlistmentRoot,
                    enlistment.RepoUrl,
                    cacheServer.Url,
                    new EventMetadata
                    {
                        { "Unattended", this.Unattended },
                        { "IsElevated", ScalarPlatform.Instance.IsElevated() },
                        { "NamedPipeName", enlistment.NamedPipeName },
                        { nameof(this.EnlistmentRootPathParameter), this.EnlistmentRootPathParameter },
                    });

                RetryConfig retryConfig = null;
                ServerScalarConfig serverScalarConfig = this.DownloadedScalarConfig;
                if (!this.SkipVersionCheck)
                {
                    string authErrorMessage;
                    if (!this.TryAuthenticate(tracer, enlistment, out authErrorMessage))
                    {
                        this.Output.WriteLine("    WARNING: " + authErrorMessage);
                        this.Output.WriteLine("    Mount will proceed, but new files cannot be accessed until Scalar can authenticate.");
                    }

                    if (serverScalarConfig == null)
                    {
                        if (retryConfig == null)
                        {
                            retryConfig = this.GetRetryConfig(tracer, enlistment);
                        }

                        serverScalarConfig = this.QueryScalarConfig(tracer, enlistment, retryConfig);
                    }

                    this.ValidateClientVersions(tracer, enlistment, serverScalarConfig, showWarnings: true);

                    CacheServerResolver cacheServerResolver = new CacheServerResolver(tracer, enlistment);
                    cacheServer = cacheServerResolver.ResolveNameFromRemote(cacheServer.Url, serverScalarConfig);
                    this.Output.WriteLine("Configured cache server: " + cacheServer);
                }

                this.InitializeLocalCacheAndObjectsPaths(tracer, enlistment, retryConfig, serverScalarConfig, cacheServer);

                if (!this.ShowStatusWhileRunning(
                    () => { return this.PerformPreMountValidation(tracer, enlistment, out mountExecutableLocation, out errorMessage); },
                    "Validating repo"))
                {
                    this.ReportErrorAndExit(tracer, errorMessage);
                }

                if (!this.SkipVersionCheck)
                {
                    string error;
                    if (!RepoMetadata.TryInitialize(tracer, enlistment.DotScalarRoot, out error))
                    {
                        this.ReportErrorAndExit(tracer, error);
                    }

                    try
                    {
                        GitProcess git = new GitProcess(enlistment);
                        this.LogEnlistmentInfoAndSetConfigValues(tracer, git, enlistment);
                    }
                    finally
                    {
                        RepoMetadata.Shutdown();
                    }
                }

                if (!this.ShowStatusWhileRunning(
                    () => { return this.TryMount(tracer, enlistment, mountExecutableLocation, out errorMessage); },
                    "Mounting"))
                {
                    this.ReportErrorAndExit(tracer, errorMessage);
                }

                if (!this.Unattended && ScalarPlatform.Instance.UnderConstruction.SupportsScalarService)
                {
                    tracer.RelatedInfo($"{nameof(this.Execute)}: Registering for automount");

                    if (this.ShowStatusWhileRunning(
                        () => { return this.RegisterMount(enlistment, out errorMessage); },
                        "Registering for automount"))
                    {
                        tracer.RelatedInfo($"{nameof(this.Execute)}: Registered for automount");
                    }
                    else
                    {
                        this.Output.WriteLine("    WARNING: " + errorMessage);
                        tracer.RelatedInfo($"{nameof(this.Execute)}: Failed to register for automount");
                    }
                }
            }
        }

        private bool PerformPreMountValidation(ITracer tracer, ScalarEnlistment enlistment, out string mountExecutableLocation, out string errorMessage)
        {
            errorMessage = string.Empty;
            mountExecutableLocation = string.Empty;

            // We have to parse these parameters here to make sure they are valid before
            // handing them to the background process which cannot tell the user when they are bad
            EventLevel verbosity;
            Keywords keywords;
            this.ParseEnumArgs(out verbosity, out keywords);

            mountExecutableLocation = Path.Combine(ProcessHelper.GetCurrentProcessLocation(), ScalarPlatform.Instance.Constants.MountExecutableName);
            if (!File.Exists(mountExecutableLocation))
            {
                errorMessage = $"Could not find {ScalarPlatform.Instance.Constants.MountExecutableName}. You may need to reinstall Scalar.";
                return false;
            }

            GitProcess git = new GitProcess(enlistment);
            if (!git.IsValidRepo())
            {
                errorMessage = "The .git folder is missing or has invalid contents";
                return false;
            }

            if (!ScalarPlatform.Instance.FileSystem.IsFileSystemSupported(enlistment.EnlistmentRoot, out string error))
            {
                errorMessage = $"FileSystem unsupported: {error}";
                return false;
            }

            return true;
        }

        private bool TryMount(ITracer tracer, ScalarEnlistment enlistment, string mountExecutableLocation, out string errorMessage)
        {
            if (!ScalarVerb.TrySetRequiredGitConfigSettings(enlistment))
            {
                errorMessage = "Unable to configure git repo";
                return false;
            }

            const string ParamPrefix = "--";

            tracer.RelatedInfo($"{nameof(this.TryMount)}: Launching background process('{mountExecutableLocation}') for {enlistment.EnlistmentRoot}");

            ScalarPlatform.Instance.StartBackgroundScalarProcess(
                tracer,
                mountExecutableLocation,
                new[]
                {
                    enlistment.EnlistmentRoot,
                    ParamPrefix + ScalarConstants.VerbParameters.Mount.Verbosity,
                    this.Verbosity,
                    ParamPrefix + ScalarConstants.VerbParameters.Mount.Keywords,
                    this.KeywordsCsv,
                    ParamPrefix + ScalarConstants.VerbParameters.Mount.StartedByService,
                    this.StartedByService.ToString(),
                    ParamPrefix + ScalarConstants.VerbParameters.Mount.StartedByVerb,
                    true.ToString()
                });

            tracer.RelatedInfo($"{nameof(this.TryMount)}: Waiting for repo to be mounted");
            return ScalarEnlistment.WaitUntilMounted(tracer, enlistment.EnlistmentRoot, this.Unattended, out errorMessage);
        }

        private bool RegisterMount(ScalarEnlistment enlistment, out string errorMessage)
        {
            errorMessage = string.Empty;

            NamedPipeMessages.RegisterRepoRequest request = new NamedPipeMessages.RegisterRepoRequest();
            request.EnlistmentRoot = enlistment.EnlistmentRoot;

            request.OwnerSID = ScalarPlatform.Instance.GetCurrentUser();

            using (NamedPipeClient client = new NamedPipeClient(this.ServicePipeName))
            {
                if (!client.Connect())
                {
                    errorMessage = "Unable to register repo because Scalar.Service is not responding.";
                    return false;
                }

                try
                {
                    client.SendRequest(request.ToMessage());
                    NamedPipeMessages.Message response = client.ReadResponse();
                    if (response.Header == NamedPipeMessages.RegisterRepoRequest.Response.Header)
                    {
                        NamedPipeMessages.RegisterRepoRequest.Response message = NamedPipeMessages.RegisterRepoRequest.Response.FromMessage(response);

                        if (!string.IsNullOrEmpty(message.ErrorMessage))
                        {
                            errorMessage = message.ErrorMessage;
                            return false;
                        }

                        if (message.State != NamedPipeMessages.CompletionState.Success)
                        {
                            errorMessage = "Unable to register repo. " + errorMessage;
                            return false;
                        }
                        else
                        {
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

        private void ParseEnumArgs(out EventLevel verbosity, out Keywords keywords)
        {
            if (!Enum.TryParse(this.KeywordsCsv, out keywords))
            {
                this.ReportErrorAndExit("Error: Invalid logging filter keywords: " + this.KeywordsCsv);
            }

            if (!Enum.TryParse(this.Verbosity, out verbosity))
            {
                this.ReportErrorAndExit("Error: Invalid logging verbosity: " + this.Verbosity);
            }
        }
    }
}
