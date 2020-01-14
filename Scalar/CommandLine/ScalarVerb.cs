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

        protected void UnregisterRepo(string enlistmentRoot)
        {
            string repoRegistryLocation = ScalarPlatform.Instance.GetCommonAppDataRootForScalarComponent(ScalarConstants.RepoRegistry.RegistryDirectoryName);
            ScalarRepoRegistry repoRegistry = new ScalarRepoRegistry(
                                                        new JsonTracer(nameof(DeleteVerb), nameof(this.Execute)),
                                                        new PhysicalFileSystem(),
                                                        repoRegistryLocation);

            bool found = false;
            foreach (ScalarRepoRegistration registration in repoRegistry.GetRegisteredRepos())
            {
                if (registration.NormalizedRepoRoot.Equals(enlistmentRoot))
                {
                    found = true;
                }
            }

            if (found && !repoRegistry.TryUnregisterRepo(enlistmentRoot, out string error))
            {
                this.ReportErrorAndExit($"Error while unregistering repo: {error}");
            }
        }

        private string GetAlternatesPath(ScalarEnlistment enlistment)
        {
            return Path.Combine(enlistment.WorkingDirectoryRoot, ScalarConstants.DotGit.Objects.Info.Alternates);
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

                string localCacheRoot;
                if (string.IsNullOrWhiteSpace(gitObjectsRoot))
                {
                    // We do not have an object cache. This is a vanilla Git repo!
                    localCacheRoot = enlistment.LocalObjectsRoot;
                    gitObjectsRoot = enlistment.LocalObjectsRoot;
                }
                else
                {
                    localCacheRoot = Path.GetDirectoryName(gitObjectsRoot);
                }

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
