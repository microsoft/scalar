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

namespace Scalar.Platform.Mac
{
    public partial class MacPlatform : POSIXPlatform
    {
        private const string UpgradeProtectedDataDirectory = "/usr/local/scalar_upgrader";
        private static readonly EnvironmentVariableBasePath[] EnvironmentVariableBaseCachePaths = new[] {
            new EnvironmentVariableBasePath(
                ScalarConstants.POSIXPlatform.EnvironmentVariables.LocalUserFolder,
                ScalarConstants.DefaultScalarCacheFolderName),
        };
        protected static readonly EnvironmentVariableBasePath[] EnvironmentVariableBaseDataPaths = new[] {
            new EnvironmentVariableBasePath(
                ScalarConstants.POSIXPlatform.EnvironmentVariables.LocalUserFolder,
                ScalarConstants.MacPlatform.LocalScalarDataPath),
        };

        public MacPlatform() : base(
             underConstruction: new UnderConstructionFlags(
                supportsScalarUpgrade: true,
                supportsScalarConfig: true,
                supportsNuGetEncryption: false,
                supportsNuGetVerification: false))
        {
        }

        public override string Name { get => "macOS"; }
        public override ScalarPlatformConstants Constants { get; } = new MacPlatformConstants();
        public override IPlatformFileSystem FileSystem { get; } = new MacFileSystem();

        public override string ScalarConfigPath
        {
            get
            {
                return Path.Combine(this.Constants.ScalarBinDirectoryPath, LocalScalarConfig.FileName);
            }
        }

        public override string GetOSVersionInformation()
        {
            ProcessResult result = ProcessHelper.Run("sw_vers", args: string.Empty, redirectOutput: true);
            return string.IsNullOrWhiteSpace(result.Output) ? result.Errors : result.Output;
        }

        public override string GetCommonAppDataRootForScalar()
        {
            return MacPlatform.GetDataRootForScalarImplementation();
        }

        public override string GetCommonAppDataRootForScalarComponent(string componentName)
        {
            return MacPlatform.GetDataRootForScalarComponentImplementation(componentName);
        }

        public override string GetSecureDataRootForScalar()
        {
            // SecureDataRoot is Windows only. On the Mac, it is the same as CommoAppDataRoot
            return this.GetCommonAppDataRootForScalar();
        }

        public override string GetSecureDataRootForScalarComponent(string componentName)
        {
            // SecureDataRoot is Windows only. On the Mac, it is the same as CommoAppDataRoot
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
            return new MacFileBasedLock(fileSystem, tracer, lockPath);
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

        public override Dictionary<string, string> GetPhysicalDiskInfo(string path, bool sizeStatsOnly)
        {
            // DiskUtil will return disk statistics in xml format
            ProcessResult processResult = ProcessHelper.Run("diskutil", "info -plist /", true);
            Dictionary<string, string> result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(processResult.Output))
            {
                result.Add("DiskUtilError", processResult.Errors);
                return result;
            }

            try
            {
                // Parse the XML looking for FilesystemType
                XDocument xmlDoc = XDocument.Parse(processResult.Output);
                XElement filesystemTypeValue = xmlDoc.XPathSelectElement("plist/dict/key[text()=\"FilesystemType\"]")?.NextNode as XElement;
                result.Add("FileSystemType", filesystemTypeValue != null ? filesystemTypeValue.Value : "Not Found");
            }
            catch (XmlException ex)
            {
                result.Add("DiskUtilError", ex.ToString());
            }

            return result;
        }

        public override bool TryGetDefaultLocalCacheRoot(string enlistmentRoot, out string localCacheRoot, out string localCacheRootError)
        {
            return TryGetEnvironmentVariableBasePath(EnvironmentVariableBaseCachePaths, out localCacheRoot, out localCacheRootError);
        }

        public override ProductUpgraderPlatformStrategy CreateProductUpgraderPlatformInteractions(
            PhysicalFileSystem fileSystem,
            ITracer tracer)
        {
            return new MacProductUpgraderPlatformStrategy(fileSystem, tracer);
        }

        public override void IsServiceInstalledAndRunning(string name, out bool installed, out bool running)
        {
            string currentUser = this.GetCurrentUser();
            MacDaemonController macDaemonController = new MacDaemonController(new ProcessRunnerImpl());
            List<MacDaemonController.DaemonInfo> daemons;
            if (!macDaemonController.TryGetDaemons(currentUser, out daemons, out string error))
            {
                installed = false;
                running = false;
            }

            MacDaemonController.DaemonInfo scalarService = daemons.FirstOrDefault(sc => string.Equals(sc.Name, "org.scalar.service"));
            installed = scalarService != null;
            running = installed && scalarService.IsRunning;
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

        public class MacPlatformConstants : POSIXPlatformConstants
        {
            public override string InstallerExtension
            {
                get { return ".dmg"; }
            }

            public override string ScalarBinDirectoryPath
            {
                get { return Path.Combine("/usr", "local", this.ScalarBinDirectoryName); }
            }

            public override string ScalarBinDirectoryName
            {
                get { return "scalar"; }
            }

            // Documented here (in the addressing section): https://www.unix.com/man-page/mojave/4/unix/
            public override int MaxPipePathLength => 104;
        }

        private static class NativeMethods
        {
            // Definitions from
            // /Library/Developer/CommandLineTools/SDKs/MacOSX.sdk

            // stdlib.h
            [DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr realpath([In] IntPtr file_name, [In, Out] IntPtr resolved_name);

            public static string ResolveSymlink(string path)
            {
                // Defined in sys/syslimits.h
                const int PATH_MAX = 1024;

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
