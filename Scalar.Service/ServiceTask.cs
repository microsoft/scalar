using System.Threading;

namespace Scalar.Service
{
    public abstract class ServiceTask
    {
        public ServiceTask()
        {
            this.TaskSignaled = new ManualResetEvent(initialState: false);
        }

        public ManualResetEvent TaskSignaled { get; private set; }

        public abstract void Execute();
        public abstract void Stop();
    }
}
