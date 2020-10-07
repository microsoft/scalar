using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using System;
using System.Runtime.InteropServices;

namespace Scalar.Platform.POSIX
{
    public abstract class POSIXFileSystem : IPlatformFileSystem
    {
        // https://github.com/dotnet/corefx/blob/103639b6ff5aa6ab6097f70732530e411817f09b/src/Common/src/CoreLib/Interop/Unix/System.Native/Interop.OpenFlags.cs#L12
        [Flags]
        public enum OpenFlags
        {
            // Access modes (mutually exclusive)
            O_RDONLY = 0x0000,
            O_WRONLY = 0x0001,
            O_RDWR = 0x0002,

            // Flags (combinable)
            O_CLOEXEC = 0x0010,
            O_CREAT = 0x0020,
            O_EXCL = 0x0040,
            O_TRUNC = 0x0080,
            O_SYNC = 0x0100,
        }

        public bool SupportsFileMode { get; } = true;

        public bool SupportsUntrackedCache { get; } = true;

        public void FlushFileBuffers(string path)
        {
            // TODO(#1057): Use native API to flush file
        }

        public void MoveAndOverwriteFile(string sourceFileName, string destinationFilename)
        {
            if (Rename(sourceFileName, destinationFilename) != 0)
            {
                NativeMethods.ThrowLastWin32Exception($"Failed to rename {sourceFileName} to {destinationFilename}");
            }
        }

        public bool TryGetNormalizedPath(string path, out string normalizedPath, out string errorMessage)
        {
            // TODO(#217): Properly determine normalized paths (e.g. across links)
            errorMessage = null;
            normalizedPath = path;
            return true;
        }

        public abstract bool IsExecutable(string fileName);

        public abstract bool IsSocket(string fileName);

        public bool TryCreateDirectoryWithAdminAndUserModifyPermissions(string directoryPath, out string error)
        {
            throw new NotImplementedException();
        }

        public bool TryCreateOrUpdateDirectoryToAdminModifyPermissions(ITracer tracer, string directoryPath, out string error)
        {
            throw new NotImplementedException();
        }

        public bool IsFileSystemSupported(string path, out string error)
        {
            return ScalarPlatform.Instance.IsFileSystemCaseSensitivitySupported(path, out error);
        }

        [DllImport("libc", EntryPoint = "rename", SetLastError = true)]
        private static extern int Rename(string oldPath, string newPath);
    }
}
