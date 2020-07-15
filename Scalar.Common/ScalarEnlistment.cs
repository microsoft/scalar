using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using System;
using System.IO;

namespace Scalar.Common
{
    public partial class ScalarEnlistment : Enlistment
    {
        public ScalarEnlistment(string enlistmentRoot, string workingDirectory, string repoUrl, string gitBinPath, GitAuthentication authentication)
            : base(
                  enlistmentRoot,
                  workingDirectory,
                  repoUrl,
                  gitBinPath,
                  flushFileBuffersForPacks: true,
                  authentication: authentication)
        {
            this.ScalarLogsRoot = Path.Combine(this.WorkingDirectoryRoot, ScalarConstants.DotGit.Logs.Root);
            this.LocalObjectsRoot = Path.Combine(this.WorkingDirectoryRoot, ScalarConstants.DotGit.Objects.Root);
        }

        public string ScalarLogsRoot { get; }

        public string LocalCacheRoot { get; private set; }

        public override string GitObjectsRoot { get; protected set; }
        public override string LocalObjectsRoot { get; protected set; }
        public override string GitPackRoot { get; protected set; }

        public bool UsesGvfsProtocol { get; protected set; }

        public static ScalarEnlistment CreateFromDirectory(
            string directory,
            string gitBinRoot,
            GitAuthentication authentication,
            bool createWithoutRepoURL = false)
        {
            if (Directory.Exists(directory))
            {
                string enlistmentRoot;
                string workingDirectory;

                if (!TryGetScalarEnlistmentRoot(directory, out enlistmentRoot, out workingDirectory))
                {
                    throw new InvalidRepoException(directory, $"Could not get enlistment root.");
                }

                if (createWithoutRepoURL)
                {
                    return new ScalarEnlistment(enlistmentRoot, workingDirectory, string.Empty, gitBinRoot, authentication);
                }

                return new ScalarEnlistment(enlistmentRoot, workingDirectory, null, gitBinRoot, authentication);
            }

            throw new InvalidRepoException(directory, $"Directory '{directory}' does not exist");
        }

        public static string GetNewScalarLogFileName(
            string logsRoot,
            string logFileType,
            string logId = null,
            PhysicalFileSystem fileSystem = null)
        {
            return Enlistment.GetNewLogFileName(
                logsRoot,
                "scalar_" + logFileType,
                logId: logId,
                fileSystem: fileSystem);
        }

        public static bool TryGetScalarEnlistmentRoot(
                                string directory,
                                out string enlistmentRoot,
                                out string workingDirectoryRoot,
                                Func<string, bool> exists = null)
        {
            if (exists == null)
            {
                exists = path => Directory.Exists(path) || File.Exists(path);
            }

            if (!ScalarPlatform.Instance.FileSystem.TryGetNormalizedPath(directory, out string normalized, out string _))
            {
                enlistmentRoot = null;
                workingDirectoryRoot = null;
                return false;
            }

            // Find a parent folder that exists.
            while (!exists(normalized))
            {
                normalized = Path.GetDirectoryName(normalized);
            }

            // First, try adding "src" to the end.
            string appendedSrc = Path.Combine(normalized, ScalarConstants.WorkingDirectoryRootName);
            string appendedSrcGit = Path.Combine(appendedSrc, ScalarConstants.DotGit.Root);
            if (exists(appendedSrc) && exists(appendedSrcGit))
            {
                enlistmentRoot = normalized;
                workingDirectoryRoot = appendedSrc;
                return true;
            }

            // Finally, check all parent folders to see if they contain a ".git" folder.
            // If found, check for the parent being "src".
            while (true)
            {
                string gitDir = Path.Combine(normalized, ScalarConstants.DotGit.Root);
                string parent = Directory.GetParent(normalized)?.FullName;

                if (exists(gitDir))
                {
                    // We have a .git directory OR a .git file (in the case of worktrees)
                    if (normalized.EndsWith($"{Path.DirectorySeparatorChar}src"))
                    {
                        enlistmentRoot = parent;
                    }
                    else
                    {
                        enlistmentRoot = normalized;
                    }

                    workingDirectoryRoot = normalized;
                    return true;
                }

                if (string.IsNullOrEmpty(parent) || parent.Length >= normalized.Length)
                {
                    break;
                }

                normalized = parent;
            }

            enlistmentRoot = null;
            workingDirectoryRoot = null;
            return false;
        }

        public void InitializeCachePathsFromKey(string localCacheRoot, string localCacheKey)
        {
            this.InitializeCachePaths(
                localCacheRoot,
                Path.Combine(localCacheRoot, localCacheKey));
        }

        public void InitializeCachePaths(string localCacheRoot, string gitObjectsRoot)
        {
            this.LocalCacheRoot = localCacheRoot;
            this.GitObjectsRoot = gitObjectsRoot;
            this.GitPackRoot = Path.Combine(this.GitObjectsRoot, ScalarConstants.DotGit.Objects.Pack.Name);

            // When using the GVFS protocol, we have a different cache location than local objects.
            this.UsesGvfsProtocol = !this.LocalCacheRoot.Equals(this.LocalObjectsRoot);
        }

        public bool TryCreateEnlistmentFolders()
        {
            try
            {
                Directory.CreateDirectory(this.EnlistmentRoot);
                ScalarPlatform.Instance.InitializeEnlistmentACLs(this.EnlistmentRoot);
                Directory.CreateDirectory(this.WorkingDirectoryRoot);
            }
            catch (IOException)
            {
                return false;
            }

            return true;
        }
    }
}
