using NUnit.Framework;
using Scalar.Service;
using Scalar.Tests.Should;
using Scalar.UnitTests.Mock.Common;
using System.Threading;

namespace Scalar.UnitTests.Service
{
    [TestFixture]
    public class ServiceTaskQueueTests
    {
        private int maxWaitTime = 500;

        [TestCase]
        public void ServiceTaskQueueHandlesTwoJobs()
        {
            TestServiceTask step1 = new TestServiceTask();
            TestServiceTask step2 = new TestServiceTask();

            using (ServiceTaskQueue queue = new ServiceTaskQueue(new MockTracer()))
            {
                queue.TryEnqueue(step1);
                queue.TryEnqueue(step2);

                step1.EventTriggered.WaitOne(this.maxWaitTime).ShouldBeTrue();
                step2.EventTriggered.WaitOne(this.maxWaitTime).ShouldBeTrue();

                queue.Stop();

                step1.NumberOfExecutions.ShouldEqual(1);
                step2.NumberOfExecutions.ShouldEqual(1);
            }
        }

        [TestCase]
        public void ServiceTaskQueueStopSuceedsWhenQueueIsEmpty()
        {
            using (ServiceTaskQueue queue = new ServiceTaskQueue(new MockTracer()))
            {
                queue.Stop();

                TestServiceTask step = new TestServiceTask();
                queue.TryEnqueue(step).ShouldEqual(false);
            }
        }

        [TestCase]
        public void ServiceTaskQueueStopsJob()
        {
            using (ServiceTaskQueue queue = new ServiceTaskQueue(new MockTracer()))
            {
                // This step stops the queue after the step is started,
                // then checks if Stop() was called.
                WatchForStopTask watchForStop = new WatchForStopTask(queue);

                queue.TryEnqueue(watchForStop).ShouldBeTrue();
                watchForStop.EventTriggered.WaitOne(this.maxWaitTime).ShouldBeTrue();
                watchForStop.SawStopping.ShouldBeTrue();

                // Ensure we don't start a job after the Stop() call
                TestServiceTask watchForStart = new TestServiceTask();
                queue.TryEnqueue(watchForStart).ShouldBeFalse();

                // This only ensures the event didn't happen within maxWaitTime
                watchForStart.EventTriggered.WaitOne(this.maxWaitTime).ShouldBeFalse();

                queue.Stop();
            }
        }

        public class TestServiceTask : IServiceTask
        {
            public TestServiceTask()
            {
                this.EventTriggered = new ManualResetEvent(initialState: false);
            }

            public ManualResetEvent EventTriggered { get; set; }
            public int NumberOfExecutions { get; set; }

            public void Execute()
            {
                this.NumberOfExecutions++;
                this.EventTriggered.Set();
            }

            public void Stop()
            {
            }
        }

        private class WatchForStopTask : IServiceTask
        {
            public WatchForStopTask(ServiceTaskQueue queue)
            {
                this.Queue = queue;
                this.EventTriggered = new ManualResetEvent(false);
            }

            public ServiceTaskQueue Queue { get; set; }

            public bool SawStopping { get; private set; }

            public ManualResetEvent EventTriggered { get; private set; }

            public void Execute()
            {
                this.Queue.Stop();
                this.EventTriggered.Set();
            }

            public void Stop()
            {
                this.SawStopping = true;
            }
        }
    }
}
