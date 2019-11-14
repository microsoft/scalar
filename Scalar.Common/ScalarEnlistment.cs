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
            : base(
                  enlistmentRoot,
                  Path.Combine(enlistmentRoot, ScalarConstants.WorkingDirectoryRootName),
                  Path.Combine(enlistmentRoot, ScalarPlatform.Instance.Constants.WorkingDirectoryBackingRootPath),
                  repoUrl,
                  gitBinPath,
                  flushFileBuffersForPacks: true,
                  authentication: authentication)
        {
            this.DotScalarRoot = Path.Combine(this.EnlistmentRoot, ScalarPlatform.Instance.Constants.DotScalarRoot);
            this.ScalarLogsRoot = Path.Combine(this.WorkingDirectoryBackingRoot, ScalarConstants.DotGit.Logs.Root);
            this.LocalObjectsRoot = Path.Combine(this.WorkingDirectoryBackingRoot, ScalarConstants.DotGit.Objects.Root);
        }

        // Existing, configured enlistment
        private ScalarEnlistment(string enlistmentRoot, string gitBinPath, GitAuthentication authentication)
            : this(
                  enlistmentRoot,
                  null,
                  gitBinPath,
                  authentication)
        {
        }

        public string DotScalarRoot { get; }

        public string ScalarLogsRoot { get; }

        public string LocalCacheRoot { get; private set; }

        public override string GitObjectsRoot { get; protected set; }
        public override string LocalObjectsRoot { get; protected set; }
        public override string GitPackRoot { get; protected set; }

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

                if (!TryGetScalarEnlistmentRoot(directory, out enlistmentRoot))
                {
                    throw new InvalidRepoException($"Could not get enlistment root.");
                }

                if (createWithoutRepoURL)
                {
                    return new ScalarEnlistment(enlistmentRoot, string.Empty, gitBinRoot, authentication);
                }

                return new ScalarEnlistment(enlistmentRoot, gitBinRoot, authentication);
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
                logId: null,
                fileSystem: fileSystem);
        }

        public static bool TryGetScalarEnlistmentRoot(string directory, out string enlistmentRoot)
        {
            // First, find a parent folder that exists.
            while (!Directory.Exists(directory))
            {
                directory = Path.GetDirectoryName(directory);
            }

            // Second, check all parent folders to see if they
            // contain a "src/.git" or ".git" folder.
            while (!string.IsNullOrEmpty(directory))
            {
                string srcDir = Path.Combine(directory, ScalarConstants.WorkingDirectoryRootName);

                if (!Directory.Exists(srcDir))
                {
                    srcDir = directory;
                }

                string gitDir = Path.Combine(srcDir, ScalarConstants.DotGit.Root);

                if (Directory.Exists(gitDir) || File.Exists(gitDir))
                {
                    // We have a .git directory OR a .git file (in the case of worktrees)
                    enlistmentRoot = directory;
                    return true;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            enlistmentRoot = null;
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
        }

        public bool TryCreateEnlistmentFolders()
        {
            try
            {
                Directory.CreateDirectory(this.EnlistmentRoot);
                ScalarPlatform.Instance.InitializeEnlistmentACLs(this.EnlistmentRoot);
                Directory.CreateDirectory(this.WorkingDirectoryRoot);
                this.CreateHiddenDirectory(this.DotScalarRoot);
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
