using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.Tracing;
using System.Diagnostics;
using System.IO;

namespace Scalar.UnitTests.Mock.Common
{
    public class MockPhysicalGitObjects : GitObjects
    {
        public MockPhysicalGitObjects(ITracer tracer, PhysicalFileSystem fileSystem, Enlistment enlistment, GitObjectsHttpRequestor objectRequestor)
            : base(tracer, enlistment, objectRequestor, fileSystem)
        {
        }

        public override string WriteLooseObject(Stream responseStream, string sha, byte[] sharedBuf = null)
        {
            using (StreamReader reader = new StreamReader(responseStream))
            {
                // Return "file contents" as "file name". Weird, but proves we got the right thing.
                return reader.ReadToEnd();
            }
        }

        public override string WriteTempPackFile(Stream stream)
        {
            Debug.Assert(stream != null, "WriteTempPackFile should not receive a null stream");

            using (stream)
            using (StreamReader reader = new StreamReader(stream))
            {
                // Return "file contents" as "file name". Weird, but proves we got the right thing.
                return reader.ReadToEnd();
            }
        }

        public override GitProcess.Result IndexTempPackFile(string tempPackPath, GitProcess gitProcess = null)
        {
            return new GitProcess.Result(string.Empty, "TestFailure", GitProcess.Result.GenericFailureCode);
        }
    }
}
