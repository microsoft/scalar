using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Maintenance;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly PhysicalFileSystem fileSystem;
        private readonly IScalarVerbRunner scalarVerb;
        private readonly IScalarRepoRegistry repoRegistry;
        private ServiceTaskQueue taskQueue;
        private List<Timer> taskTimers;

        public MaintenanceTaskScheduler(
            ITracer tracer,
            PhysicalFileSystem fileSystem,
            IScalarVerbRunner scalarVerb,
            IScalarRepoRegistry repoRegistry)
        {
            this.tracer = tracer;
            this.fileSystem = fileSystem;
            this.scalarVerb = scalarVerb;
            this.repoRegistry = repoRegistry;
            this.taskTimers = new List<Timer>();
            this.taskQueue = new ServiceTaskQueue(this.tracer);
            this.ScheduleRecurringTasks();
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

        private void ScheduleRecurringTasks()
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
                        this.fileSystem,
                        this.scalarVerb,
                        this.repoRegistry,
                        this,
                        schedule.Task)),
                state: null,
                dueTime: schedule.DueTime,
                period: schedule.Period));
            }
        }

        internal class MaintenanceTask : IServiceTask
        {
            private readonly ITracer tracer;
            private readonly PhysicalFileSystem fileSystem;
            private readonly IScalarVerbRunner scalarVerb;
            private readonly IScalarRepoRegistry repoRegistry;
            private readonly IRegisteredUserStore registeredUserStore;
            private readonly MaintenanceTasks.Task task;

            public MaintenanceTask(
                ITracer tracer,
                PhysicalFileSystem fileSystem,
                IScalarVerbRunner scalarVerb,
                IScalarRepoRegistry repoRegistry,
                IRegisteredUserStore registeredUserStore,
                MaintenanceTasks.Task task)
            {
                this.tracer = tracer;
                this.fileSystem = fileSystem;
                this.scalarVerb = scalarVerb;
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

                    this.RunMaintenanceTaskForRepos(registeredUser);
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

            public void RunMaintenanceTaskForRepos(UserAndSession registeredUser)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add(nameof(this.task), MaintenanceTasks.GetVerbTaskName(this.task));
                metadata.Add(nameof(registeredUser.UserId), registeredUser.UserId);
                metadata.Add(nameof(registeredUser.SessionId), registeredUser.SessionId);

                int reposForUserCount = 0;
                string rootPath;
                string errorMessage;

                foreach (ScalarRepoRegistration repoRegistration in this.repoRegistry.GetRegisteredReposForUser(registeredUser.UserId))
                {
                    ++reposForUserCount;

                    rootPath = Path.GetPathRoot(repoRegistration.NormalizedRepoRoot);

                    metadata[nameof(repoRegistration.NormalizedRepoRoot)] = repoRegistration.NormalizedRepoRoot;
                    metadata[nameof(rootPath)] = rootPath;
                    metadata.Remove(nameof(errorMessage));

                    if (!string.IsNullOrWhiteSpace(rootPath) && !this.fileSystem.DirectoryExists(rootPath))
                    {
                        // If the volume does not exist we'll assume the drive was removed or is encrypted,
                        // and we'll leave the repo in the registry (but we won't run maintenance on it).
                        this.tracer.RelatedEvent(
                            EventLevel.Informational,
                            $"{nameof(this.RunMaintenanceTaskForRepos)}_SkippedRepoWithMissingVolume",
                            metadata);

                        continue;
                    }

                    if (!this.fileSystem.DirectoryExists(repoRegistration.NormalizedRepoRoot))
                    {
                        // The repo is no longer on disk (but its volume is present)
                        // Unregister the repo
                        if (this.repoRegistry.TryUnregisterRepo(repoRegistration.NormalizedRepoRoot, out errorMessage))
                        {
                            this.tracer.RelatedEvent(
                                EventLevel.Informational,
                                $"{nameof(this.RunMaintenanceTaskForRepos)}_RemovedMissingRepo",
                                metadata);
                        }
                        else
                        {
                            metadata[nameof(errorMessage)] = errorMessage;
                            this.tracer.RelatedEvent(
                                EventLevel.Informational,
                                $"{nameof(this.RunMaintenanceTaskForRepos)}_FailedToRemoveRepo",
                                metadata);
                        }

                        continue;
                    }

                    this.tracer.RelatedEvent(
                                EventLevel.Informational,
                                $"{nameof(this.RunMaintenanceTaskForRepos)}_CallingMaintenance",
                                metadata);

                    this.scalarVerb.CallMaintenance(this.task, repoRegistration.NormalizedRepoRoot, registeredUser.SessionId);
                }

                if (reposForUserCount == 0)
                {
                    metadata.Add(TracingConstants.MessageKey.InfoMessage, "No registered repos for user");
                    this.tracer.RelatedEvent(
                        EventLevel.Informational,
                        $"{nameof(this.RunMaintenanceTaskForRepos)}_NoRepos",
                        metadata);
                }
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
    }
}
