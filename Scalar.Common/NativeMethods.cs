using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Scalar.Common
{
    public static partial class NativeMethods
    {
        [Flags]
        public enum MoveFileFlags : uint
        {
            MoveFileReplaceExisting = 0x00000001,    // MOVEFILE_REPLACE_EXISTING
            MoveFileCopyAllowed = 0x00000002,        // MOVEFILE_COPY_ALLOWED
            MoveFileDelayUntilReboot = 0x00000004,   // MOVEFILE_DELAY_UNTIL_REBOOT
            MoveFileWriteThrough = 0x00000008,       // MOVEFILE_WRITE_THROUGH
            MoveFileCreateHardlink = 0x00000010,     // MOVEFILE_CREATE_HARDLINK
            MoveFileFailIfNotTrackable = 0x00000020, // MOVEFILE_FAIL_IF_NOT_TRACKABLE
        }

        public static void FlushFileBuffers(string path)
        {
            using (SafeFileHandle fileHandle = CreateFile(
                path,
                FileAccess.GENERIC_WRITE,
                FileShare.ReadWrite,
                IntPtr.Zero,
                FileMode.Open,
                FileAttributes.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero))
            {
                if (fileHandle.IsInvalid)
                {
                    ThrowLastWin32Exception($"Invalid handle for '{path}'");
                }

                if (!FlushFileBuffers(fileHandle))
                {
                    ThrowLastWin32Exception($"Failed to flush buffers for '{path}'");
                }
            }
        }

        public static void MoveFile(string existingFileName, string newFileName, MoveFileFlags flags)
        {
            if (!MoveFileEx(existingFileName, newFileName, (uint)flags))
            {
                ThrowLastWin32Exception($"Failed to move '{existingFileName}' to '{newFileName}'");
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool MoveFileEx(
            string existingFileName,
            string newFileName,
            uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushFileBuffers(SafeFileHandle hFile);
    }
}
