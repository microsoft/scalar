using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using Scalar.Platform.POSIX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Scalar.Platform.Linux
{
    public partial class LinuxPlatform : POSIXPlatform
    {
        // TODO(Linux): determine installation location and upgrader path
        private const string UpgradeProtectedDataDirectory = "/usr/local/scalar_upgrader";

        // TODO(Linux): We should ideally consider any colon-separated paths
        // in $XDG_CONFIG_DIRS and $XDG_DATA_DIRS, as well as their defaults
        // (i.e., /etc/xdg and /usr/local/share:/usr/share).
        // We should also ideally create any missing directories using a 0700
        // permission mode via a native wrapper for mkdir(2) instead of
        // relying on our caller's use of Directory.CreateDirectory().
        private static readonly EnvironmentVariableBasePath[] EnvironmentVariableBaseCachePaths = new[] {
            new EnvironmentVariableBasePath(
                ScalarConstants.LinuxPlatform.EnvironmentVariables.LocalUserCacheFolder,
                ScalarConstants.LinuxPlatform.LocalScalarFolderName),
            new EnvironmentVariableBasePath(
                ScalarConstants.POSIXPlatform.EnvironmentVariables.LocalUserFolder,
                ScalarConstants.LinuxPlatform.LocalScalarCachePath),
        };
        protected static readonly EnvironmentVariableBasePath[] EnvironmentVariableBaseConfigPaths = new[] {
            new EnvironmentVariableBasePath(
                ScalarConstants.LinuxPlatform.EnvironmentVariables.LocalUserConfigFolder,
                ScalarConstants.LinuxPlatform.LocalScalarFolderName),
            new EnvironmentVariableBasePath(
                ScalarConstants.POSIXPlatform.EnvironmentVariables.LocalUserFolder,
                ScalarConstants.LinuxPlatform.LocalScalarConfigPath),
        };
        protected static readonly EnvironmentVariableBasePath[] EnvironmentVariableBaseDataPaths = new[] {
            new EnvironmentVariableBasePath(
                ScalarConstants.LinuxPlatform.EnvironmentVariables.LocalUserDataFolder,
                ScalarConstants.LinuxPlatform.LocalScalarFolderName),
            new EnvironmentVariableBasePath(
                ScalarConstants.POSIXPlatform.EnvironmentVariables.LocalUserFolder,
                ScalarConstants.LinuxPlatform.LocalScalarDataPath),
        };

        public LinuxPlatform() : base(
             underConstruction: new UnderConstructionFlags(
                supportsScalarUpgrade: false,
                supportsScalarConfig: true,
                supportsNuGetEncryption: false,
                supportsNuGetVerification: false))
        {
        }

        public override string Name { get => "Linux"; }
        public override ScalarPlatformConstants Constants { get; } = new LinuxPlatformConstants();
        public override IPlatformFileSystem FileSystem { get; } = new LinuxFileSystem();

        public override string ScalarConfigPath
        {
            get
            {
                string localConfigRoot;
                string localConfigRootError;

                if (!TryGetEnvironmentVariableBasePath(EnvironmentVariableBaseConfigPaths, out localConfigRoot, out localConfigRootError))
                {
                    throw new ArgumentException(localConfigRootError);
                }

                return Path.Combine(localConfigRoot, LocalScalarConfig.FileName);
            }
        }

        public override string GetOSVersionInformation()
        {
            ProcessResult result = ProcessHelper.Run("uname", args: "-srv", redirectOutput: true);
            return string.IsNullOrWhiteSpace(result.Output) ? result.Errors : result.Output;
        }

        public override string GetCommonAppDataRootForScalar()
        {
            return LinuxPlatform.GetDataRootForScalarImplementation();
        }

        public override string GetCommonAppDataRootForScalarComponent(string componentName)
        {
            return LinuxPlatform.GetDataRootForScalarComponentImplementation(componentName);
        }

        public override string GetSecureDataRootForScalar()
        {
            // SecureDataRoot is Windows only. On Linux, it is the same as CommoAppDataRoot
            return this.GetCommonAppDataRootForScalar();
        }

        public override string GetSecureDataRootForScalarComponent(string componentName)
        {
            // SecureDataRoot is Windows only. On Linux, it is the same as CommoAppDataRoot
            return this.GetCommonAppDataRootForScalarComponent(componentName);
        }

        public override string GetLogsDirectoryForGVFSComponent(string componentName)
        {
            return Path.Combine(this.GetCommonAppDataRootForScalarComponent(componentName), "Logs");
        }

        public override FileBasedLock CreateFileBasedLock(
            PhysicalFileSystem fileSystem,
            ITracer tracer,
            string lockPath)
        {
            return new LinuxFileBasedLock(fileSystem, tracer, lockPath);
        }

        public override string GetUpgradeProtectedDataDirectory()
        {
            return UpgradeProtectedDataDirectory;
        }

        public override string GetUpgradeHighestAvailableVersionDirectory()
        {
            return GetUpgradeHighestAvailableVersionDirectoryImplementation();
        }

        /// <summary>
        /// This is the directory in which the upgradelogs directory should go.
        /// There can be multiple logs directories, so here we return the containing
        /// directory.
        /// </summary>
        public override string GetUpgradeLogDirectoryParentDirectory()
        {
            return this.GetUpgradeNonProtectedDataDirectory();
        }

        public override bool TryGetDefaultLocalCacheRoot(string enlistmentRoot, out string localCacheRoot, out string localCacheRootError)
        {
            return TryGetEnvironmentVariableBasePath(EnvironmentVariableBaseCachePaths, out localCacheRoot, out localCacheRootError);
        }

        public override ProductUpgraderPlatformStrategy CreateProductUpgraderPlatformInteractions(
            PhysicalFileSystem fileSystem,
            ITracer tracer)
        {
            return new LinuxProductUpgraderPlatformStrategy(fileSystem, tracer);
        }

        public override void IsServiceInstalledAndRunning(string name, out bool installed, out bool running)
        {
            installed = false;
            running = false;
        }

        public override string GetTemplateHooksDirectory()
        {
            string gitExecPath = GitInstallation.GetInstalledGitBinPath();

            // Resolve symlinks
            string resolvedExecPath = NativeMethods.ResolveSymlink(gitExecPath);

            // Get the containing bin directory
            string gitBinDir = Path.GetDirectoryName(resolvedExecPath);

            // Compute the base installation path (../)
            string installBaseDir = Path.GetDirectoryName(gitBinDir);
            installBaseDir = Path.GetFullPath(installBaseDir);

            return Path.Combine(installBaseDir, ScalarConstants.InstalledGit.HookTemplateDir);
        }

        public class LinuxPlatformConstants : POSIXPlatformConstants
        {
            public override string InstallerExtension
            {
                get { return ".deb"; }
            }

            // TODO(Linux): determine installation location
            public override string ScalarBinDirectoryPath
            {
                get { return Path.Combine("/usr", "local", this.ScalarBinDirectoryName); }
            }

            public override string ScalarBinDirectoryName
            {
                get { return "scalar"; }
            }

            // Documented here (in the addressing section): https://www.unix.com/man-page/linux/7/unix/
            public override int MaxPipePathLength => 108;
        }

        private static class NativeMethods
        {
            // Definitions from
            // /Library/Developer/CommandLineTools/SDKs/LinuxOSX.sdk

            // stdlib.h
            [DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr realpath([In] IntPtr file_name, [In, Out] IntPtr resolved_name);

            public static string ResolveSymlink(string path)
            {
                // Defined in linux/limits.h
                const int PATH_MAX = 4096;

                IntPtr pathBuf = IntPtr.Zero;
                IntPtr resolvedBuf = IntPtr.Zero;

                try
                {
                    pathBuf = Marshal.StringToHGlobalAuto(path);
                    resolvedBuf = Marshal.AllocHGlobal(PATH_MAX + 1);
                    IntPtr result = realpath(pathBuf, resolvedBuf);

                    if (result == IntPtr.Zero)
                    {
                        // Failed!
                        return null;
                    }

                    return Marshal.PtrToStringUTF8(resolvedBuf);
                }
                finally
                {
                    if (pathBuf != IntPtr.Zero) Marshal.FreeHGlobal(pathBuf);
                    if (resolvedBuf != IntPtr.Zero) Marshal.FreeHGlobal(resolvedBuf);
                }
            }
        }
    }
}
