using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.Prefetch;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Scalar.CommandLine
{
    [Verb(CloneVerb.CloneVerbName, HelpText = "Clone a git repo and mount it as a Scalar virtual repo")]
    public class CloneVerb : ScalarVerb
    {
        private const string CloneVerbName = "clone";

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
            "no-prefetch",
            Required = false,
            Default = false,
            HelpText = "Use this option to not prefetch commits after clone")]
        public bool NoPrefetch { get; set; }

        [Option(
            "local-cache-path",
            Required = false,
            HelpText = "Use this option to override the path for the local Scalar cache.")]
        public string LocalCacheRoot { get; set; }

        [Option(
            "sparse",
            Required = false,
            Default = "true",
            HelpText = "When cloning, create a sparse working directory.")]
        public string Sparse { get; set; }

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

            try
            {
                ScalarEnlistment enlistment;
                Result cloneResult = new Result(false);

                CacheServerInfo cacheServer = null;
                ServerScalarConfig serverScalarConfig = null;

                using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, "ScalarClone"))
                {
                    cloneResult = this.TryCreateEnlistment(fullEnlistmentRootPathParameter, normalizedEnlistmentRootPath, out enlistment);
                    if (cloneResult.Success)
                    {
                        tracer.AddLogFileEventListener(
                            ScalarEnlistment.GetNewScalarLogFileName(enlistment.ScalarLogsRoot, ScalarConstants.LogFileTypes.Clone),
                            EventLevel.Informational,
                            Keywords.Any);
                        tracer.WriteStartEvent(
                            enlistment.EnlistmentRoot,
                            enlistment.RepoUrl,
                            this.CacheServerUrl,
                            new EventMetadata
                            {
                                { "Branch", this.Branch },
                                { "LocalCacheRoot", this.LocalCacheRoot },
                                { "SingleBranch", this.SingleBranch },
                                { "Sparse", this.Sparse },
                                { "NoPrefetch", this.NoPrefetch },
                                { "Unattended", this.Unattended },
                                { "IsElevated", ScalarPlatform.Instance.IsElevated() },
                                { "NamedPipeName", enlistment.NamedPipeName },
                                { "ProcessID", Process.GetCurrentProcess().Id },
                                { nameof(this.EnlistmentRootPathParameter), this.EnlistmentRootPathParameter },
                                { nameof(fullEnlistmentRootPathParameter), fullEnlistmentRootPathParameter },
                            });

                        CacheServerResolver cacheServerResolver = new CacheServerResolver(tracer, enlistment);
                        cacheServer = cacheServerResolver.ParseUrlOrFriendlyName(this.CacheServerUrl);

                        string resolvedLocalCacheRoot;
                        if (string.IsNullOrWhiteSpace(this.LocalCacheRoot))
                        {
                            string localCacheRootError;
                            if (!LocalCacheResolver.TryGetDefaultLocalCacheRoot(enlistment, out resolvedLocalCacheRoot, out localCacheRootError))
                            {
                                this.ReportErrorAndExit(
                                    tracer,
                                    $"Failed to determine the default location for the local Scalar cache: `{localCacheRootError}`");
                            }
                        }
                        else
                        {
                            resolvedLocalCacheRoot = Path.GetFullPath(this.LocalCacheRoot);
                        }

                        this.Output.WriteLine("Clone parameters:");
                        this.Output.WriteLine("  Repo URL:     " + enlistment.RepoUrl);
                        this.Output.WriteLine("  Branch:       " + (string.IsNullOrWhiteSpace(this.Branch) ? "Default" : this.Branch));
                        this.Output.WriteLine("  Cache Server: " + cacheServer);
                        this.Output.WriteLine("  Local Cache:  " + resolvedLocalCacheRoot);
                        this.Output.WriteLine("  Destination:  " + enlistment.EnlistmentRoot);
                        this.Output.WriteLine("  Sparse:       " + this.Sparse);

                        string authErrorMessage;
                        if (!this.TryAuthenticate(tracer, enlistment, out authErrorMessage))
                        {
                            this.ReportErrorAndExit(tracer, "Cannot clone because authentication failed: " + authErrorMessage);
                        }

                        RetryConfig retryConfig = this.GetRetryConfig(tracer, enlistment, TimeSpan.FromMinutes(RetryConfig.FetchAndCloneTimeoutMinutes));
                        serverScalarConfig = this.QueryScalarConfig(tracer, enlistment, retryConfig);

                        cacheServer = this.ResolveCacheServer(tracer, cacheServer, cacheServerResolver, serverScalarConfig);

                        this.ValidateClientVersions(tracer, enlistment, serverScalarConfig, showWarnings: true);

                        this.ShowStatusWhileRunning(
                            () =>
                            {
                                cloneResult = this.TryClone(tracer, enlistment, cacheServer, retryConfig, serverScalarConfig, resolvedLocalCacheRoot);
                                return cloneResult.Success;
                            },
                            "Cloning",
                            normalizedEnlistmentRootPath);
                    }

                    if (!cloneResult.Success)
                    {
                        tracer.RelatedError(cloneResult.ErrorMessage);
                    }
                }

                if (cloneResult.Success)
                {
                    if (!this.NoPrefetch)
                    {
                        ReturnCode result = this.Execute<PrefetchVerb>(
                            enlistment,
                            verb =>
                            {
                                verb.Commits = true;
                                verb.SkipVersionCheck = true;
                                verb.ResolvedCacheServer = cacheServer;
                                verb.ServerScalarConfig = serverScalarConfig;
                            });

                        if (result != ReturnCode.Success)
                        {
                            this.Output.WriteLine("\r\nError during prefetch @ {0}", fullEnlistmentRootPathParameter);
                            exitCode = (int)result;
                        }
                    }

                    this.Execute<MountVerb>(
                        enlistment,
                        verb =>
                        {
                            verb.SkipMountedCheck = true;
                            verb.SkipVersionCheck = true;
                            verb.ResolvedCacheServer = cacheServer;
                            verb.DownloadedScalarConfig = serverScalarConfig;
                        });

                    GitProcess git = new GitProcess(enlistment);
                    git.ForceCheckoutAllFiles();
                }
                else
                {
                    this.Output.WriteLine("\r\nCannot clone @ {0}", fullEnlistmentRootPathParameter);
                    this.Output.WriteLine("Error: {0}", cloneResult.ErrorMessage);
                    exitCode = (int)ReturnCode.GenericError;
                }
            }
            catch (AggregateException e)
            {
                this.Output.WriteLine("Cannot clone @ {0}:", fullEnlistmentRootPathParameter);
                foreach (Exception ex in e.Flatten().InnerExceptions)
                {
                    this.Output.WriteLine("Exception: {0}", ex.ToString());
                }

                exitCode = (int)ReturnCode.GenericError;
            }
            catch (VerbAbortedException)
            {
                throw;
            }
            catch (Exception e)
            {
                this.ReportErrorAndExit("Cannot clone @ {0}: {1}", fullEnlistmentRootPathParameter, e.ToString());
            }

            Environment.Exit(exitCode);
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

        private Result TryClone(
            JsonTracer tracer,
            ScalarEnlistment enlistment,
            CacheServerInfo cacheServer,
            RetryConfig retryConfig,
            ServerScalarConfig serverScalarConfig,
            string resolvedLocalCacheRoot)
        {
            using (GitObjectsHttpRequestor objectRequestor = new GitObjectsHttpRequestor(tracer, enlistment, cacheServer, retryConfig))
            {
                GitRefs refs = objectRequestor.QueryInfoRefs(this.SingleBranch ? this.Branch : null);

                if (refs == null)
                {
                    return new Result("Could not query info/refs from: " + Uri.EscapeUriString(enlistment.RepoUrl));
                }

                if (this.Branch == null)
                {
                    this.Branch = refs.GetDefaultBranch();

                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Branch", this.Branch);
                    tracer.RelatedEvent(EventLevel.Informational, "CloneDefaultRemoteBranch", metadata);
                }
                else
                {
                    if (!refs.HasBranch(this.Branch))
                    {
                        EventMetadata metadata = new EventMetadata();
                        metadata.Add("Branch", this.Branch);
                        tracer.RelatedEvent(EventLevel.Warning, "CloneBranchDoesNotExist", metadata);

                        string errorMessage = string.Format("Remote branch {0} not found in upstream origin", this.Branch);
                        return new Result(errorMessage);
                    }
                }

                if (!enlistment.TryCreateEnlistmentFolders())
                {
                    string error = "Could not create enlistment directory";
                    tracer.RelatedError(error);
                    return new Result(error);
                }

                if (!ScalarPlatform.Instance.FileSystem.IsFileSystemSupported(enlistment.EnlistmentRoot, out string fsError))
                {
                    string error = $"FileSystem unsupported: {fsError}";
                    tracer.RelatedError(error);
                    return new Result(error);
                }

                string localCacheError;
                if (!this.TryDetermineLocalCacheAndInitializePaths(tracer, enlistment, serverScalarConfig, cacheServer, resolvedLocalCacheRoot, out localCacheError))
                {
                    tracer.RelatedError(localCacheError);
                    return new Result(localCacheError);
                }

                Directory.CreateDirectory(enlistment.GitObjectsRoot);
                Directory.CreateDirectory(enlistment.GitPackRoot);
                Directory.CreateDirectory(enlistment.BlobSizesRoot);

                return this.CreateClone(tracer, enlistment, objectRequestor, refs, this.Branch);
            }
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
            string errorMessage;
            string existingEnlistmentRoot;
            if (ScalarPlatform.Instance.TryGetScalarEnlistmentRoot(normalizedEnlistmentRootPath, out existingEnlistmentRoot, out errorMessage))
            {
                this.ReportErrorAndExit("Error: You can't clone inside an existing Scalar repo ({0})", existingEnlistmentRoot);
            }

            if (this.IsExistingPipeListening(normalizedEnlistmentRootPath))
            {
                this.ReportErrorAndExit($"Error: There is currently a Scalar.Mount process running for '{normalizedEnlistmentRootPath}'. This process must be stopped before cloning.");
            }
        }

        private bool TryDetermineLocalCacheAndInitializePaths(
            ITracer tracer,
            ScalarEnlistment enlistment,
            ServerScalarConfig serverScalarConfig,
            CacheServerInfo currentCacheServer,
            string localCacheRoot,
            out string errorMessage)
        {
            errorMessage = null;
            LocalCacheResolver localCacheResolver = new LocalCacheResolver(enlistment);

            string error;
            string localCacheKey;
            if (!localCacheResolver.TryGetLocalCacheKeyFromLocalConfigOrRemoteCacheServers(
                tracer,
                serverScalarConfig,
                currentCacheServer,
                localCacheRoot,
                localCacheKey: out localCacheKey,
                errorMessage: out error))
            {
                errorMessage = "Error determining local cache key: " + error;
                return false;
            }

            EventMetadata metadata = new EventMetadata();
            metadata.Add("localCacheRoot", localCacheRoot);
            metadata.Add("localCacheKey", localCacheKey);
            metadata.Add(TracingConstants.MessageKey.InfoMessage, "Initializing cache paths");
            tracer.RelatedEvent(EventLevel.Informational, "CloneVerb_TryDetermineLocalCacheAndInitializePaths", metadata);

            enlistment.InitializeCachePathsFromKey(localCacheRoot, localCacheKey);

            return true;
        }

        private Result CreateClone(
            ITracer tracer,
            ScalarEnlistment enlistment,
            GitObjectsHttpRequestor objectRequestor,
            GitRefs refs,
            string branch)
        {
            Result initRepoResult = this.TryInitRepo(tracer, refs, enlistment);
            if (!initRepoResult.Success)
            {
                return initRepoResult;
            }

            PhysicalFileSystem fileSystem = new PhysicalFileSystem();
            string errorMessage;
            if (!this.TryCreateAlternatesFile(fileSystem, enlistment, out errorMessage))
            {
                return new Result("Error configuring alternate: " + errorMessage);
            }

            GitRepo gitRepo = new GitRepo(tracer, enlistment, fileSystem);
            ScalarContext context = new ScalarContext(tracer, fileSystem, gitRepo, enlistment);
            ScalarGitObjects gitObjects = new ScalarGitObjects(context, objectRequestor);

            if (!this.TryDownloadCommit(
                refs.GetTipCommitId(branch),
                enlistment,
                objectRequestor,
                gitObjects,
                gitRepo,
                out errorMessage))
            {
                return new Result(errorMessage);
            }

            if (!ScalarVerb.TrySetRequiredGitConfigSettings(enlistment) ||
                !ScalarVerb.TrySetOptionalGitConfigSettings(enlistment))
            {
                return new Result("Unable to configure git repo");
            }

            CacheServerResolver cacheServerResolver = new CacheServerResolver(tracer, enlistment);
            if (!cacheServerResolver.TrySaveUrlToLocalConfig(objectRequestor.CacheServer, out errorMessage))
            {
                return new Result("Unable to configure cache server: " + errorMessage);
            }

            GitProcess git = new GitProcess(enlistment);
            string originBranchName = "origin/" + branch;
            GitProcess.Result createBranchResult = git.CreateBranchWithUpstream(branch, originBranchName);
            if (createBranchResult.ExitCodeIsFailure)
            {
                return new Result("Unable to create branch '" + originBranchName + "': " + createBranchResult.Errors + "\r\n" + createBranchResult.Output);
            }

            File.WriteAllText(
                Path.Combine(enlistment.WorkingDirectoryBackingRoot, ScalarConstants.DotGit.Head),
                "ref: refs/heads/" + branch);

            if (!this.TryDownloadRootGitAttributes(enlistment, gitObjects, gitRepo, out errorMessage))
            {
                return new Result(errorMessage);
            }

            this.CreateGitScript(enlistment);

            string installHooksError;
            if (!HooksInstaller.InstallHooks(context, out installHooksError))
            {
                tracer.RelatedError(installHooksError);
                return new Result(installHooksError);
            }

            // Place block below in an if statement when sparse clone is available.
            {
                BlobPrefetcher prefetcher = new BlobPrefetcher(
                                                    tracer,
                                                    enlistment,
                                                    objectRequestor,
                                                    fileList: new List<string>() { "*" },
                                                    folderList: null,
                                                    lastPrefetchArgs: null,
                                                    chunkSize: 4000,
                                                    searchThreadCount: Environment.ProcessorCount,
                                                    downloadThreadCount: Environment.ProcessorCount,
                                                    indexThreadCount: Environment.ProcessorCount);
                prefetcher.PrefetchWithStats(
                                branch,
                                isBranch: true,
                                hydrateFilesAfterDownload: false,
                                matchedBlobCount: out int _,
                                downloadedBlobCount: out int _,
                                hydratedFileCount: out int _);
            }

            GitProcess.Result forceCheckoutResult = git.ForceCheckout(branch);
            if (forceCheckoutResult.ExitCodeIsFailure && forceCheckoutResult.Errors.IndexOf("unable to read tree") > 0)
            {
                // It is possible to have the above TryDownloadCommit() fail because we
                // already have the commit and root tree we intend to check out, but
                // don't have a tree further down the working directory. If we fail
                // checkout here, its' because we don't have these trees and the
                // read-object hook is not available yet. Force downloading the commit
                // again and retry the checkout.

                if (!this.TryDownloadCommit(
                    refs.GetTipCommitId(branch),
                    enlistment,
                    objectRequestor,
                    gitObjects,
                    gitRepo,
                    out errorMessage,
                    checkLocalObjectCache: false))
                {
                    return new Result(errorMessage);
                }

                forceCheckoutResult = git.ForceCheckout(branch);
            }

            if (!RepoMetadata.TryInitialize(tracer, enlistment.DotScalarRoot, out errorMessage))
            {
                tracer.RelatedError(errorMessage);
                return new Result(errorMessage);
            }

            try
            {
                RepoMetadata.Instance.SaveCloneMetadata(tracer, enlistment);
                this.LogEnlistmentInfoAndSetConfigValues(tracer, git, enlistment);
            }
            catch (Exception e)
            {
                tracer.RelatedError(e.ToString());
                return new Result(e.Message);
            }
            finally
            {
                RepoMetadata.Shutdown();
            }

            return new Result(true);
        }

        // TODO(#1364): Don't call this method on POSIX platforms (or have it no-op on them)
        private void CreateGitScript(ScalarEnlistment enlistment)
        {
            FileInfo gitCmd = new FileInfo(Path.Combine(enlistment.EnlistmentRoot, "git.cmd"));
            using (FileStream fs = gitCmd.Create())
            using (StreamWriter writer = new StreamWriter(fs))
            {
                writer.Write(
@"
@echo OFF
echo .
echo ^[105;30m
echo      This repo was cloned using Scalar, and the git repo is in the 'src' directory
echo      Switching you to the 'src' directory and rerunning your git command
echo                                                                                      [0m

@echo ON
cd src
git %*
");
            }

            gitCmd.Attributes = FileAttributes.Hidden;
        }

        private Result TryInitRepo(ITracer tracer, GitRefs refs, Enlistment enlistmentToInit)
        {
            string repoPath = enlistmentToInit.WorkingDirectoryBackingRoot;
            GitProcess.Result initResult = GitProcess.Init(enlistmentToInit);
            if (initResult.ExitCodeIsFailure)
            {
                string error = string.Format("Could not init repo at to {0}: {1}", repoPath, initResult.Errors);
                tracer.RelatedError(error);
                return new Result(error);
            }

            GitProcess.Result remoteAddResult = new GitProcess(enlistmentToInit).RemoteAdd("origin", enlistmentToInit.RepoUrl);
            if (remoteAddResult.ExitCodeIsFailure)
            {
                string error = string.Format("Could not add remote to {0}: {1}", repoPath, remoteAddResult.Errors);
                tracer.RelatedError(error);
                return new Result(error);
            }

            File.WriteAllText(
                Path.Combine(repoPath, ScalarConstants.DotGit.PackedRefs),
                refs.ToPackedRefs());

            if (this.Sparse.Equals("true"))
            {
                GitProcess.Result sparseCheckoutResult = GitProcess.SparseCheckoutInit(enlistmentToInit);
                if (sparseCheckoutResult.ExitCodeIsFailure)
                {
                    string error = string.Format("Could not init sparse-checkout at to {0}: {1}", repoPath, sparseCheckoutResult.Errors);
                    tracer.RelatedError(error);
                    return new Result(error);
                }
            }

            return new Result(true);
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
