using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using System;
using System.IO;

namespace Scalar.Common
{
    public partial class ScalarEnlistment : Enlistment
    {
        private string gitVersion;
        private string scalarVersion;

        // New enlistment
        public ScalarEnlistment(string enlistmentRoot, string repoUrl, string gitBinPath, GitAuthentication authentication)
            : this(
                  enlistmentRoot,
                  Path.Combine(enlistmentRoot, ScalarConstants.WorkingDirectoryRootName),
                  Path.Combine(enlistmentRoot, ScalarPlatform.Instance.Constants.WorkingDirectoryBackingRootPath),
                  repoUrl,
                  gitBinPath,
                  authentication)
        {
        }

        // Existing, configured enlistment
        private ScalarEnlistment(string enlistmentRoot, string workingDirectory, string repoUrl, string gitBinPath, GitAuthentication authentication)
            : this(
                  enlistmentRoot,
                  workingDirectory,
                  workingDirectory,
                  repoUrl,
                  gitBinPath,
                  authentication)
        {
        }

        private ScalarEnlistment(string enlistmentRoot,
                                 string workingDirectory,
                                 string workingDirectoryBackingRoot,
                                 string repoUrl,
                                 string gitBinPath,
                                 GitAuthentication authentication)
            : base(
                  enlistmentRoot,
                  workingDirectory,
                  workingDirectoryBackingRoot,
                  repoUrl,
                  gitBinPath,
                  flushFileBuffersForPacks: true,
                  authentication: authentication)
        {
            this.ScalarLogsRoot = Path.Combine(this.WorkingDirectoryBackingRoot, ScalarConstants.DotGit.Logs.Root);
            this.LocalObjectsRoot = Path.Combine(this.WorkingDirectoryBackingRoot, ScalarConstants.DotGit.Objects.Root);
        }

        public string ScalarLogsRoot { get; }

        public string LocalCacheRoot { get; private set; }

        public override string GitObjectsRoot { get; protected set; }
        public override string LocalObjectsRoot { get; protected set; }
        public override string GitPackRoot { get; protected set; }
        public bool IsScalarRepo { get; protected set; }

        // These version properties are only used in logging during clone and mount to track version numbers
        public string GitVersion
        {
            get { return this.gitVersion; }
        }

        public string ScalarVersion
        {
            get { return this.scalarVersion; }
        }

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
                    throw new InvalidRepoException($"Could not get enlistment root.");
                }

                if (createWithoutRepoURL)
                {
                    return new ScalarEnlistment(enlistmentRoot, string.Empty, gitBinRoot, authentication);
                }

                return new ScalarEnlistment(enlistmentRoot, workingDirectory, null, gitBinRoot, authentication);
            }

            throw new InvalidRepoException($"Directory '{directory}' does not exist");
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

            // Second, look all places where "src" exists in the path and look for "src/.git"
            int srcPos = 0;
            string srcSearch = $"{Path.DirectorySeparatorChar}{ScalarConstants.WorkingDirectoryRootName}";

            while ((srcPos = normalized.IndexOf(srcSearch, srcPos + 1, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                string srcDir = normalized.Substring(0, srcPos + srcSearch.Length);

                if (!srcDir.Equals(normalized, StringComparison.OrdinalIgnoreCase) &&
                    !normalized.StartsWith(srcDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    // the "src" that appears inside the normalized path is not a full directory name!
                    continue;
                }

                string gitDir = Path.Combine(srcDir, ScalarConstants.DotGit.Root);

                if (exists(gitDir))
                {
                    // We have a .git directory OR a .git file (in the case of worktrees)
                    enlistmentRoot = normalized.Substring(0, srcPos); ;
                    workingDirectoryRoot = srcDir;
                    return true;
                }
            }

            // Finally, check all parent folders to see if they contain a ".git" folder.
            while (true)
            {
                string gitDir = Path.Combine(normalized, ScalarConstants.DotGit.Root);

                if (exists(gitDir))
                {
                    // We have a .git directory OR a .git file (in the case of worktrees)
                    enlistmentRoot = normalized;
                    workingDirectoryRoot = normalized;
                    return true;
                }

                string parent = Directory.GetParent(normalized)?.FullName;
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

        public void SetGitVersion(string gitVersion)
        {
            this.SetOnce(gitVersion, ref this.gitVersion);
        }

        public void SetScalarVersion(string scalarVersion)
        {
            this.SetOnce(scalarVersion, ref this.scalarVersion);
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

            // Scalar repos have a different cache location than their local objects
            this.IsScalarRepo = !this.LocalCacheRoot.Equals(this.LocalObjectsRoot);
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

        public string GetEnlistmentId()
        {
            return this.GetId(ScalarConstants.GitConfig.EnlistmentId);
        }

        private void SetOnce<T>(T value, ref T valueToSet)
        {
            if (valueToSet != null)
            {
                throw new InvalidOperationException("Value already set.");
            }

            valueToSet = value;
        }

        /// <summary>
        /// Creates a hidden directory @ the given path.
        /// If directory already exists, hides it.
        /// </summary>
        /// <param name="path">Path to desired hidden directory</param>
        private void CreateHiddenDirectory(string path)
        {
            DirectoryInfo dir = Directory.CreateDirectory(path);
            dir.Attributes = FileAttributes.Hidden;
        }

        private string GetId(string key)
        {
            GitProcess.ConfigResult configResult = this.CreateGitProcess().GetFromLocalConfig(key);
            string value;
            string error;
            configResult.TryParseAsString(out value, out error, defaultValue: string.Empty);
            return value.Trim();
        }
    }
}
