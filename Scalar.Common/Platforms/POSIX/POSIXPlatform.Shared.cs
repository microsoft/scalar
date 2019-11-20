using Scalar.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Scalar.Platform.POSIX
{
    public abstract partial class POSIXPlatform
    {
        public static bool IsElevatedImplementation()
        {
            int euid = GetEuid();
            return euid == 0;
        }

        public static bool IsProcessActiveImplementation(int processId)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }

        public static string GetNamedPipeNameImplementation(string enlistmentRoot, string dotScalarRoot)
        {
            // Pipes are stored as files on POSIX, use a rooted pipe name to keep full control of the location of the file
            return Path.Combine(enlistmentRoot, dotScalarRoot, "Scalar_NetCorePipe");
        }

        public static bool IsConsoleOutputRedirectedToFileImplementation()
        {
            // TODO(#1355): Implement proper check
            return false;
        }

        [DllImport("libc", EntryPoint = "geteuid", SetLastError = true)]
        private static extern int GetEuid();
    }
}
