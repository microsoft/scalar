using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.Maintenance;
using Scalar.Common.Prefetch;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;

namespace Scalar.CommandLine
{
    [Verb(PrefetchVerb.PrefetchVerbName, HelpText = "Prefetch remote objects for the current head")]
    public class PrefetchVerb : ScalarVerb.ForExistingEnlistment
    {
        private const string PrefetchVerbName = "prefetch";

        private const int LockWaitTimeMs = 100;
        private const int WaitingOnLockLogThreshold = 50;
        private const int IoFailureRetryDelayMS = 50;
        private const string PrefetchCommitsAndTreesLock = "prefetch-commits-trees.lock";

        private const int ChunkSize = 4000;
        private static readonly int SearchThreadCount = Environment.ProcessorCount;
        private static readonly int DownloadThreadCount = Environment.ProcessorCount;
        private static readonly int IndexThreadCount = Environment.ProcessorCount;

        private List<string> parsedFoldersList;

        [Option(
            "files",
            Required = false,
            Default = "",
            HelpText = "A semicolon-delimited list of files to fetch. Simple prefix wildcards, e.g. *.txt, are supported.")]
        public string Files { get; set; }

        [Option(
            "folders",
            Required = false,
            Default = "",
            HelpText = "A semicolon-delimited list of folders to fetch. Wildcards are not supported.")]
        public string Folders { get; set; }

        [Option(
            "folders-list",
            Required = false,
            Default = "",
            HelpText = "A file containing line-delimited list of folders to fetch. Wildcards are not supported.")]
        public string FoldersListFile { get; set; }

        [Option(
            "stdin-files-list",
            Required = false,
            Default = false,
            HelpText = "Specify this flag to load file list from stdin. Same format as when loading from file.")]
        public bool FilesFromStdIn { get; set; }

        [Option(
            "stdin-folders-list",
            Required = false,
            Default = false,
            HelpText = "Specify this flag to load folder list from stdin. Same format as when loading from file.")]
        public bool FoldersFromStdIn { get; set; }

        [Option(
            "files-list",
            Required = false,
            Default = "",
            HelpText = "A file containing line-delimited list of files to fetch. Wildcards are supported.")]
        public string FilesListFile { get; set; }

        [Option(
            'c',
            "commits",
            Required = false,
            Default = false,
            HelpText = "Fetch the latest set of commit and tree packs. This option cannot be used with any of the file- or folder-related options.")]
        public bool Commits { get; set; }

        [Option(
            "verbose",
            Required = false,
            Default = false,
            HelpText = "Show all outputs on the console in addition to writing them to a log file.")]
        public bool Verbose { get; set; }

        public bool SkipVersionCheck { get; set; }
        public CacheServerInfo ResolvedCacheServer { get; set; }
        public ServerScalarConfig ServerScalarConfig { get; set; }

        public List<string> ParsedFoldersList
        {
            get { return this.parsedFoldersList; }
        }

        protected override string VerbName
        {
            get { return PrefetchVerbName; }
        }

