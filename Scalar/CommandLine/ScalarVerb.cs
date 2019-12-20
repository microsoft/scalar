using CommandLine;
using Newtonsoft.Json;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;

namespace Scalar.CommandLine
{
    public abstract class ScalarVerb
    {
        protected const string StartServiceInstructions = "Run 'sc start Scalar.Service' from an elevated command prompt to ensure it is running.";

        private readonly bool validateOriginURL;

        public ScalarVerb(bool validateOrigin = true)
        {
            this.Output = Console.Out;
            this.ReturnCode = ReturnCode.Success;
            this.validateOriginURL = validateOrigin;
            this.ServiceName = ScalarConstants.Service.ServiceName;
            this.StartedByService = false;
            this.Unattended = ScalarEnlistment.IsUnattended(tracer: null);

            this.InitializeDefaultParameterValues();
        }

        [Flags]
        private enum GitCoreGVFSFlags
        {
            // GVFS_SKIP_SHA_ON_INDEX
            // Disables the calculation of the sha when writing the index
            SkipShaOnIndex = 1 << 0,

            // GVFS_BLOCK_COMMANDS
            // Blocks git commands that are not allowed in a GVFS/Scalar repo
            BlockCommands = 1 << 1,

            // GVFS_MISSING_OK
            // Normally git write-tree ensures that the objects referenced by the
            // directory exist in the object database.This option disables this check.
            MissingOk = 1 << 2,

            // GVFS_NO_DELETE_OUTSIDE_SPARSECHECKOUT
            // When marking entries to remove from the index and the working
            // directory this option will take into account what the
            // skip-worktree bit was set to so that if the entry has the
            // skip-worktree bit set it will not be removed from the working
            // directory.  This will allow virtualized working directories to
            // detect the change to HEAD and use the new commit tree to show
            // the files that are in the working directory.
            NoDeleteOutsideSparseCheckout = 1 << 3,

            // GVFS_FETCH_SKIP_REACHABILITY_AND_UPLOADPACK
            // While performing a fetch with a virtual file system we know
            // that there will be missing objects and we don't want to download
            // them just because of the reachability of the commits.  We also
            // don't want to download a pack file with commits, trees, and blobs
            // since these will be downloaded on demand.  This flag will skip the
            // checks on the reachability of objects during a fetch as well as
            // the upload pack so that extraneous objects don't get downloaded.
            FetchSkipReachabilityAndUploadPack = 1 << 4,

            // 1 << 5 has been deprecated

            // GVFS_BLOCK_FILTERS_AND_EOL_CONVERSIONS
            // With a virtual file system we only know the file size before any
            // CRLF or smudge/clean filters processing is done on the client.
            // To prevent file corruption due to truncation or expansion with
            // garbage at the end, these filters must not run when the file
            // is first accessed and brought down to the client. Git.exe can't
            // currently tell the first access vs subsequent accesses so this
            // flag just blocks them from occurring at all.
            BlockFiltersAndEolConversions = 1 << 6,

            // GVFS_PREFETCH_DURING_FETCH
            // While performing a `git fetch` command, use the gvfs-helper to
            // perform a "prefetch" of commits and trees.
            PrefetchDuringFetch = 1 << 7,
        }

        public abstract string EnlistmentRootPathParameter { get; set; }

        [Option(
            ScalarConstants.VerbParameters.InternalUseOnly,
            Required = false,
            HelpText = "This parameter is reserved for internal use.")]
        public string InternalParameters
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        InternalVerbParameters mountInternal = InternalVerbParameters.FromJson(value);
                        if (!string.IsNullOrEmpty(mountInternal.ServiceName))
                        {
                            this.ServiceName = mountInternal.ServiceName;
                        }

