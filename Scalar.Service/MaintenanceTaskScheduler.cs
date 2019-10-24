using Scalar.Common;
using Scalar.Common.Maintenance;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Scalar.Service
{
    public class MaintenanceTaskScheduler : IDisposable, IRegisteredUserStore
    {
        private readonly TimeSpan looseObjectsDueTime = TimeSpan.FromMinutes(5);
        private readonly TimeSpan looseObjectsPeriod = TimeSpan.FromHours(6);

        private readonly TimeSpan packfileDueTime = TimeSpan.FromMinutes(30);
        private readonly TimeSpan packfilePeriod = TimeSpan.FromHours(12);

        private readonly TimeSpan commitGraphDueTime = TimeSpan.FromMinutes(15);
        private readonly TimeSpan commitGraphPeriod = TimeSpan.FromHours(1);

        private readonly TimeSpan fetchCommitsAndTreesPeriod = TimeSpan.FromMinutes(15);

        private readonly ITracer tracer;
        private ServiceTaskQueue taskQueue;
        private List<Timer> taskTimers;

        public MaintenanceTaskScheduler(ITracer tracer, IRepoRegistry repoRegistry)
        {
            this.tracer = tracer;
            this.taskTimers = new List<Timer>();
            this.taskQueue = new ServiceTaskQueue(this.tracer);
            this.ScheduleRecurringTasks(repoRegistry);
        }

        public UserAndSession RegisteredUser { get; private set; }

        public void RegisterUser(UserAndSession user)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add(nameof(user.UserId), user.UserId);
            metadata.Add(nameof(user.SessionId), user.SessionId);
            metadata.Add(
                TracingConstants.MessageKey.InfoMessage,
                $"{nameof(MaintenanceTaskScheduler)}: Registering user");
            this.tracer.RelatedEvent(EventLevel.Informational, nameof(this.RegisterUser), metadata);

            this.RegisteredUser = user;
        }

        public void Dispose()
        {
            this.taskQueue.Stop();
            foreach (Timer timer in this.taskTimers)
            {
                using (ManualResetEvent timerDisposed = new ManualResetEvent(initialState: false))
                {
                    timer.Dispose(timerDisposed);
                    timerDisposed.WaitOne();
                }
            }

            this.taskQueue.Dispose();
            this.taskQueue = null;
            this.taskTimers = null;
        }

        private void ScheduleRecurringTasks(IRepoRegistry repoRegistry)
        {
            if (ScalarEnlistment.IsUnattended(this.tracer))
            {
                this.tracer.RelatedInfo($"{nameof(this.ScheduleRecurringTasks)}: Skipping maintenance tasks due to running unattended");
                return;
            }

            List<MaintenanceSchedule> taskSchedules = new List<MaintenanceSchedule>()
            {
                new MaintenanceSchedule(
                    MaintenanceTasks.Task.FetchCommitsAndTrees,
                    dueTime: this.fetchCommitsAndTreesPeriod,
                    period: this.fetchCommitsAndTreesPeriod),
                new MaintenanceSchedule(
                    MaintenanceTasks.Task.LooseObjects,
                    dueTime: this.looseObjectsDueTime,
                    period: this.looseObjectsPeriod),
                new MaintenanceSchedule(
                    MaintenanceTasks.Task.PackFiles,
                    dueTime: this.packfileDueTime,
                    period: this.packfilePeriod),
                new MaintenanceSchedule(
                    MaintenanceTasks.Task.CommitGraph,
                    dueTime: this.commitGraphDueTime,
                    period: this.commitGraphPeriod),
            };

            foreach (MaintenanceSchedule schedule in taskSchedules)
            {
                this.taskTimers.Add(new Timer(
                (state) => this.taskQueue.TryEnqueue(
                    new MaintenanceTask(
                        this.tracer,
                        repoRegistry,
                        this,
                        schedule.Task)),
                state: null,
                dueTime: schedule.DueTime,
                period: schedule.Period));
            }
        }

        private class MaintenanceSchedule
        {
            public MaintenanceSchedule(MaintenanceTasks.Task task, TimeSpan dueTime, TimeSpan period)
            {
                this.Task = task;
                this.DueTime = dueTime;
                this.Period = period;
            }

            public MaintenanceTasks.Task Task { get; }
            public TimeSpan DueTime { get; }
            public TimeSpan Period { get; }
        }

        private class MaintenanceTask : IServiceTask
        {
            private readonly MaintenanceTasks.Task task;
            private readonly IRepoRegistry repoRegistry;
            private readonly ITracer tracer;
            private readonly IRegisteredUserStore registeredUserStore;

            public MaintenanceTask(
                ITracer tracer,
                IRepoRegistry repoRegistry,
                IRegisteredUserStore registeredUserStore,
                MaintenanceTasks.Task task)
            {
                this.tracer = tracer;
                this.repoRegistry = repoRegistry;
                this.registeredUserStore = registeredUserStore;
                this.task = task;
            }

            public void Execute()
            {
                UserAndSession registeredUser = this.registeredUserStore.RegisteredUser;
                if (registeredUser != null)
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add(nameof(registeredUser.UserId), registeredUser.UserId);
                    metadata.Add(nameof(registeredUser.SessionId), registeredUser.SessionId);
                    metadata.Add(nameof(this.task), this.task);
                    metadata.Add(TracingConstants.MessageKey.InfoMessage, "Executing maintenance task");
                    this.tracer.RelatedEvent(
                        EventLevel.Informational,
                        $"{nameof(MaintenanceTaskScheduler)}.{nameof(this.Execute)}",
                        metadata);

                    this.repoRegistry.RunMaintenanceTaskForRepos(
                        this.task,
                        registeredUser.UserId,
                        registeredUser.SessionId);
                }
                else
                {
                    this.tracer.RelatedInfo($"{nameof(MaintenanceTask)}: Skipping '{this.task}', no registered user");
                }
            }

            public void Stop()
            {
                // TODO: #185 - Kill the currently running maintenance verb
            }
        }
    }
}
