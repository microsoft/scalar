using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.Tracing;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Scalar.CommandLine
{
    [Verb(CloneVerb.CloneVerbName, HelpText = "Clone a git repo and register it with the service as a Scalar repo")]
    public class CloneVerb : ScalarVerb
    {
        private const string CloneVerbName = "clone";

        private JsonTracer tracer;
        private ScalarEnlistment enlistment;
        private CacheServerResolver cacheServerResolver;
        private CacheServerInfo cacheServer;
        private ServerScalarConfig serverScalarConfig;
        private RetryConfig retryConfig;
        private GitObjectsHttpRequestor objectRequestor;
        private GitProcess git;
        private GitRefs refs;
        private ScalarContext context;
        private PhysicalFileSystem fileSystem = new PhysicalFileSystem();

        [Value(
                0,
                Required = true,
                MetaName = "Repository URL",
                HelpText = "The url of the repo")]
        public string RepositoryURL { get; set; }

        [Value(
            1,
            Required = false,
            Default = "",
            MetaName = "Enlistment Root Path",
            HelpText = "Full or relative path to the Scalar enlistment root")]
        public override string EnlistmentRootPathParameter { get; set; }

        [Option(
            "cache-server-url",
            Required = false,
            Default = null,
            HelpText = "The url or friendly name of the cache server")]
        public string CacheServerUrl { get; set; }

        [Option(
            'b',
            "branch",
            Required = false,
            HelpText = "Branch to checkout after clone")]
        public string Branch { get; set; }

        [Option(
            "single-branch",
            Required = false,
            Default = false,
            HelpText = "Use this option to only download metadata for the branch that will be checked out")]
        public bool SingleBranch { get; set; }

        [Option(
            "no-fetch-commits-and-trees",
            Required = false,
            Default = false,
            HelpText = "Use this option to skip fetching commits and trees after clone")]
        public bool NoFetchCommitsAndTrees { get; set; }

        // By default this is "Drive\.scalarCache"
        [Option(
            "local-cache-path",
            Required = false,
            HelpText = "Use this option to override the path for the local Scalar cache.")]
        public string LocalCacheRoot { get; set; }

        [Option(
            "full-clone",
            Required = false,
            Default = false,
            HelpText = "When cloning, create full working directory.")]
        public bool FullClone { get; set; }

        protected override string VerbName
        {
            get { return CloneVerbName; }
        }

        public override void Execute()
        {
            int exitCode = 0;

            this.ValidatePathParameter(this.EnlistmentRootPathParameter);
            this.ValidatePathParameter(this.LocalCacheRoot);

            string fullEnlistmentRootPathParameter;
            string normalizedEnlistmentRootPath = this.GetCloneRoot(out fullEnlistmentRootPathParameter);

            if (!string.IsNullOrWhiteSpace(this.LocalCacheRoot))
            {
                string fullLocalCacheRootPath = Path.GetFullPath(this.LocalCacheRoot);

                string errorMessage;
                string normalizedLocalCacheRootPath;
                if (!ScalarPlatform.Instance.FileSystem.TryGetNormalizedPath(fullLocalCacheRootPath, out normalizedLocalCacheRootPath, out errorMessage))
                {
                    this.ReportErrorAndExit($"Failed to determine normalized path for '--local-cache-path' path {fullLocalCacheRootPath}: {errorMessage}");
                }

                if (normalizedLocalCacheRootPath.StartsWith(
                    Path.Combine(normalizedEnlistmentRootPath, ScalarConstants.WorkingDirectoryRootName),
                    StringComparison.OrdinalIgnoreCase))
                {
                    this.ReportErrorAndExit("'--local-cache-path' cannot be inside the src folder");
                }
            }

            this.CheckNotInsideExistingRepo(normalizedEnlistmentRootPath);
            this.BlockEmptyCacheServerUrl(this.CacheServerUrl);

            Result cloneResult = new Result(false);

            using (this.tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, "ScalarClone"))
            {
                try
                {
                    cloneResult = this.DoClone(fullEnlistmentRootPathParameter, normalizedEnlistmentRootPath);
                }
                catch (AggregateException e)
                {
                    this.Output.WriteLine("Cannot clone @ {0}:", fullEnlistmentRootPathParameter);
                    foreach (Exception ex in e.Flatten().InnerExceptions)
                    {
                        this.Output.WriteLine("Exception: {0}", ex.ToString());
                    }

                    cloneResult = new Result(false);
                }
                catch (Exception e) when (!(e is VerbAbortedException))
                {
                    this.ReportErrorAndExit("Cannot clone @ {0}: {1}", fullEnlistmentRootPathParameter, e.ToString());
                }

                if (!cloneResult.Success)
                {
                    this.tracer.RelatedError(cloneResult.ErrorMessage);
                    this.Output.WriteLine();
                    this.Output.WriteLine("Cannot clone @ {0}", fullEnlistmentRootPathParameter);
                    this.Output.WriteLine("Error: {0}", cloneResult.ErrorMessage);
                    exitCode = (int)ReturnCode.GenericError;
                }
            }

            Environment.Exit(exitCode);
        }

        private Result DoClone(string fullEnlistmentRootPathParameter, string normalizedEnlistmentRootPath)
        {
            Result cloneResult = null;
            cloneResult = this.TryCreateEnlistment(fullEnlistmentRootPathParameter, normalizedEnlistmentRootPath, out this.enlistment);

            if (!cloneResult.Success)
            {
                this.tracer.RelatedError($"Error while creating enlistment: {cloneResult.ErrorMessage}");
                return cloneResult;
            }

            this.tracer.AddLogFileEventListener(
                ScalarEnlistment.GetNewScalarLogFileName(this.enlistment.ScalarLogsRoot, ScalarConstants.LogFileTypes.Clone),
                EventLevel.Informational,
                Keywords.Any);
            this.tracer.WriteStartEvent(
                this.enlistment.EnlistmentRoot,
                this.enlistment.RepoUrl,
                this.CacheServerUrl,
                this.AddVerbDataToMetadata(new EventMetadata
                {
                    { nameof(this.Branch), this.Branch },
                    { nameof(this.LocalCacheRoot), this.LocalCacheRoot },
                    { nameof(this.SingleBranch), this.SingleBranch },
                    { nameof(this.FullClone), this.FullClone },
                    { nameof(this.NoFetchCommitsAndTrees), this.NoFetchCommitsAndTrees },
                    { nameof(this.Unattended), this.Unattended },
                    { nameof(ScalarPlatform.Instance.IsElevated), ScalarPlatform.Instance.IsElevated() },
                    { "ProcessID", Process.GetCurrentProcess().Id },
                    { nameof(this.EnlistmentRootPathParameter), this.EnlistmentRootPathParameter },
                    { nameof(fullEnlistmentRootPathParameter), fullEnlistmentRootPathParameter },
                }));

            this.cacheServerResolver = new CacheServerResolver(this.tracer, this.enlistment);
            this.cacheServer = this.cacheServerResolver.ParseUrlOrFriendlyName(this.CacheServerUrl);

            string resolvedLocalCacheRoot;
            if (string.IsNullOrWhiteSpace(this.LocalCacheRoot))
            {
                if (!LocalCacheResolver.TryGetDefaultLocalCacheRoot(this.enlistment, out resolvedLocalCacheRoot, out string localCacheRootError))
                {
                    this.ReportErrorAndExit(
                        this.tracer,
                        $"Failed to determine the default location for the local Scalar cache: `{localCacheRootError}`");
                }
            }
            else
            {
                resolvedLocalCacheRoot = Path.GetFullPath(this.LocalCacheRoot);
            }

            this.Output.WriteLine("Clone parameters:");
            this.Output.WriteLine("  Repo URL:     " + this.enlistment.RepoUrl);
            this.Output.WriteLine("  Branch:       " + (string.IsNullOrWhiteSpace(this.Branch) ? "Default" : this.Branch));
            this.Output.WriteLine("  Cache Server: " + this.cacheServer);
            this.Output.WriteLine("  Local Cache:  " + resolvedLocalCacheRoot);
            this.Output.WriteLine("  Destination:  " + this.enlistment.EnlistmentRoot);
            this.Output.WriteLine("  FullClone:     " + this.FullClone);

            string authErrorMessage;
            if (!this.TryAuthenticate(this.tracer, this.enlistment, out authErrorMessage))
            {
                this.ReportErrorAndExit(this.tracer, "Cannot clone because authentication failed: " + authErrorMessage);
            }

            this.retryConfig = this.GetRetryConfig(this.tracer, this.enlistment, TimeSpan.FromMinutes(RetryConfig.FetchAndCloneTimeoutMinutes));
            this.serverScalarConfig = this.QueryScalarConfig(this.tracer, this.enlistment, this.retryConfig);

            this.cacheServer = this.ResolveCacheServer(this.tracer, this.cacheServer, this.cacheServerResolver, this.serverScalarConfig);

            this.ValidateClientVersions(this.tracer, this.enlistment, this.serverScalarConfig, showWarnings: true);

            using (this.objectRequestor = new GitObjectsHttpRequestor(this.tracer, this.enlistment, this.cacheServer, this.retryConfig))
            {
                cloneResult = this.CreateScalarDirctories(resolvedLocalCacheRoot);

                if (!cloneResult.Success)
                {
                    this.tracer.RelatedError(cloneResult.ErrorMessage);
                    return cloneResult;
                }

                this.ShowStatusWhileRunning(
                () =>
                {
                    cloneResult = this.CreateClone();
                    return cloneResult.Success;
                },
                "Cloning");

                if (!cloneResult.Success)
                {
                    this.tracer.RelatedError(cloneResult.ErrorMessage);
                    return cloneResult;
                }

                if (!this.NoFetchCommitsAndTrees)
                {
                    ReturnCode result = this.Execute<MaintenanceVerb>(
                        this.enlistment,
                        verb =>
                        {
                            verb.MaintenanceTask = ScalarConstants.VerbParameters.Maintenance.FetchCommitsAndTreesTaskName;
                            verb.SkipVersionCheck = true;
                            verb.ResolvedCacheServer = this.cacheServer;
                            verb.ServerScalarConfig = this.serverScalarConfig;
                        });

                    if (result != ReturnCode.Success)
                    {
                        this.Output.WriteLine("\r\nError while fetching commits and trees @ {0}", fullEnlistmentRootPathParameter);
                        return cloneResult;
                    }
                }

                this.ConfigureWatchmanIntegration();

                cloneResult = this.CheckoutRepo();
            }

            if (cloneResult.Success)
            {
                cloneResult = this.TryRegisterRepo();
            }

            return cloneResult;
        }

        private void ConfigureWatchmanIntegration()
        {
            string watchmanLocation = ProcessHelper.GetProgramLocation(ScalarPlatform.Instance.Constants.ProgramLocaterCommand, "watchman");
            this.Output.Write("Configuring Watchman...");
            if (string.IsNullOrEmpty(watchmanLocation))
            {
                this.Output.WriteLine("Skipping: Watchman not installed.");
                this.tracer.RelatedWarning("Watchman is not installed - skipping Watchman configuration.");
                return;
            }

            try
            {
                string fsMonitorWatchmanSampleHookPath = Path.Combine(
                    this.enlistment.WorkingDirectoryBackingRoot,
                    ScalarConstants.DotGit.Hooks.FsMonitorWatchmanSamplePath);

                string queryWatchmanPath = Path.Combine(
                    this.enlistment.WorkingDirectoryBackingRoot,
                    ScalarConstants.DotGit.Hooks.QueryWatchmanPath);

                this.fileSystem.CopyFile(
                    fsMonitorWatchmanSampleHookPath,
                    queryWatchmanPath,
                    overwrite: false);

                this.git.SetInLocalConfig("core.fsmonitor", ".git/hooks/query-watchman");

                // Complete the Configuring Watchman progress line...
                this.Output.WriteLine("Succeeded.");
                this.tracer.RelatedWarning("Watchman configured!");
            }
            catch (IOException ex)
            {
                this.Output.WriteLine("Failed: Check clone logs for details.");
                EventMetadata metadata = this.CreateEventMetadata(ex);
                this.tracer.RelatedError(metadata, $"Failed to configure Watchman integration: {ex.Message}");
            }
        }

        private Result TryCreateEnlistment(
            string fullEnlistmentRootPathParameter,
            string normalizedEnlistementRootPath,
            out ScalarEnlistment enlistment)
        {
            enlistment = null;

            // Check that EnlistmentRootPath is empty before creating a tracer and LogFileEventListener as
            // LogFileEventListener will create a file in EnlistmentRootPath
            if (Directory.Exists(normalizedEnlistementRootPath) && Directory.EnumerateFileSystemEntries(normalizedEnlistementRootPath).Any())
            {
                if (fullEnlistmentRootPathParameter.Equals(normalizedEnlistementRootPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new Result($"Clone directory '{fullEnlistmentRootPathParameter}' exists and is not empty");
                }

                return new Result($"Clone directory '{fullEnlistmentRootPathParameter}' ['{normalizedEnlistementRootPath}'] exists and is not empty");
            }

            string gitBinPath = ScalarPlatform.Instance.GitInstallation.GetInstalledGitBinPath();
            if (string.IsNullOrWhiteSpace(gitBinPath))
            {
                return new Result(ScalarConstants.GitIsNotInstalledError);
            }

            try
            {
                enlistment = new ScalarEnlistment(
                    normalizedEnlistementRootPath,
                    this.RepositoryURL,
                    gitBinPath,
                    authentication: null);
            }
            catch (InvalidRepoException e)
            {
                return new Result($"Error when creating a new Scalar enlistment at '{normalizedEnlistementRootPath}'. {e.Message}");
            }

            return new Result(true);
        }

        private Result CreateScalarDirctories(string resolvedLocalCacheRoot)
        {
            this.refs = this.objectRequestor.QueryInfoRefs(this.SingleBranch ? this.Branch : null);

            if (this.refs == null)
            {
                return new Result("Could not query info/refs from: " + Uri.EscapeUriString(this.enlistment.RepoUrl));
            }

            if (this.Branch == null)
            {
                this.Branch = this.refs.GetDefaultBranch();

                EventMetadata metadata = this.CreateEventMetadata();
                metadata.Add("Branch", this.Branch);
                this.tracer.RelatedEvent(EventLevel.Informational, "CloneDefaultRemoteBranch", metadata);
            }
            else
            {
                if (!this.refs.HasBranch(this.Branch))
                {
                    EventMetadata metadata = this.CreateEventMetadata();
                    metadata.Add("Branch", this.Branch);
                    this.tracer.RelatedEvent(EventLevel.Warning, "CloneBranchDoesNotExist", metadata);

                    string errorMessage = string.Format("Remote branch {0} not found in upstream origin", this.Branch);
                    return new Result(errorMessage);
                }
            }

            if (!this.enlistment.TryCreateEnlistmentFolders())
            {
                string error = "Could not create enlistment directory";
                this.tracer.RelatedError(error);
                return new Result(error);
            }

            if (!ScalarPlatform.Instance.FileSystem.IsFileSystemSupported(this.enlistment.EnlistmentRoot, out string fsError))
            {
                string error = $"FileSystem unsupported: {fsError}";
                this.tracer.RelatedError(error);
                return new Result(error);
            }

            string localCacheError;
            if (!this.TryDetermineLocalCacheAndInitializePaths(resolvedLocalCacheRoot, out localCacheError))
            {
                this.tracer.RelatedError(localCacheError);
                return new Result(localCacheError);
            }

            Directory.CreateDirectory(this.enlistment.GitObjectsRoot);
            Directory.CreateDirectory(this.enlistment.GitPackRoot);

            return new Result(true);
        }

        private string GetCloneRoot(out string fullEnlistmentRootPathParameter)
        {
            fullEnlistmentRootPathParameter = null;

            try
            {
                string repoName = this.RepositoryURL.Substring(this.RepositoryURL.LastIndexOf('/') + 1);
                fullEnlistmentRootPathParameter =
                    string.IsNullOrWhiteSpace(this.EnlistmentRootPathParameter)
                    ? Path.Combine(Environment.CurrentDirectory, repoName)
                    : this.EnlistmentRootPathParameter;

                fullEnlistmentRootPathParameter = Path.GetFullPath(fullEnlistmentRootPathParameter);

                string errorMessage;
                string enlistmentRootPath;
                if (!ScalarPlatform.Instance.FileSystem.TryGetNormalizedPath(fullEnlistmentRootPathParameter, out enlistmentRootPath, out errorMessage))
                {
                    this.ReportErrorAndExit("Unable to determine normalized path of clone root: " + errorMessage);
                    return null;
                }

                return enlistmentRootPath;
            }
            catch (IOException e)
            {
                this.ReportErrorAndExit("Unable to determine clone root: " + e.ToString());
                return null;
            }
        }

        private void CheckNotInsideExistingRepo(string normalizedEnlistmentRootPath)
        {
            if (ScalarEnlistment.TryGetScalarEnlistmentRoot(normalizedEnlistmentRootPath, out string existingEnlistmentRoot, out string workingDirectory))
            {
                this.ReportErrorAndExit("Error: You can't clone inside an existing Scalar repo ({0})", existingEnlistmentRoot);
            }
        }

        private bool TryDetermineLocalCacheAndInitializePaths(string localCacheRoot, out string errorMessage)
        {
            errorMessage = null;
            LocalCacheResolver localCacheResolver = new LocalCacheResolver(this.enlistment);

            VstsInfoData vstsInfo = this.QueryVstsInfo(this.tracer, this.enlistment, this.retryConfig);

            string error;
            string localCacheKey;
            if (!localCacheResolver.TryGetLocalCacheKeyFromRepoInfoOrURL(
                this.tracer,
                vstsInfo,
                localCacheKey: out localCacheKey,
                errorMessage: out error))
            {
                errorMessage = "Error determining local cache key: " + error;
                return false;
            }

            EventMetadata metadata = this.CreateEventMetadata();
            metadata.Add("localCacheRoot", localCacheRoot);
            metadata.Add("localCacheKey", localCacheKey);
            metadata.Add(TracingConstants.MessageKey.InfoMessage, "Initializing cache paths");
            this.tracer.RelatedEvent(EventLevel.Informational, "CloneVerb_TryDetermineLocalCacheAndInitializePaths", metadata);

            this.enlistment.InitializeCachePathsFromKey(localCacheRoot, localCacheKey);

            return true;
        }

        private Result CreateClone()
        {
            Result initRepoResult = this.TryInitRepo();
            if (!initRepoResult.Success)
            {
                return initRepoResult;
            }

            string errorMessage;
            if (!this.TrySetObjectCacheLocation(this.fileSystem, this.enlistment, out errorMessage))
            {
                return new Result("Error configuring alternate: " + errorMessage);
            }

            this.context = new ScalarContext(this.tracer, this.fileSystem, this.enlistment);

            if (!ScalarVerb.TrySetRequiredGitConfigSettings(this.enlistment) ||
                !ScalarVerb.TrySetOptionalGitConfigSettings(this.enlistment))
            {
                return new Result("Unable to configure git repo");
            }

            CacheServerResolver cacheServerResolver = new CacheServerResolver(this.tracer, this.enlistment);
            if (!cacheServerResolver.TrySaveUrlToLocalConfig(this.cacheServer, out errorMessage))
            {
                return new Result("Unable to configure cache server: " + errorMessage);
            }

            if (!this.TryDownloadCommit(
                this.refs.GetTipCommitId(this.Branch),
                this.enlistment,
                out errorMessage))
            {
                return new Result(errorMessage);
            }

            this.git = new GitProcess(this.enlistment);
            string originBranchName = "origin/" + this.Branch;
            GitProcess.Result createBranchResult = this.git.CreateBranchWithUpstream(this.Branch, originBranchName);
            if (createBranchResult.ExitCodeIsFailure)
            {
                return new Result("Unable to create branch '" + originBranchName + "': " + createBranchResult.Errors + "\r\n" + createBranchResult.Output);
            }

            File.WriteAllText(
                Path.Combine(this.enlistment.WorkingDirectoryBackingRoot, ScalarConstants.DotGit.Head),
                "ref: refs/heads/" + this.Branch);

            try
            {
                this.LogEnlistmentInfoAndSetConfigValues(this.tracer, this.git, this.enlistment);
            }
            catch (Exception e)
            {
                this.tracer.RelatedError(e.ToString());
                return new Result(e.Message);
            }

            return new Result(true);
        }

        private Result TryRegisterRepo()
        {
            if (this.Unattended)
            {
                this.tracer.RelatedInfo($"{nameof(this.Execute)}: Skipping repo registration (running Unattended)");
                return new Result(true);
            }

            string errorMessage = string.Empty;
            if (this.ShowStatusWhileRunning(
                () => { return this.TryRegisterRepo(this.tracer, this.enlistment, this.fileSystem, out errorMessage); },
                "Registering repo"))
            {
                this.tracer.RelatedInfo($"{nameof(this.Execute)}: Registration succeeded");
                return new Result(true);
            }

            this.tracer.RelatedError($"{nameof(this.Execute)}: Failed to register repo: {errorMessage}");
            return new Result($"Failed to register repo: {errorMessage}");
        }

        private Result TryInitRepo()
        {
            string repoPath = this.enlistment.WorkingDirectoryBackingRoot;
            GitProcess.Result initResult = GitProcess.Init(this.enlistment);
            if (initResult.ExitCodeIsFailure)
            {
                string error = string.Format("Could not init repo at to {0}: {1}", repoPath, initResult.Errors);
                this.tracer.RelatedError(error);
                return new Result(error);
            }

            GitProcess.Result remoteAddResult = new GitProcess(this.enlistment).RemoteAdd("origin", this.enlistment.RepoUrl);
            if (remoteAddResult.ExitCodeIsFailure)
            {
                string error = string.Format("Could not add remote to {0}: {1}", repoPath, remoteAddResult.Errors);
                this.tracer.RelatedError(error);
                return new Result(error);
            }

            File.WriteAllText(
                Path.Combine(repoPath, ScalarConstants.DotGit.PackedRefs),
                this.refs.ToPackedRefs());

            if (!this.FullClone)
            {
                GitProcess.Result sparseCheckoutResult = GitProcess.SparseCheckoutInit(this.enlistment);
                if (sparseCheckoutResult.ExitCodeIsFailure)
                {
                    string error = string.Format("Could not init sparse-checkout at to {0}: {1}", repoPath, sparseCheckoutResult.Errors);
                    this.tracer.RelatedError(error);
                    return new Result(error);
                }
            }

            return new Result(true);
        }

        private Result CheckoutRepo()
        {
            GitProcess.Result result = this.git.ForceCheckout(this.Branch);
            if (result.ExitCodeIsFailure)
            {
                EventMetadata metadata = this.CreateEventMetadata();
                metadata["git-output"] = result.Output;
                metadata["git-errors"] = result.Errors;
                this.tracer.RelatedError(metadata, "Failed to checkout repo");
                return new Result("Failed to checkout repo");
            }

            return new Result(true);
        }

        private void LogEnlistmentInfoAndSetConfigValues(ITracer tracer, GitProcess git, ScalarEnlistment enlistment)
        {
            string enlistmentId = Guid.NewGuid().ToString("N");
            EventMetadata metadata = this.CreateEventMetadata();
            metadata.Add("Enlistment", enlistment);
            metadata.Add("EnlistmentId", enlistmentId);
            metadata.Add("EnlistmentRoot", enlistment.EnlistmentRoot);
            metadata.Add("PhysicalDiskInfo", ScalarPlatform.Instance.GetPhysicalDiskInfo(enlistment.WorkingDirectoryRoot, sizeStatsOnly: false));
            tracer.RelatedEvent(EventLevel.Informational, "EnlistmentInfo", metadata, Keywords.Telemetry);

            GitProcess.Result configResult = git.SetInLocalConfig(ScalarConstants.GitConfig.EnlistmentId, enlistmentId, replaceAll: true);
            if (configResult.ExitCodeIsFailure)
            {
                string error = "Could not update config with enlistment id, error: " + configResult.Errors;
                tracer.RelatedWarning(error);
            }
        }

        private class Result
        {
            public Result(bool success)
            {
                this.Success = success;
                this.ErrorMessage = string.Empty;
            }

            public Result(string errorMessage)
            {
                this.Success = false;
                this.ErrorMessage = errorMessage;
            }

            public bool Success { get; }
            public string ErrorMessage { get; }
        }
    }
}