                        this.StartedByService = mountInternal.StartedByService;
                    }
                    catch (JsonReaderException e)
                    {
                        this.ReportErrorAndExit("Failed to parse InternalParameters: {0}.\n {1}", value, e);
                    }
                }
            }
        }

        public string ServiceName { get; set; }

        public bool StartedByService { get; set; }

        public bool Unattended { get; private set; }

        public string ServicePipeName
        {
            get
            {
                return ScalarPlatform.Instance.GetScalarServiceNamedPipeName(this.ServiceName);
            }
        }

        public TextWriter Output { get; set; }

        public ReturnCode ReturnCode { get; private set; }

        protected abstract string VerbName { get; }

        public static bool TrySetRequiredGitConfigSettings(Enlistment enlistment)
        {
            string expectedHooksPath = Path.Combine(enlistment.WorkingDirectoryBackingRoot, ScalarConstants.DotGit.Hooks.Root);
            expectedHooksPath = Paths.ConvertPathToGitFormat(expectedHooksPath);

            string coreGVFSFlags = Convert.ToInt32(
                GitCoreGVFSFlags.BlockCommands |
                GitCoreGVFSFlags.MissingOk |
                GitCoreGVFSFlags.FetchSkipReachabilityAndUploadPack |
                GitCoreGVFSFlags.PrefetchDuringFetch)
                .ToString();

            // These settings are required for normal Scalar functionality.
            // They will override any existing local configuration values.
            //
            // IMPORTANT! These must parallel the settings in ControlGitRepo:Initialize
            //
            Dictionary<string, string> requiredSettings = new Dictionary<string, string>
            {
                { "am.keepcr", "true" },
                { "checkout.optimizenewbranch", "true" },
                { "core.autocrlf", "false" },
                { "core.commitGraph", "true" },
                { "core.fscache", "true" },
                { "core.gvfs", coreGVFSFlags },
                { ScalarConstants.GitConfig.UseGvfsHelper, "true" },
                { "core.multiPackIndex", "true" },
                { "core.preloadIndex", "true" },
                { "core.safecrlf", "false" },
                { "core.untrackedCache", ScalarPlatform.Instance.FileSystem.SupportsUntrackedCache ? "true" : "false" },
                { "core.repositoryformatversion", "0" },
                { "core.filemode", ScalarPlatform.Instance.FileSystem.SupportsFileMode ? "true" : "false" },
                { "core.bare", "false" },
                { "core.logallrefupdates", "true" },
                { "core.hookspath", expectedHooksPath },
                { GitConfigSetting.CredentialUseHttpPath, "true" },
                { "credential.validate", "false" },
                { "gc.auto", "0" },
                { "gui.gcwarning", "false" },
                { "index.threads", "true" },
                { "index.version", "4" },
                { "merge.stat", "false" },
                { "merge.renames", "false" },
                { "pack.useBitmaps", "false" },
                { "pack.useSparse", "true" },
                { "receive.autogc", "false" },
                { "reset.quiet", "true" },
                { "http.version", "HTTP/1.1" },
                { "feature.manyFiles", "false" },
                { "feature.experimental", "false" },
                { "fetch.writeCommitGraph", "false" },
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                requiredSettings.Add("http.sslBackend", "schannel");
            }

            if (!TrySetConfig(enlistment, requiredSettings, isRequired: true))
            {
                return false;
            }

            return true;
        }

        public static bool TrySetOptionalGitConfigSettings(Enlistment enlistment)
        {
            // These settings are optional, because they impact performance but not functionality of Scalar.
            // These settings should only be set by the clone or repair verbs, so that they do not
            // overwrite the values set by the user in their local config.
            Dictionary<string, string> optionalSettings = new Dictionary<string, string>
            {
                { "status.aheadbehind", "false" },
            };

            if (!TrySetConfig(enlistment, optionalSettings, isRequired: false))
            {
                return false;
            }

            return true;
        }

        public abstract void Execute();

        public virtual void InitializeDefaultParameterValues()
        {
        }

        protected ReturnCode Execute<TVerb>(
            string enlistmentRootPath,
            Action<TVerb> configureVerb = null)
            where TVerb : ScalarVerb, new()
        {
            TVerb verb = new TVerb();
            verb.EnlistmentRootPathParameter = enlistmentRootPath;
            verb.ServiceName = this.ServiceName;
            verb.Unattended = this.Unattended;

            if (configureVerb != null)
            {
                configureVerb(verb);
            }

            try
            {
                verb.Execute();
            }
            catch (VerbAbortedException)
            {
            }

            return verb.ReturnCode;
        }

        protected ReturnCode Execute<TVerb>(
            ScalarEnlistment enlistment,
            Action<TVerb> configureVerb = null,
            Action<TVerb> postExecuteVerb = null)
            where TVerb : ScalarVerb.ForExistingEnlistment, new()
        {
            TVerb verb = new TVerb();
            verb.EnlistmentRootPathParameter = enlistment.EnlistmentRoot;
            verb.ServiceName = this.ServiceName;
            verb.Unattended = this.Unattended;

            if (configureVerb != null)
            {
                configureVerb(verb);
            }

            try
            {
                verb.Execute(enlistment.Authentication);
            }
            catch (VerbAbortedException)
            {
            }

            if (postExecuteVerb != null)
            {
                postExecuteVerb(verb);
            }

            return verb.ReturnCode;
        }

        protected bool ShowStatusWhileRunning(
            Func<bool> action,
            string message)
        {
            return ConsoleHelper.ShowStatusWhileRunning(
                action,
                message,
                this.Output,
                showSpinner: !this.Unattended && this.Output == Console.Out && !ScalarPlatform.Instance.IsConsoleOutputRedirectedToFile(),
                initialDelayMs: 0);
        }

        protected bool TryAuthenticate(ITracer tracer, ScalarEnlistment enlistment, out string authErrorMessage)
        {
            string authError = null;

            bool result = this.ShowStatusWhileRunning(
                () => enlistment.Authentication.TryInitialize(tracer, enlistment, out authError),
                "Authenticating");

            authErrorMessage = authError;
            return result;
        }

        protected EventMetadata CreateEventMetadata(Exception e = null)
        {
            EventMetadata metadata = new EventMetadata();
            this.AddVerbDataToMetadata(metadata);
            if (e != null)
            {
                metadata.Add("Exception", e.ToString());
            }

            return metadata;
        }

        /// <summary>
        /// Add the standard ScalarVerb metadata to the specified EventMetadata
        /// </summary>
        /// <param name="metadata">
        /// EventMetadata to which verb data will be added
        /// </param>
        /// <returns>
        /// The specified EventMetadata (updated to include verb metadata)
        /// </returns>
        protected EventMetadata AddVerbDataToMetadata(EventMetadata metadata)
        {
            metadata["Area"] = $"{this.VerbName}_Verb";
            metadata["Verb"] = this.VerbName;
            return metadata;
        }

        protected void ReportErrorAndExit(ITracer tracer, ReturnCode exitCode, string error, params object[] args)
        {
            if (!string.IsNullOrEmpty(error))
            {
                if (args == null || args.Length == 0)
                {
                    this.Output.WriteLine(error);
                    if (tracer != null && exitCode != ReturnCode.Success)
                    {
                        tracer.RelatedError(error);
                    }
                }
                else
                {
                    this.Output.WriteLine(error, args);
                    if (tracer != null && exitCode != ReturnCode.Success)
                    {
                        tracer.RelatedError(error, args);
                    }
                }
            }

            this.ReturnCode = exitCode;
            throw new VerbAbortedException(this);
        }

        protected void ReportErrorAndExit(string error, params object[] args)
        {
            this.ReportErrorAndExit(tracer: null, exitCode: ReturnCode.GenericError, error: error, args: args);
        }

        protected void ReportErrorAndExit(ITracer tracer, string error, params object[] args)
        {
            this.ReportErrorAndExit(tracer, ReturnCode.GenericError, error, args);
        }

        protected RetryConfig GetRetryConfig(ITracer tracer, ScalarEnlistment enlistment, TimeSpan? timeoutOverride = null)
        {
            RetryConfig retryConfig;
            string error;
            if (!RetryConfig.TryLoadFromGitConfig(tracer, enlistment, out retryConfig, out error))
            {
                this.ReportErrorAndExit(tracer, "Failed to determine Scalar timeout and max retries: " + error);
            }

            if (timeoutOverride.HasValue)
            {
                retryConfig.Timeout = timeoutOverride.Value;
            }

            return retryConfig;
        }

        protected ServerScalarConfig QueryScalarConfig(ITracer tracer, ScalarEnlistment enlistment, RetryConfig retryConfig)
        {
            ServerScalarConfig serverScalarConfig = null;
            string errorMessage = null;
            if (!this.ShowStatusWhileRunning(
                () =>
                {
                    using (ConfigHttpRequestor configRequestor = new ConfigHttpRequestor(tracer, enlistment, retryConfig))
                    {
                        const bool LogErrors = true;
                        return configRequestor.TryQueryScalarConfig(LogErrors, out serverScalarConfig, out _, out errorMessage);
                    }
                },
                "Querying remote for config"))
            {
                this.ReportErrorAndExit(tracer, "Unable to query /gvfs/config" + Environment.NewLine + errorMessage);
            }

            return serverScalarConfig;
        }

        protected VstsInfoData QueryVstsInfo(ITracer tracer, ScalarEnlistment enlistment, RetryConfig retryConfig)
        {
            VstsInfoData vstsInfo = null;
            string errorMessage = null;
            if (!this.ShowStatusWhileRunning(
                () =>
                {
                    using (VstsInfoHttpRequestor repoInfoRequestor = new VstsInfoHttpRequestor(tracer, enlistment, retryConfig))
                    {
                        const bool LogErrors = true;
                        return repoInfoRequestor.TryQueryRepoInfo(LogErrors, out vstsInfo, out errorMessage);
                    }
                },
                "Querying remote for repo info"))
            {
                this.ReportErrorAndExit(tracer, $"Unable to query {ScalarConstants.Endpoints.RepoInfo}" + Environment.NewLine + errorMessage);
            }

            return vstsInfo;
        }

        protected void ValidateClientVersions(ITracer tracer, ScalarEnlistment enlistment, ServerScalarConfig scalarConfig, bool showWarnings)
        {
            this.CheckGitVersion(tracer, enlistment, out string gitVersion);
            enlistment.SetGitVersion(gitVersion);

            string errorMessage = null;
            bool errorIsFatal = false;
            if (!this.TryValidateScalarVersion(enlistment, tracer, scalarConfig, out errorMessage, out errorIsFatal))
            {
                if (errorIsFatal)
                {
                    this.ReportErrorAndExit(tracer, errorMessage);
                }
                else if (showWarnings)
                {
                    this.Output.WriteLine();
                    this.Output.WriteLine(errorMessage);
                    this.Output.WriteLine();
                }
            }
        }

        protected bool TrySetObjectCacheLocation(PhysicalFileSystem fileSystem, ScalarEnlistment enlistment, out string errorMessage)
        {
            try
            {
                string alternatesFilePath = this.GetAlternatesPath(enlistment);
                string tempFilePath = alternatesFilePath + ".tmp";
                string gitObjectsPath = enlistment.GitObjectsRoot.Replace(Path.PathSeparator, ScalarConstants.GitPathSeparator);
                fileSystem.WriteAllText(tempFilePath, gitObjectsPath);
                fileSystem.MoveAndOverwriteFile(tempFilePath, alternatesFilePath);

                GitProcess process = new GitProcess(enlistment);
                process.SetInLocalConfig(ScalarConstants.GitConfig.ObjectCache, gitObjectsPath);
            }
            catch (SecurityException e)
            {
                errorMessage = e.Message;
                return false;
            }
            catch (IOException e)
            {
                errorMessage = e.Message;
                return false;
            }

            errorMessage = null;
            return true;
        }

        protected void BlockEmptyCacheServerUrl(string userInput)
        {
            if (userInput == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(userInput))
            {
                this.ReportErrorAndExit(
@"You must specify a value for the cache server.
You can specify a URL, a name of a configured cache server, or the special names None or Default.");
            }
        }

        protected CacheServerInfo ResolveCacheServer(
            ITracer tracer,
            CacheServerInfo cacheServer,
            CacheServerResolver cacheServerResolver,
            ServerScalarConfig serverScalarConfig)
        {
            CacheServerInfo resolvedCacheServer = cacheServer;

            if (cacheServer.Url == null)
            {
                string cacheServerName = cacheServer.Name;
                string error = null;

                if (!cacheServerResolver.TryResolveUrlFromRemote(
                        cacheServerName,
                        serverScalarConfig,
                        out resolvedCacheServer,
                        out error))
                {
                    this.ReportErrorAndExit(tracer, error);
                }
            }
            else if (cacheServer.Name.Equals(CacheServerInfo.ReservedNames.UserDefined))
            {
                resolvedCacheServer = cacheServerResolver.ResolveNameFromRemote(cacheServer.Url, serverScalarConfig);
            }

            this.Output.WriteLine("Using cache server: " + resolvedCacheServer);
            return resolvedCacheServer;
        }

        protected void ValidatePathParameter(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    Path.GetFullPath(path);
                }
                catch (Exception e)
                {
                    this.ReportErrorAndExit("Invalid path: '{0}' ({1})", path, e.Message);
                }
            }
        }

        protected bool TryDownloadCommit(
            string commitId,
            Enlistment enlistment,
            out string error)
        {
            GitProcess process = new GitProcess(enlistment);
            GitProcess.Result downloadResult = process.GvfsHelperDownloadCommit(commitId);

            error = downloadResult.Errors;
            return downloadResult.ExitCodeIsSuccess;
        }

        protected ScalarEnlistment CreateEnlistment(string enlistmentRootPath, GitAuthentication authentication)
        {
            string gitBinPath = ScalarPlatform.Instance.GitInstallation.GetInstalledGitBinPath();
            if (string.IsNullOrWhiteSpace(gitBinPath))
            {
                this.ReportErrorAndExit("Error: " + ScalarConstants.GitIsNotInstalledError);
            }

            ScalarEnlistment enlistment = null;
            try
            {
                enlistment = ScalarEnlistment.CreateFromDirectory(
                    enlistmentRootPath,
                    gitBinPath,
                    authentication,
                    createWithoutRepoURL: !this.validateOriginURL);
            }
            catch (InvalidRepoException e)
            {
                this.ReportErrorAndExit(
                    "Error: '{0}' is not a valid Scalar enlistment. {1}",
                    enlistmentRootPath,
                    e.Message);
            }

            return enlistment;
        }

        protected bool TryRegisterRepo(ITracer tracer, ScalarEnlistment enlistment, PhysicalFileSystem fileSystem, out string errorMessage)
        {
            string repoRegistryLocation = ScalarPlatform.Instance.GetCommonAppDataRootForScalarComponent(ScalarConstants.RepoRegistry.RegistryDirectoryName);
            ScalarRepoRegistry repoRegistry = new ScalarRepoRegistry(
                tracer,
                fileSystem,
                repoRegistryLocation);

            tracer.RelatedInfo($"{nameof(this.Execute)}: Registering repo '{enlistment.EnlistmentRoot}'");
            return repoRegistry.TryRegisterRepo(enlistment.EnlistmentRoot, ScalarPlatform.Instance.GetCurrentUser(), out errorMessage);
        }

        private static bool TrySetConfig(Enlistment enlistment, Dictionary<string, string> configSettings, bool isRequired)
        {
            GitProcess git = new GitProcess(enlistment);

            Dictionary<string, GitConfigSetting> existingConfigSettings;

            // If the settings are required, then only check local config settings, because we don't want to depend on
            // global settings that can then change independent of this repo.
            if (!git.TryGetAllConfig(localOnly: isRequired, configSettings: out existingConfigSettings))
            {
                return false;
            }

            foreach (KeyValuePair<string, string> setting in configSettings)
            {
                GitConfigSetting existingSetting;
                if (setting.Value != null)
                {
                    if (!existingConfigSettings.TryGetValue(setting.Key, out existingSetting) ||
                        (isRequired && !existingSetting.HasValue(setting.Value)))
                    {
                        GitProcess.Result setConfigResult = git.SetInLocalConfig(setting.Key, setting.Value);
                        if (setConfigResult.ExitCodeIsFailure)
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    if (existingConfigSettings.TryGetValue(setting.Key, out existingSetting))
                    {
                        git.DeleteFromLocalConfig(setting.Key);
                    }
                }
            }

            return true;
        }

        private string GetAlternatesPath(ScalarEnlistment enlistment)
        {
            return Path.Combine(enlistment.WorkingDirectoryBackingRoot, ScalarConstants.DotGit.Objects.Info.Alternates);
        }

        private void CheckGitVersion(ITracer tracer, ScalarEnlistment enlistment, out string version)
        {
            GitVersion gitVersion = null;
            if (string.IsNullOrEmpty(enlistment.GitBinPath) || !GitProcess.TryGetVersion(enlistment.GitBinPath, out gitVersion, out string _))
            {
                this.ReportErrorAndExit(tracer, "Error: Unable to retrieve the git version");
            }

            version = gitVersion.ToString();

            if (gitVersion.Platform != ScalarConstants.SupportedGitVersion.Platform)
            {
                this.ReportErrorAndExit(tracer, "Error: Invalid version of git {0}.  Must use scalar version.", version);
            }

            if (ProcessHelper.IsDevelopmentVersion())
            {
                if (gitVersion.IsLessThan(ScalarConstants.SupportedGitVersion))
                {
                    this.ReportErrorAndExit(
                        tracer,
                        "Error: Installed git version {0} is less than the supported version of {1}.",
                        gitVersion,
                        ScalarConstants.SupportedGitVersion);
                }
                else if (!gitVersion.IsEqualTo(ScalarConstants.SupportedGitVersion))
                {
                    this.Output.WriteLine($"Warning: Installed git version {gitVersion} does not match supported version of {ScalarConstants.SupportedGitVersion}.");
                }
            }
            else
            {
                if (!gitVersion.IsEqualTo(ScalarConstants.SupportedGitVersion))
                {
                    this.ReportErrorAndExit(
                        tracer,
                        "Error: Installed git version {0} does not match supported version of {1}.",
                        gitVersion,
                        ScalarConstants.SupportedGitVersion);
                }
            }
        }

        private bool TryValidateScalarVersion(ScalarEnlistment enlistment, ITracer tracer, ServerScalarConfig config, out string errorMessage, out bool errorIsFatal)
        {
            errorMessage = null;
            errorIsFatal = false;

            using (ITracer activity = tracer.StartActivity("ValidateScalarVersion", EventLevel.Informational))
            {
                Version currentVersion = new Version(ProcessHelper.GetCurrentProcessVersion());

                IEnumerable<ServerScalarConfig.VersionRange> allowedGvfsClientVersions =
                    config != null
                    ? config.AllowedScalarClientVersions
                    : null;

                if (allowedGvfsClientVersions == null || !allowedGvfsClientVersions.Any())
                {
                    errorMessage = "WARNING: Unable to validate your Scalar version" + Environment.NewLine;
                    if (config == null)
                    {
                        errorMessage += "Could not query valid Scalar versions from: " + Uri.EscapeUriString(enlistment.RepoUrl);
                    }
                    else
                    {
                        errorMessage += "Server not configured to provide supported Scalar versions";
                    }

                    EventMetadata metadata = this.CreateEventMetadata();
                    tracer.RelatedError(metadata, errorMessage, Keywords.Network);

                    return false;
                }

                foreach (ServerScalarConfig.VersionRange versionRange in config.AllowedScalarClientVersions)
                {
                    if (currentVersion >= versionRange.Min &&
                        (versionRange.Max == null || currentVersion <= versionRange.Max))
                    {
                        activity.RelatedEvent(
                            EventLevel.Informational,
                            "ScalarVersionValidated",
                            this.AddVerbDataToMetadata(new EventMetadata
                            {
                                { "SupportedVersionRange", versionRange },
                            }));

                        enlistment.SetScalarVersion(currentVersion.ToString());
                        return true;
                    }
                }

                activity.RelatedError("Scalar version {0} is not supported", currentVersion);
            }

            errorMessage = "ERROR: Your Scalar version is no longer supported.  Install the latest and try again.";
            errorIsFatal = true;
            return false;
        }

        public abstract class ForExistingEnlistment : ScalarVerb
        {
            public ForExistingEnlistment(bool validateOrigin = true) : base(validateOrigin)
            {
            }

            [Value(
                0,
                Required = false,
                Default = "",
                MetaName = "Enlistment Root Path",
                HelpText = "Full or relative path to the Scalar enlistment root")]
            public override string EnlistmentRootPathParameter { get; set; }

            public sealed override void Execute()
            {
                this.Execute(authentication: null);
            }

            public void Execute(GitAuthentication authentication)
            {
                this.ValidatePathParameter(this.EnlistmentRootPathParameter);

                this.PreCreateEnlistment();
                ScalarEnlistment enlistment = this.CreateEnlistment(this.EnlistmentRootPathParameter, authentication);

                this.Execute(enlistment);
            }

            protected virtual void PreCreateEnlistment()
            {
            }

            protected abstract void Execute(ScalarEnlistment enlistment);

            protected void InitializeCachePaths(
                ITracer tracer,
                ScalarEnlistment enlistment)
            {
                string error;
                string gitObjectsRoot;
                GitProcess process = new GitProcess(enlistment);
                GitProcess.ConfigResult result = process.GetFromLocalConfig(ScalarConstants.GitConfig.ObjectCache);
                if (!result.TryParseAsString(out gitObjectsRoot, out error))
                {
                    this.ReportErrorAndExit("Failed to determine git objects root from git config: " + error);
                }

                if (string.IsNullOrWhiteSpace(gitObjectsRoot))
                {
                    this.ReportErrorAndExit(tracer, "Invalid git objects root (empty or whitespace)");
                }

                string localCacheRoot = Path.GetDirectoryName(gitObjectsRoot);

                if (string.IsNullOrWhiteSpace(localCacheRoot))
                {
                    this.ReportErrorAndExit(tracer, "Invalid local cache path (empty or whitespace)");
                }

                enlistment.InitializeCachePaths(localCacheRoot, gitObjectsRoot);
            }
        }

        public abstract class ForNoEnlistment : ScalarVerb
        {
            public ForNoEnlistment(bool validateOrigin = true) : base(validateOrigin)
            {
            }

            public override string EnlistmentRootPathParameter
            {
                get { throw new InvalidOperationException(); }
                set { throw new InvalidOperationException(); }
            }
        }

        public class VerbAbortedException : Exception
        {
            public VerbAbortedException(ScalarVerb verb)
            {
                this.Verb = verb;
            }

            public ScalarVerb Verb { get; }
        }
    }
}
