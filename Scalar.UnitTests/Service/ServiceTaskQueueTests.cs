using NUnit.Framework;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Maintenance;
using Scalar.Common.Tracing;
using Scalar.Service;
using Scalar.Tests.Should;
using Scalar.UnitTests.Mock.Common;
using System.Collections.Generic;
using System.Threading;

namespace Scalar.UnitTests.Service
{
    [TestFixture]
    public class ServiceTaskQueueTests
    {
        private int maxWaitTime = 500;
        private ReadyFileSystem fileSystem;
        private ScalarEnlistment enlistment;
        private ScalarContext context;
        private GitObjects gitObjects;

        [TestCase]
        public void ServiceTaskQueueHandlesTwoJobs()
        {
            this.TestSetup();

            TestServiceTask step1 = new TestServiceTask();
            TestServiceTask step2 = new TestServiceTask();

            ServiceTaskQueue queue = new ServiceTaskQueue(new MockTracer());

            queue.TryEnqueue(step1);
            queue.TryEnqueue(step2);

            step1.EventTriggered.WaitOne(this.maxWaitTime).ShouldBeTrue();
            step2.EventTriggered.WaitOne(this.maxWaitTime).ShouldBeTrue();

            queue.Stop();

            step1.NumberOfExecutions.ShouldEqual(1);
            step2.NumberOfExecutions.ShouldEqual(1);
        }

        [TestCase]
        public void ServiceTaskQueueStopSuceedsWhenQueueIsEmpty()
        {
            this.TestSetup();

            ServiceTaskQueue queue = new ServiceTaskQueue(new MockTracer());

            queue.Stop();

            TestServiceTask step = new TestServiceTask();
            queue.TryEnqueue(step).ShouldEqual(false);
        }

        [TestCase]
        public void ServiceTaskQueueStopsJob()
        {
            this.TestSetup();

            ServiceTaskQueue queue = new ServiceTaskQueue(new MockTracer());

            // This step stops the queue after the step is started,
            // then checks if Stop() was called.
            WatchForStopTask watchForStop = new WatchForStopTask(queue);

            queue.TryEnqueue(watchForStop);
            watchForStop.EventTriggered.WaitOne(this.maxWaitTime).ShouldBeTrue();
            watchForStop.SawStopping.ShouldBeTrue();

            // Ensure we don't start a job after the Stop() call
            TestServiceTask watchForStart = new TestServiceTask();
            queue.TryEnqueue(watchForStart).ShouldBeFalse();

            // This only ensures the event didn't happen within maxWaitTime
            watchForStart.EventTriggered.WaitOne(this.maxWaitTime).ShouldBeFalse();

            queue.Stop();
        }

        private void TestSetup()
        {
            ITracer tracer = new MockTracer();
            this.enlistment = new MockScalarEnlistment();

            // We need to have the EnlistmentRoot and GitObjectsRoot available for jobs to run
            this.fileSystem = new ReadyFileSystem(new string[]
            {
                this.enlistment.EnlistmentRoot,
                this.enlistment.GitObjectsRoot
            });

            this.context = new ScalarContext(tracer, this.fileSystem, this.enlistment);
            this.gitObjects = new MockPhysicalGitObjects(tracer, this.fileSystem, this.enlistment, null);
        }

        public class ReadyFileSystem : PhysicalFileSystem
        {
            public ReadyFileSystem(IEnumerable<string> paths)
            {
                this.Paths = new HashSet<string>(paths);
            }

            public HashSet<string> Paths { get; }

            public override bool DirectoryExists(string path)
            {
                return this.Paths.Contains(path);
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