        protected override void Execute(ScalarEnlistment enlistment)
        {
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, "Prefetch"))
            {
                if (this.Verbose)
                {
                    tracer.AddDiagnosticConsoleEventListener(EventLevel.Informational, Keywords.Any);
                }

                string cacheServerUrl = CacheServerResolver.GetUrlFromConfig(enlistment);

                tracer.AddLogFileEventListener(
                    ScalarEnlistment.GetNewScalarLogFileName(enlistment.ScalarLogsRoot, ScalarConstants.LogFileTypes.Prefetch),
                    EventLevel.Informational,
                    Keywords.Any);
                tracer.WriteStartEvent(
                    enlistment.EnlistmentRoot,
                    enlistment.RepoUrl,
                    cacheServerUrl);

                try
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Commits", this.Commits);
                    metadata.Add("Files", this.Files);
                    metadata.Add("Folders", this.Folders);
                    metadata.Add("FileListFile", this.FilesListFile);
                    metadata.Add("FoldersListFile", this.FoldersListFile);
                    metadata.Add("FilesFromStdIn", this.FilesFromStdIn);
                    metadata.Add("FoldersFromStdIn", this.FoldersFromStdIn);
                    tracer.RelatedEvent(EventLevel.Informational, "PerformPrefetch", metadata);

                    if (this.Commits)
                    {
                        if (!string.IsNullOrWhiteSpace(this.Files) ||
                            !string.IsNullOrWhiteSpace(this.Folders) ||
                            !string.IsNullOrWhiteSpace(this.FoldersListFile) ||
                            !string.IsNullOrWhiteSpace(this.FilesListFile) ||
                            this.FilesFromStdIn ||
                            this.FoldersFromStdIn)
                        {
                            this.ReportErrorAndExit(tracer, "You cannot prefetch commits and blobs at the same time.");
                        }

                        GitObjectsHttpRequestor objectRequestor;
                        CacheServerInfo cacheServer;
                        this.InitializeServerConnection(
                            tracer,
                            enlistment,
                            cacheServerUrl,
                            out objectRequestor,
                            out cacheServer);
                        this.PrefetchCommits(tracer, enlistment, objectRequestor, cacheServer);
                    }
                    else
                    {
                        string headCommitId;
                        List<string> filesList;
                        FileBasedDictionary<string, string> lastPrefetchArgs;

                        this.LoadBlobPrefetchArgs(tracer, enlistment, out headCommitId, out filesList, out this.parsedFoldersList, out lastPrefetchArgs);

                        if (BlobPrefetcher.IsNoopPrefetch(tracer, lastPrefetchArgs, headCommitId, filesList, this.parsedFoldersList))
                        {
                            Console.WriteLine("All requested files are already available. Nothing new to prefetch.");
                        }
                        else
                        {
                            GitObjectsHttpRequestor objectRequestor;
                            CacheServerInfo cacheServer;
                            this.InitializeServerConnection(
                                tracer,
                                enlistment,
                                cacheServerUrl,
                                out objectRequestor,
                                out cacheServer);
                            this.PrefetchBlobs(tracer, enlistment, headCommitId, filesList, this.parsedFoldersList, lastPrefetchArgs, objectRequestor, cacheServer);
                        }
                    }
                }
                catch (VerbAbortedException)
                {
                    throw;
                }
                catch (AggregateException aggregateException)
                {
                    this.Output.WriteLine(
                        "Cannot prefetch {0}. " + ConsoleHelper.GetScalarLogMessage(enlistment.EnlistmentRoot),
                        enlistment.EnlistmentRoot);
                    foreach (Exception innerException in aggregateException.Flatten().InnerExceptions)
                    {
                        tracer.RelatedError(
                            new EventMetadata
                            {
                                { "Verb", typeof(PrefetchVerb).Name },
                                { "Exception", innerException.ToString() }
                            },
                            $"Unhandled {innerException.GetType().Name}: {innerException.Message}");
                    }

                    Environment.ExitCode = (int)ReturnCode.GenericError;
                }
                catch (Exception e)
                {
                    this.Output.WriteLine(
                        "Cannot prefetch {0}. " + ConsoleHelper.GetScalarLogMessage(enlistment.EnlistmentRoot),
                        enlistment.EnlistmentRoot);
                    tracer.RelatedError(
                        new EventMetadata
                        {
                            { "Verb", typeof(PrefetchVerb).Name },
                            { "Exception", e.ToString() }
                        },
                        $"Unhandled {e.GetType().Name}: {e.Message}");

                    Environment.ExitCode = (int)ReturnCode.GenericError;
                }
            }
        }

        private void InitializeServerConnection(
            ITracer tracer,
            ScalarEnlistment enlistment,
            string cacheServerUrl,
            out GitObjectsHttpRequestor objectRequestor,
            out CacheServerInfo cacheServer)
        {
            RetryConfig retryConfig = this.GetRetryConfig(tracer, enlistment, TimeSpan.FromMinutes(RetryConfig.FetchAndCloneTimeoutMinutes));

            cacheServer = this.ResolvedCacheServer;
            ServerScalarConfig serverScalarConfig = this.ServerScalarConfig;
            if (!this.SkipVersionCheck)
            {
                string authErrorMessage;
                if (!this.TryAuthenticate(tracer, enlistment, out authErrorMessage))
                {
                    this.ReportErrorAndExit(tracer, "Unable to prefetch because authentication failed: " + authErrorMessage);
                }

                if (serverScalarConfig == null)
                {
                    serverScalarConfig = this.QueryScalarConfig(tracer, enlistment, retryConfig);
                }

                if (cacheServer == null)
                {
                    CacheServerResolver cacheServerResolver = new CacheServerResolver(tracer, enlistment);
                    cacheServer = cacheServerResolver.ResolveNameFromRemote(cacheServerUrl, serverScalarConfig);
                }

                this.ValidateClientVersions(tracer, enlistment, serverScalarConfig, showWarnings: false);

                this.Output.WriteLine("Configured cache server: " + cacheServer);
            }

            this.InitializeLocalCacheAndObjectsPaths(tracer, enlistment, retryConfig, serverScalarConfig, cacheServer);
            objectRequestor = new GitObjectsHttpRequestor(tracer, enlistment, cacheServer, retryConfig);
        }

        private void PrefetchCommits(ITracer tracer, ScalarEnlistment enlistment, GitObjectsHttpRequestor objectRequestor, CacheServerInfo cacheServer)
        {
            bool success;
            string error = string.Empty;
            PhysicalFileSystem fileSystem = new PhysicalFileSystem();
            GitRepo repo = new GitRepo(tracer, enlistment, fileSystem);
            ScalarContext context = new ScalarContext(tracer, fileSystem, repo, enlistment);
            GitObjects gitObjects = new ScalarGitObjects(context, objectRequestor);

            if (this.Verbose)
            {
                success = new PrefetchStep(context, gitObjects, requireCacheLock: false).TryPrefetchCommitsAndTrees(out error);
            }
            else
            {
                success = this.ShowStatusWhileRunning(
                    () => new PrefetchStep(context, gitObjects, requireCacheLock: false).TryPrefetchCommitsAndTrees(out error),
                "Fetching commits and trees " + this.GetCacheServerDisplay(cacheServer, enlistment.RepoUrl));
            }

            if (!success)
            {
                this.ReportErrorAndExit(tracer, "Prefetching commits and trees failed: " + error);
            }
        }

        private void LoadBlobPrefetchArgs(
            ITracer tracer,
            ScalarEnlistment enlistment,
            out string headCommitId,
            out List<string> filesList,
            out List<string> foldersList,
            out FileBasedDictionary<string, string> lastPrefetchArgs)
        {
            string error;

            if (!FileBasedDictionary<string, string>.TryCreate(
                    tracer,
                    Path.Combine(enlistment.DotScalarRoot, "LastBlobPrefetch.dat"),
                    new PhysicalFileSystem(),
                    out lastPrefetchArgs,
                    out error))
            {
                tracer.RelatedWarning("Unable to load last prefetch args: " + error);
            }

            filesList = new List<string>();
            foldersList = new List<string>();

            if (!BlobPrefetcher.TryLoadFileList(enlistment, this.Files, this.FilesListFile, filesList, readListFromStdIn: this.FilesFromStdIn, error: out error))
            {
                this.ReportErrorAndExit(tracer, error);
            }

            if (!BlobPrefetcher.TryLoadFolderList(enlistment, this.Folders, this.FoldersListFile, foldersList, readListFromStdIn: this.FoldersFromStdIn, error: out error))
            {
                this.ReportErrorAndExit(tracer, error);
            }

            GitProcess gitProcess = new GitProcess(enlistment);
            GitProcess.Result result = gitProcess.RevParse(ScalarConstants.DotGit.HeadName);
            if (result.ExitCodeIsFailure)
            {
                this.ReportErrorAndExit(tracer, result.Errors);
            }

            headCommitId = result.Output.Trim();
        }

        private void PrefetchBlobs(
            ITracer tracer,
            ScalarEnlistment enlistment,
            string headCommitId,
            List<string> filesList,
            List<string> foldersList,
            FileBasedDictionary<string, string> lastPrefetchArgs,
            GitObjectsHttpRequestor objectRequestor,
            CacheServerInfo cacheServer)
        {
            BlobPrefetcher blobPrefetcher = new BlobPrefetcher(
                tracer,
                enlistment,
                objectRequestor,
                filesList,
                foldersList,
                lastPrefetchArgs,
                ChunkSize,
                SearchThreadCount,
                DownloadThreadCount,
                IndexThreadCount);

            if (blobPrefetcher.FolderList.Count == 0 &&
                blobPrefetcher.FileList.Count == 0)
            {
                this.ReportErrorAndExit(tracer, "Did you mean to fetch all blobs? If so, specify `--files '*'` to confirm.");
            }

            int matchedBlobCount = 0;
            int downloadedBlobCount = 0;

            Func<bool> doPrefetch =
                () =>
                {
                    try
                    {
                        blobPrefetcher.PrefetchWithStats(
                            headCommitId,
                            isBranch: false,
                            matchedBlobCount: out matchedBlobCount,
                            downloadedBlobCount: out downloadedBlobCount);
                        return !blobPrefetcher.HasFailures;
                    }
                    catch (BlobPrefetcher.FetchException e)
                    {
                        tracer.RelatedError(e.Message);
                        return false;
                    }
                };

            if (this.Verbose)
            {
                doPrefetch();
            }
            else
            {
                this.ShowStatusWhileRunning(doPrefetch, "Fetching blobs " + this.GetCacheServerDisplay(cacheServer, enlistment.RepoUrl));
            }

            if (blobPrefetcher.HasFailures)
            {
                Environment.ExitCode = 1;
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Stats:");
                Console.WriteLine("  Matched blobs:    " + matchedBlobCount);
                Console.WriteLine("  Already cached:   " + (matchedBlobCount - downloadedBlobCount));
                Console.WriteLine("  Downloaded:       " + downloadedBlobCount);
            }
        }

        private bool CheckIsMounted(bool verbose)
        {
            Func<bool> checkMount = () => this.Execute<StatusVerb>(
                    this.EnlistmentRootPathParameter,
                    verb => verb.Output = new StreamWriter(new MemoryStream())) == ReturnCode.Success;

            if (verbose)
            {
                return ConsoleHelper.ShowStatusWhileRunning(
                    checkMount,
                    "Checking that Scalar is mounted",
                    this.Output,
                    showSpinner: true,
                    scalarLogEnlistmentRoot: null);
            }
            else
            {
                return checkMount();
            }
        }

        private string GetCacheServerDisplay(CacheServerInfo cacheServer, string repoUrl)
        {
            if (!cacheServer.IsNone(repoUrl))
            {
                return "from cache server";
            }

            return "from origin (no cache server)";
        }
    }
}
