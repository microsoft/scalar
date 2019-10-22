using System;
using System.Collections.Generic;
using System.Threading;
using Scalar.Common.Tracing;

namespace Scalar.Service
{
    public class ServiceTaskRunner
    {
        private readonly ManualResetEvent[] taskEvents;
        private readonly ManualResetEvent shutdownEvent;
        private readonly ICollection<ServiceTask> serviceTasks;
        private readonly ITracer tracer;
        private ServiceTask currentTask;

        public ServiceTaskRunner(ITracer tracer, ICollection<ServiceTask> serviceTasks)
        {
            if (serviceTasks.Count == 0)
            {
                throw new ArgumentException("There must be at least one service task to run", nameof(serviceTasks));
            }

            this.tracer = tracer;
            this.shutdownEvent = new ManualResetEvent(initialState: false);

            this.serviceTasks = serviceTasks;
            this.taskEvents = new ManualResetEvent[serviceTasks.Count + 1]; // +1 for shutdown event

            int taskEventIndex = 0;
            foreach (ServiceTask task in serviceTasks)
            {
                this.taskEvents[taskEventIndex] = task.TaskSignaled;
                ++taskEventIndex;
            }

            this.taskEvents[taskEventIndex] = this.shutdownEvent;

            Thread worker = new Thread(() => this.RunTasks());
            worker.Name = "MaintenanceWorker";
            worker.IsBackground = true;
            worker.Start();
        }

        public void Stop()
        {
            this.shutdownEvent.Set();
            this.currentTask?.Stop();
        }

        private void RunTasks()
        {
            this.tracer.RelatedInfo($"{nameof(ServiceTaskRunner)}_{nameof(this.RunTasks)}: Waiting for tasks");

            while (true)
            {
                WaitHandle.WaitAny(this.taskEvents);

                foreach (ServiceTask task in this.serviceTasks)
                {
                    // No need to check shutdownEvent outside of loop, there is always at
                    // least one task in this.serviceTasks
                    if (this.shutdownEvent.WaitOne(0))
                    {
                        this.tracer.RelatedInfo($"{nameof(ServiceTaskRunner)}_{nameof(this.RunTasks)}: Shutting down");
                        return;
                    }

                    if (task.TaskSignaled.WaitOne(0))
                    {
                        this.currentTask = task;
                        task.Execute();
                        task.TaskSignaled.Reset();
                        this.currentTask = null;
                    }
                }
            }
        }
    }
}
