using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using System;
using System.IO;

namespace Scalar.Common
{
    public abstract class Enlistment
    {
        protected Enlistment(
            string enlistmentRoot,
            string workingDirectoryRoot,
            string repoUrl,
            string gitBinPath,
            bool flushFileBuffersForPacks,
            GitAuthentication authentication)
        {
            if (string.IsNullOrWhiteSpace(gitBinPath))
            {
                throw new ArgumentException("Path to git.exe must be set");
            }

            this.EnlistmentRoot = enlistmentRoot;
            this.WorkingDirectoryRoot = workingDirectoryRoot;
            this.DotGitRoot = Path.Combine(this.WorkingDirectoryRoot, ScalarConstants.DotGit.Root);
            this.GitBinPath = gitBinPath;
            this.FlushFileBuffersForPacks = flushFileBuffersForPacks;

            GitProcess gitProcess = new GitProcess(this);
            if (repoUrl != null)
            {
                this.RepoUrl = repoUrl;
            }
            else
            {
                GitProcess.ConfigResult originResult = gitProcess.GetOriginUrl();
                if (!originResult.TryParseAsString(out string originUrl, out string error))
                {
                    if (!gitProcess.TryGetRemotes(out string[] remotes, out error))
                    {
                        throw new InvalidRepoException(this.WorkingDirectoryRoot, $"Failed to load remotes with error: {error}");
                    }

                    if (remotes.Length > 0)
                    {
                        GitProcess.ConfigResult remoteResult = gitProcess.GetFromLocalConfig($"remote.{remotes[0]}.url");
                        if (!remoteResult.TryParseAsString(out originUrl, out error))
                        {
                            originUrl = null;
                        }
                    }
                }

                this.RepoUrl = originUrl?.Trim() ?? string.Empty;
            }

            this.Authentication = authentication ?? new GitAuthentication(gitProcess, this.RepoUrl, this.WorkingDirectoryRoot);
        }

        public string EnlistmentRoot { get; }

        // Path to the root of the working (i.e. "src") directory.
        public string WorkingDirectoryRoot { get; }

        public string DotGitRoot { get; private set; }
        public abstract string GitObjectsRoot { get; protected set; }
        public abstract string LocalObjectsRoot { get; protected set; }
        public abstract string GitPackRoot { get; protected set; }
        public string RepoUrl { get; }
        public bool FlushFileBuffersForPacks { get; }

        public string GitBinPath { get; }

        public GitAuthentication Authentication { get; }

        public static string GetNewLogFileName(
            string logsRoot,
            string prefix,
            string logId = null,
            PhysicalFileSystem fileSystem = null)
        {
            fileSystem = fileSystem ?? new PhysicalFileSystem();

            // TODO: Remove Directory.CreateDirectory() code from here
            // Don't change the state from an accessor.
            if (!fileSystem.DirectoryExists(logsRoot))
            {
                fileSystem.CreateDirectory(logsRoot);
            }

            logId = logId ?? DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string name = prefix + "_" + logId;
            string fullPath = Path.Combine(
                logsRoot,
                name + ".log");

            if (string.IsNullOrEmpty(logId) && fileSystem.FileExists(fullPath))
            {
                fullPath = Path.Combine(
                    logsRoot,
                    name + "_" + Guid.NewGuid().ToString("N") + ".log");
            }

            return fullPath;
        }

        public virtual GitProcess CreateGitProcess()
        {
            return new GitProcess(this);
        }
    }
}
