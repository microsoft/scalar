using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;

namespace Scalar.Common
{
    public class ScalarContext : IDisposable
    {
        private bool disposedValue = false;

        public ScalarContext(ITracer tracer, PhysicalFileSystem fileSystem, ScalarEnlistment enlistment)
        {
            this.Tracer = tracer;
            this.FileSystem = fileSystem;
            this.Enlistment = enlistment;

            this.Unattended = ScalarEnlistment.IsUnattended(this.Tracer);
        }

        public ITracer Tracer { get; private set; }
        public PhysicalFileSystem FileSystem { get; private set; }
        public ScalarEnlistment Enlistment { get; private set; }
        public bool Unattended { get; private set; }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.Tracer.Dispose();
                    this.Tracer = null;
                }

                this.disposedValue = true;
            }
        }
    }
}
