using System;
using System.Collections.Concurrent;
using System.Threading;
using Scalar.Common.Tracing;

namespace Scalar.Service
{
    public class ServiceTaskQueue : IDisposable
    {
        private readonly ITracer tracer;
        private BlockingCollection<IServiceTask> queue = new BlockingCollection<IServiceTask>();
        private IServiceTask currentTask;
        private Thread workerThread;

        public ServiceTaskQueue(ITracer tracer)
        {
            this.tracer = tracer;
            this.workerThread = new Thread(this.ExecuteTasksOnWorkerThread);
            this.workerThread.Name = nameof(ServiceTaskQueue);
            this.workerThread.IsBackground = true;
            this.workerThread.Start();
        }

        public bool TryEnqueue(IServiceTask step)
        {
            try
            {
                this.queue.Add(step);
                return true;
            }
            catch (InvalidOperationException)
            {
                // We called queue.CompleteAdding()
            }

            return false;
        }

        public void Stop()
        {
            this.queue.CompleteAdding();
            this.currentTask?.Stop();
        }

        public void Dispose()
        {
            if (this.workerThread != null)
            {
                // Wait for the working thread to finish before setting queue
                // to null to ensure that the worker does not hit a null
                // reference exception trying to access the queue
                this.workerThread.Join();
                this.workerThread = null;
            }

            if (this.queue != null)
            {
                this.queue.Dispose();
                this.queue = null;
            }
        }

        private void ExecuteTasksOnWorkerThread()
        {
            while (this.queue.TryTake(out this.currentTask, Timeout.Infinite) &&
                !this.queue.IsAddingCompleted)
            {
                try
                {
                    this.currentTask.Execute();
                }
                catch (Exception e)
                {
                    EventMetadata metadata = this.CreateEventMetadata(nameof(this.ExecuteTasksOnWorkerThread), e);
                    this.tracer.RelatedError(
                        metadata: metadata,
                        message: $"{nameof(this.ExecuteTasksOnWorkerThread)}: Unexpected Exception while running service tasks: {e.Message}");
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
