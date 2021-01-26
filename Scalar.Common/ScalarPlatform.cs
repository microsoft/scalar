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

        public abstract NamedPipeServerStream CreatePipeByName(string pipeName);

        public abstract string GetOSVersionInformation();
        public abstract string GetCommonAppDataRootForScalar();

        public string GetCommonAppDataRootForScalarComponent(string componentName)
        {
            return Path.Combine(this.GetCommonAppDataRootForScalar(), componentName);
        }

        public abstract string GetSecureDataRootForScalar();

        public string GetSecureDataRootForScalarComponent(string componentName)
        {
            return Path.Combine(this.GetSecureDataRootForScalar(), componentName);
        }

        public string GetLogsDirectoryForGVFSComponent(string componentName)
        {
            return Path.Combine(this.GetCommonAppDataRootForScalarComponent(componentName), "Logs");
        }

        public abstract void InitializeEnlistmentACLs(string enlistmentPath);
        public abstract bool IsElevated();
        public abstract string GetCurrentUser();

        public abstract void ConfigureVisualStudio(string gitBinPath, ITracer tracer);

        public abstract bool TryVerifyAuthenticodeSignature(string path, out string subject, out string issuer, out string error);

        public abstract Dictionary<string, string> GetPhysicalDiskInfo(string path, bool sizeStatsOnly);

        public abstract bool TryKillProcessTree(int processId, out int exitCode, out string error);

        public abstract bool TryGetDefaultLocalCacheRoot(string enlistmentRoot, out string localCacheRoot, out string localCacheRootError);

        public abstract FileBasedLock CreateFileBasedLock(
            PhysicalFileSystem fileSystem,
            ITracer tracer,
            string lockPath);
        public abstract string GetTemplateHooksDirectory();

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

        public bool IsFileSystemCaseSensitivitySupported(string path, out string errorMessage)
        {
            Exception ex = null;
            bool caseSensitive = IsDirectoryCaseSensitive(path, out ex);

            if (ex != null)
            {
                errorMessage = $"Exception when performing {nameof(this.IsFileSystemCaseSensitivitySupported)}: {ex.ToString()}";
                return false;
            }

            bool caseSensitiveFileSystem = this.Constants.CaseSensitiveFileSystem;
            if (caseSensitive != caseSensitiveFileSystem)
            {
                errorMessage = $"Scalar does not support case {(caseSensitiveFileSystem ? "in" : "")}sensitive filesystems on {this.Name}";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public abstract class ScalarPlatformConstants
        {
            public static readonly char PathSeparator = Path.DirectorySeparatorChar;
            public abstract int MaxPipePathLength { get; }
            public abstract string ExecutableExtension { get; }
            public abstract string InstallerExtension { get; }

            public abstract string ScalarBinDirectoryPath { get; }

            public abstract string ScalarBinDirectoryName { get; }

            public abstract string ScalarExecutableName { get; }

            public abstract string ProgramLocaterCommand { get; }

            public abstract bool CaseSensitiveFileSystem { get; }

            public StringComparison PathComparison
            {
                get
                {
                    return this.CaseSensitiveFileSystem ?
                        StringComparison.Ordinal :
                        StringComparison.OrdinalIgnoreCase;
                }
            }

            public StringComparer PathComparer
            {
                get
                {
                    return this.CaseSensitiveFileSystem ?
                        StringComparer.Ordinal :
                        StringComparer.OrdinalIgnoreCase;
                }
            }
        }

        public class UnderConstructionFlags
        {
            public UnderConstructionFlags(
                bool supportsScalarConfig = true,
                bool supportsNuGetEncryption = true,
                bool supportsNuGetVerification = true)
            {
                this.SupportsScalarConfig = supportsScalarConfig;
                this.SupportsNuGetEncryption = supportsNuGetEncryption;
                this.SupportsNuGetVerification = supportsNuGetVerification;
            }

            public bool SupportsScalarConfig { get; }
            public bool SupportsNuGetEncryption { get; }
            public bool SupportsNuGetVerification { get; }
        }

        private bool IsDirectoryCaseSensitive(string path, out Exception exception)
        {
            try
            {
                string lowerCaseFilePath = Path.Combine(path, $"casetest{Guid.NewGuid().ToString()}");
                string upperCaseFilePath = lowerCaseFilePath.ToUpper();
                bool isCaseSensitive;

                using (FileStream fs = File.Create(lowerCaseFilePath))
                {
                }
                isCaseSensitive = !File.Exists(upperCaseFilePath);
                File.Delete(lowerCaseFilePath);

                exception = null;
                return isCaseSensitive;
            }
            catch (Exception e)
            {
                exception = e;
                return false;
            }
        }
    }
}
