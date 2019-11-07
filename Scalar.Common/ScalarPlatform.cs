using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;

namespace Scalar.Common
{
    public abstract class ScalarPlatform
    {
        public ScalarPlatform(UnderConstructionFlags underConstruction)
        {
            this.UnderConstruction = underConstruction;
        }

        public static ScalarPlatform Instance { get; private set; }

        public abstract IGitInstallation GitInstallation { get; }
        public abstract IDiskLayoutUpgradeData DiskLayoutUpgrade { get; }
        public abstract IPlatformFileSystem FileSystem { get; }

        public abstract ScalarPlatformConstants Constants { get; }
        public UnderConstructionFlags UnderConstruction { get; }
        public abstract string Name { get; }

        public abstract string ScalarConfigPath { get; }

        public static void Register(ScalarPlatform platform)
        {
            if (ScalarPlatform.Instance != null)
            {
                throw new InvalidOperationException("Cannot register more than one platform");
            }

            ScalarPlatform.Instance = platform;
        }

        /// <summary>
        /// Starts a Scalar process in the background.
        /// </summary>
        /// <remarks>
        /// This method should only be called by processes whose code we own as the background process must
        /// do some extra work after it starts.
        /// </remarks>
        public abstract void StartBackgroundScalarProcess(ITracer tracer, string programName, string[] args);

        /// <summary>
        /// Adjusts the current process for running in the background.
        /// </summary>
        /// <remarks>
        /// This method should be called after starting by processes launched using <see cref="ScalarPlatform.StartBackgroundScalarProcess"/>
        /// </remarks>
        /// <exception cref="Win32Exception">
        /// Failed to prepare process to run in background.
        /// </exception>
        public abstract void PrepareProcessToRunInBackground();

        public abstract bool IsProcessActive(int processId);
        public abstract void IsServiceInstalledAndRunning(string name, out bool installed, out bool running);
        public abstract string GetNamedPipeName(string enlistmentRoot);
        public abstract string GetScalarServiceNamedPipeName(string serviceName);
        public abstract NamedPipeServerStream CreatePipeByName(string pipeName);

        public abstract string GetOSVersionInformation();
        public abstract string GetDataRootForScalar();
        public abstract string GetDataRootForScalarComponent(string componentName);
        public abstract void InitializeEnlistmentACLs(string enlistmentPath);
        public abstract bool IsElevated();
        public abstract string GetCurrentUser();
        public abstract string GetUserIdFromLoginSessionId(int sessionId, ITracer tracer);

        /// <summary>
        /// Get the directory for upgrades that is permissioned to
        /// require elevated privileges to modify. This can be used for
        /// data that we don't want normal user accounts to modify.
        /// </summary>
        public abstract string GetUpgradeProtectedDataDirectory();

        /// <summary>
        /// Directory that upgrader log directory should be placed
        /// in. There can be multiple log directories, so this is the
        /// containing directory to place them in.
        /// </summary>
        public abstract string GetUpgradeLogDirectoryParentDirectory();

        /// <summary>
        /// Directory that contains the file indicating that a new
        /// version is available.
        /// </summary>
        public abstract string GetUpgradeHighestAvailableVersionDirectory();

        public abstract void ConfigureVisualStudio(string gitBinPath, ITracer tracer);

        public abstract bool TryVerifyAuthenticodeSignature(string path, out string subject, out string issuer, out string error);

        public abstract Dictionary<string, string> GetPhysicalDiskInfo(string path, bool sizeStatsOnly);

        public abstract bool IsConsoleOutputRedirectedToFile();

        public abstract bool TryKillProcessTree(int processId, out int exitCode, out string error);

        public abstract bool TryGetScalarEnlistmentRoot(string directory, out string enlistmentRoot, out string errorMessage);
        public abstract bool TryGetDefaultLocalCacheRoot(string enlistmentRoot, out string localCacheRoot, out string localCacheRootError);

        public abstract FileBasedLock CreateFileBasedLock(
            PhysicalFileSystem fileSystem,
            ITracer tracer,
            string lockPath);

        public abstract ProductUpgraderPlatformStrategy CreateProductUpgraderPlatformInteractions(
            PhysicalFileSystem fileSystem,
            ITracer tracer);

        public bool TryGetNormalizedPathRoot(string path, out string pathRoot, out string errorMessage)
        {
            pathRoot = null;
            errorMessage = null;
            string normalizedPath = null;

            if (!this.FileSystem.TryGetNormalizedPath(path, out normalizedPath, out errorMessage))
            {
                return false;
            }

            pathRoot = Path.GetPathRoot(normalizedPath);
            return true;
        }

        public abstract class ScalarPlatformConstants
        {
            public static readonly char PathSeparator = Path.DirectorySeparatorChar;
            public abstract int MaxPipePathLength { get; }
            public abstract string ExecutableExtension { get; }
            public abstract string InstallerExtension { get; }

            /// <summary>
            /// Indicates whether the platform supports running the upgrade application while
            /// the upgrade verb is running.
            /// </summary>
            public abstract bool SupportsUpgradeWhileRunning { get; }
            public abstract string WorkingDirectoryBackingRootPath { get; }
            public abstract string DotScalarRoot { get; }

            public abstract string ScalarBinDirectoryPath { get; }

            public abstract string ScalarBinDirectoryName { get; }

            public abstract string ScalarExecutableName { get; }

            public abstract string ProgramLocaterCommand { get; }

            /// <summary>
            /// Different platforms can have different requirements
            /// around which processes can block upgrade. For example,
            /// on Windows, we will block upgrade if any Scalar commands
            /// are running, but on POSIX platforms, we relax this
            /// constraint to allow upgrade to run while the upgrade
            /// command is running. Another example is that
            /// Non-windows platforms do not block upgrade when bash
            /// is running.
            /// </summary>
            public abstract HashSet<string> UpgradeBlockingProcesses { get; }

            public string ScalarUpgraderExecutableName
            {
                get { return "Scalar.Upgrader" + this.ExecutableExtension; }
            }
        }

        public class UnderConstructionFlags
        {
            public UnderConstructionFlags(
                bool supportsScalarUpgrade = true,
                bool supportsScalarConfig = true,
                bool supportsNuGetEncryption = true)
            {
                this.SupportsScalarUpgrade = supportsScalarUpgrade;
                this.SupportsScalarConfig = supportsScalarConfig;
                this.SupportsNuGetEncryption = supportsNuGetEncryption;
            }

            public bool SupportsScalarUpgrade { get; }
            public bool SupportsScalarConfig { get; }
            public bool SupportsNuGetEncryption { get; }
        }
    }
}
