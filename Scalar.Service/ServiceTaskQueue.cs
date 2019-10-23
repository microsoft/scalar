using System;
using System.Collections.Concurrent;
using System.Threading;
using Scalar.Common.Tracing;

namespace Scalar.Service
{
    public class ServiceTaskQueue
    {
        private readonly object queueLock = new object();
        private readonly ITracer tracer;
        private BlockingCollection<ServiceTask> queue = new BlockingCollection<ServiceTask>();
        private ServiceTask currentTask;

        public ServiceTaskQueue(ITracer tracer)
        {
            this.tracer = tracer;
            Thread worker = new Thread(() => this.RunQueue());
            worker.Name = "MaintenanceWorker";
            worker.IsBackground = true;
            worker.Start();
        }

        public bool TryEnqueue(ServiceTask step)
        {
            try
            {
                lock (this.queueLock)
                {
                    if (this.queue == null)
                    {
                        return false;
                    }

                    this.queue.Add(step);
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                // We called queue.CompleteAdding()
            }

            return false;
        }

        public void Stop()
        {
            lock (this.queueLock)
            {
                this.queue?.CompleteAdding();
            }

            this.currentTask?.Stop();
        }

        private void RunQueue()
        {
            while (true)
            {
                // We cannot take the lock here, as TryTake is blocking.
                // However, this is the place to set 'this.queue' to null.
                if (!this.queue.TryTake(out this.currentTask, Timeout.Infinite)
                    || this.queue.IsAddingCompleted)
                {
                    lock (this.queueLock)
                    {
                        // A stop was requested
                        this.queue?.Dispose();
                        this.queue = null;
                        return;
                    }
                }

                try
                {
                    this.currentTask.Execute();
                }
                catch (Exception e)
                {
                    EventMetadata metadata = this.CreateEventMetadata(nameof(this.RunQueue), e);
                    this.tracer.RelatedError(
                        metadata: metadata,
                        message: $"{nameof(this.RunQueue)}: Unexpected Exception while running service tasks: {e.Message}");
                }
            }
        }

        private EventMetadata CreateEventMetadata(string methodName, Exception exception = null)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add("Area", nameof(ServiceTaskQueue));
            metadata.Add("Method", methodName);
            if (exception != null)
            {
                metadata.Add("ExceptionMessage", exception.Message);
                metadata.Add("StackTrace", exception.StackTrace);
            }

            return metadata;
        }
    }
}
