using Microsoft.Win32.SafeHandles;
using Scalar.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Scalar.Platform.Windows
{
    public partial class WindowsPlatform
    {
        private const int StillActive = 259; /* from Win32 STILL_ACTIVE */

        private enum StdHandle
        {
            Stdin = -10,
            Stdout = -11,
            Stderr = -12
        }

        private enum FileType : uint
        {
            Unknown = 0x0000,
            Disk = 0x0001,
            Char = 0x0002,
            Pipe = 0x0003,
            Remote = 0x8000,
        }

        public static bool IsElevatedImplementation()
        {
            using (WindowsIdentity id = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static bool IsProcessActiveImplementation(int processId, bool tryGetProcessById)
        {
            using (SafeFileHandle process = NativeMethods.OpenProcess(NativeMethods.ProcessAccessFlags.QueryLimitedInformation, false, processId))
            {
                if (!process.IsInvalid)
                {
                    uint exitCode;
                    if (NativeMethods.GetExitCodeProcess(process, out exitCode) && exitCode == StillActive)
                    {
                        return true;
                    }
                }
                else if (tryGetProcessById)
                {
                    // The process.IsInvalid may be true when the mount process doesn't have access to call
                    // OpenProcess for the specified processId. Fallback to slow way of finding process.
                    try
                    {
                        Process.GetProcessById(processId);
                        return true;
                    }
                    catch (ArgumentException)
                    {
                        return false;
                    }
                }

                return false;
            }
        }

        public static string GetDataRootForScalarImplementation()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolderOption.Create),
                "Scalar",
                "Data");
        }

        public static string GetDataRootForScalarComponentImplementation(string componentName)
        {
            return Path.Combine(GetDataRootForScalarImplementation(), componentName);
        }

        public static bool IsConsoleOutputRedirectedToFileImplementation()
        {
            return FileType.Disk == GetFileType(GetStdHandle(StdHandle.Stdout));
        }

        public static string GetUpgradeProtectedDataDirectoryImplementation()
        {
            return Path.Combine(GetDataRootForScalarImplementation(), ProductUpgraderInfo.UpgradeDirectoryName);
        }

        public static string GetUpgradeHighestAvailableVersionDirectoryImplementation()
        {
            return GetUpgradeProtectedDataDirectoryImplementation();
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(StdHandle std);

        [DllImport("kernel32.dll")]
        private static extern FileType GetFileType(IntPtr hdl);
    }
}
