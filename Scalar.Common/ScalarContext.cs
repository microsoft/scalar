using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;

namespace Scalar.Common
{
    public class ScalarContext : IDisposable
    {
        private bool disposedValue = false;

        public ScalarContext(ITracer tracer, PhysicalFileSystem fileSystem, GitRepo repository, ScalarEnlistment enlistment)
        {
            this.Tracer = tracer;
            this.FileSystem = fileSystem;
            this.Enlistment = enlistment;
            this.Repository = repository;

            this.Unattended = ScalarEnlistment.IsUnattended(this.Tracer);
        }

        public ITracer Tracer { get; private set; }
        public PhysicalFileSystem FileSystem { get; private set; }
        public GitRepo Repository { get; private set; }
        public ScalarEnlistment Enlistment { get; private set; }
        public bool Unattended { get; private set; }

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.Repository.Dispose();
                    this.Tracer.Dispose();
                    this.Tracer = null;
                }

                this.disposedValue = true;
            }
        }
    }
}
