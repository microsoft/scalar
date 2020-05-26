using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using System;

namespace Scalar.UnitTests.Mock.FileSystem
{
    public class MockPlatformFileSystem : IPlatformFileSystem
    {
        public bool SupportsFileMode { get; } = true;

        public bool SupportsUntrackedCache { get; } = true;

        public void FlushFileBuffers(string path)
        {
            throw new NotSupportedException();
        }

        public void MoveAndOverwriteFile(string sourceFileName, string destinationFilename)
        {
            throw new NotSupportedException();
        }

        public bool TryGetNormalizedPath(string path, out string normalizedPath, out string errorMessage)
        {
            errorMessage = null;
            normalizedPath = path;
            return true;
        }

        public bool IsExecutable(string fileName)
        {
            throw new NotSupportedException();
        }

        public bool IsSocket(string fileName)
        {
            throw new NotSupportedException();
        }

        public bool TryCreateDirectoryWithAdminAndUserModifyPermissions(string directoryPath, out string error)
        {
            throw new NotSupportedException();
        }

        public bool TryCreateOrUpdateDirectoryToAdminModifyPermissions(ITracer tracer, string directoryPath, out string error)
        {
            throw new NotSupportedException();
        }

        public bool IsFileSystemSupported(string path, out string error)
        {
            error = null;
            return true;
        }
    }
}
