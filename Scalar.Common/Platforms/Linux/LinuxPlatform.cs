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
    public class LinuxPlatform : POSIXPlatform
    {
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
        private static readonly EnvironmentVariableBasePath[] EnvironmentVariableBaseConfigPaths = new[] {
            new EnvironmentVariableBasePath(
                ScalarConstants.LinuxPlatform.EnvironmentVariables.LocalUserConfigFolder,
                ScalarConstants.LinuxPlatform.LocalScalarFolderName),
            new EnvironmentVariableBasePath(
                ScalarConstants.POSIXPlatform.EnvironmentVariables.LocalUserFolder,
                ScalarConstants.LinuxPlatform.LocalScalarConfigPath),
        };
        private static readonly EnvironmentVariableBasePath[] EnvironmentVariableBaseDataPaths = new[] {
            new EnvironmentVariableBasePath(
                ScalarConstants.LinuxPlatform.EnvironmentVariables.LocalUserDataFolder,
                ScalarConstants.LinuxPlatform.LocalScalarFolderName),
            new EnvironmentVariableBasePath(
                ScalarConstants.POSIXPlatform.EnvironmentVariables.LocalUserFolder,
                ScalarConstants.LinuxPlatform.LocalScalarDataPath),
        };

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

        public override FileBasedLock CreateFileBasedLock(
            PhysicalFileSystem fileSystem,
            ITracer tracer,
            string lockPath)
        {
            return new LinuxFileBasedLock(fileSystem, tracer, lockPath);
        }

        public override bool TryGetDefaultLocalCacheRoot(string enlistmentRoot, out string localCacheRoot, out string localCacheRootError)
        {
            return TryGetEnvironmentVariableBasePath(EnvironmentVariableBaseCachePaths, out localCacheRoot, out localCacheRootError);
        }

        public override void IsServiceInstalledAndRunning(string name, out bool installed, out bool running)
        {
            installed = false;
            running = false;
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

            // Documented here (in the addressing section): https://www.unix.com/man-page/linux/7/unix/
            public override int MaxPipePathLength => 108;

            public override bool CaseSensitiveFileSystem => true;
        }

        // Defined in linux/limits.h
        protected override int MaxPathLength => 4096;

        protected override bool TryGetDefaultLocalDataRoot(out string localDataRoot, out string localDataRootError)
        {
            return TryGetEnvironmentVariableBasePath(EnvironmentVariableBaseDataPaths, out localDataRoot, out localDataRootError);
        }
    }
}
