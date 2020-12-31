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
                        InternalVerbParameters internalParams = InternalVerbParameters.FromJson(value);
                        if (!string.IsNullOrEmpty(internalParams.ServiceName))
                        {
                            this.ServiceName = internalParams.ServiceName;
                        }

                        this.StartedByService = internalParams.StartedByService;
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
            catch (InvalidRepoException ire)
            {
                this.ReportErrorAndExit($"Invalid repository: {ire.Message}");
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
                this.Output);
        }

        protected GitAuthentication.Result TryAuthenticate(ITracer tracer, ScalarEnlistment enlistment, out string authErrorMessage)
        {
            string authError = null;

            GitAuthentication.Result result = GitAuthentication.Result.UnableToDetermine;
            bool runResult = this.ShowStatusWhileRunning(
                () =>
                {
                    result = enlistment.Authentication.TryInitialize(tracer, enlistment, out authError);
                    return true;
                },
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
                tracer.RelatedWarning("Unable to query /gvfs/config" + Environment.NewLine + errorMessage);
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

        protected void ValidateUrlParameter(string url)
        {
            if (url.StartsWith("http://") ||
                url.StartsWith("https://") ||
                url.StartsWith("ssh://"))
            {
                return;
            }

            // Do not allow other protocols or absolute file paths.
            if (url.Contains("://") || url.StartsWith('/'))
            {
                this.ReportErrorAndExit($"Invalid URL: '{url}'. Only HTTP, HTTPS, and SSH URLs are supported.");
            }

            // Everything else _could_ be a valid SSH URL.
        }

        protected bool TryDownloadCommit(
            string commitId,
            Enlistment enlistment,
            out string error)
        {
            GitProcess process = new GitProcess(enlistment);
            GitProcess.Result downloadResult = process.GvfsHelperDownloadCommit(commitId);

            if (downloadResult.ExitCodeIsFailure)
            {
                error = string.IsNullOrEmpty(downloadResult.Errors)
                            ? "Error while downloading tip commit"
                            : "Error while downloading tip commit:\n" + downloadResult.Errors;
            }
            else
            {
                error = null;
            }

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

        protected GitFeatureFlags GetAvailableGitFeatures(ITracer tracer)
        {
            // Determine what features of Git we have available to guide how we init/clone the repository
            var gitFeatures = GitFeatureFlags.None;
            string gitBinPath = ScalarPlatform.Instance.GitInstallation.GetInstalledGitBinPath();
            tracer.RelatedInfo("Attempting to determine Git version for installation '{0}'", gitBinPath);
            if (GitProcess.TryGetVersion(gitBinPath, out var gitVersion, out string gitVersionError))
            {
                tracer.RelatedInfo("Git installation '{0}' has version '{1}", gitBinPath, gitVersion);
                gitFeatures = gitVersion.GetFeatures();
            }
            else
            {
                tracer.RelatedWarning("Unable to detect Git features for installation '{0}'. Failed to get Git version: '{1}", gitBinPath, gitVersionError);
                this.Output.WriteLine("Warning: unable to detect Git features: {0}", gitVersionError);
            }

            return gitFeatures;
        }

        private string GetAlternatesPath(ScalarEnlistment enlistment)
        {
            return Path.Combine(enlistment.WorkingDirectoryRoot, ScalarConstants.DotGit.Objects.Info.Alternates);
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
