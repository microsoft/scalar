using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.Tracing;

namespace Scalar.Common.Prefetch.Git
{
    public class PrefetchGitObjects : GitObjects
    {
        public PrefetchGitObjects(ITracer tracer, Enlistment enlistment, GitObjectsHttpRequestor objectRequestor, PhysicalFileSystem fileSystem = null) : base(tracer, enlistment, objectRequestor, fileSystem)
        {
        }
    }
}
