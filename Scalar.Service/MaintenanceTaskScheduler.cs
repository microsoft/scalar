using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Maintenance;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private readonly TimeSpan configDueTime = TimeSpan.FromSeconds(0);
        private readonly TimeSpan configPeriod = TimeSpan.FromHours(24);

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

        public void ScheduleRecurringTasks()
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
                new MaintenanceSchedule(
                    MaintenanceTasks.Task.Config,
                    dueTime: this.configDueTime,
                    period: this.configPeriod,
                    ignorePause: true),
            };

            foreach (MaintenanceSchedule schedule in taskSchedules)
            {
                this.taskTimers.Add(new Timer(
                (state) =>
                {
                    this.taskQueue.TryEnqueue(
                        new MaintenanceTask(
                            this.tracer,
                            this.fileSystem,
                            this.scalarVerb,
                            this.repoRegistry,
                            this,
                            schedule.Task,
                            schedule.IgnorePause));
                },
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
            private readonly bool ignorePause;

            public MaintenanceTask(
                ITracer tracer,
                PhysicalFileSystem fileSystem,
                IScalarVerbRunner scalarVerb,
                IScalarRepoRegistry repoRegistry,
                IRegisteredUserStore registeredUserStore,
                MaintenanceTasks.Task task,
                bool ignorePause = true)
            {
                this.tracer = tracer;
                this.fileSystem = fileSystem;
                this.scalarVerb = scalarVerb;
                this.repoRegistry = repoRegistry;
                this.registeredUserStore = registeredUserStore;
                this.task = task;
                this.ignorePause = ignorePause;
            }

            public void Execute()
            {
                UserAndSession registeredUser = this.registeredUserStore.RegisteredUser;
                if (registeredUser != null)
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add(nameof(registeredUser.UserId), registeredUser.UserId);
                    metadata.Add(nameof(registeredUser.SessionId), registeredUser.SessionId);
                    metadata.Add(nameof(this.task), this.task.ToString());
                    metadata.Add(TracingConstants.MessageKey.InfoMessage, "Executing maintenance task");
                    using (ITracer activity = this.tracer.StartActivity($"{nameof(MaintenanceTask)}.{nameof(this.Execute)}", EventLevel.Informational, metadata))
                    {
                        this.RunMaintenanceTaskForRepos(registeredUser);
                    }
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

            private void RunMaintenanceTaskForRepos(UserAndSession registeredUser)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add(nameof(this.task), MaintenanceTasks.GetVerbTaskName(this.task));
                metadata.Add(nameof(registeredUser.UserId), registeredUser.UserId);
                metadata.Add(nameof(registeredUser.SessionId), registeredUser.SessionId);

                int reposSkipped = 0;
                int reposSuccessfullyRemoved = 0;
                int repoRemovalFailures = 0;
                int reposMaintained = 0;
                int reposInRegistryForUser = 0;
                bool maintenancePaused = false;

                string rootPath;
                string errorMessage;
                string traceMessage = null;

                IEnumerable<ScalarRepoRegistration> reposForUser = this.repoRegistry.GetRegisteredRepos().Where(
                    x => x.UserId.Equals(registeredUser.UserId, StringComparison.InvariantCultureIgnoreCase));

                foreach (ScalarRepoRegistration repoRegistration in reposForUser)
                {
                    ++reposInRegistryForUser;

                    if (maintenancePaused || this.IsMaintenancePaused(out traceMessage))
                    {
                        metadata[nameof(traceMessage)] = traceMessage;
                        maintenancePaused = true;
                        ++reposSkipped;
                        continue;
                    }

                    rootPath = Path.GetPathRoot(repoRegistration.NormalizedRepoRoot);

                    metadata[nameof(repoRegistration.NormalizedRepoRoot)] = repoRegistration.NormalizedRepoRoot;
                    metadata[nameof(rootPath)] = rootPath;
                    metadata.Remove(nameof(errorMessage));

                    if (!string.IsNullOrWhiteSpace(rootPath) && !this.fileSystem.DirectoryExists(rootPath))
                    {
                        ++reposSkipped;

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
                            ++reposSuccessfullyRemoved;
                            this.tracer.RelatedEvent(
                                EventLevel.Informational,
                                $"{nameof(this.RunMaintenanceTaskForRepos)}_RemovedMissingRepo",
                                metadata);
                        }
                        else
                        {
                            ++repoRemovalFailures;
                            metadata[nameof(errorMessage)] = errorMessage;
                            this.tracer.RelatedEvent(
                                EventLevel.Warning,
                                $"{nameof(this.RunMaintenanceTaskForRepos)}_FailedToRemoveRepo",
                                metadata);
                        }

                        continue;
                    }

                    ++reposMaintained;
                    this.tracer.RelatedEvent(
                                EventLevel.Informational,
                                $"{nameof(this.RunMaintenanceTaskForRepos)}_CallingMaintenance",
                                metadata);

                    this.scalarVerb.CallMaintenance(this.task, repoRegistration.NormalizedRepoRoot, registeredUser.SessionId);
                }

                metadata.Add(nameof(reposInRegistryForUser), reposInRegistryForUser);
                metadata.Add(nameof(reposSkipped), reposSkipped);
                metadata.Add(nameof(reposSuccessfullyRemoved), reposSuccessfullyRemoved);
                metadata.Add(nameof(repoRemovalFailures), repoRemovalFailures);
                metadata.Add(nameof(reposMaintained), reposMaintained);
                metadata.Add(nameof(maintenancePaused), maintenancePaused);
                this.tracer.RelatedEvent(
                    EventLevel.Informational,
                    $"{nameof(this.RunMaintenanceTaskForRepos)}_MaintenanceSummary",
                    metadata);
            }

            private bool IsMaintenancePaused(out string traceMessage)
            {
                if (this.ignorePause)
                {
                    traceMessage = null;
                    return false;
                }

                if (this.repoRegistry.TryGetMaintenanceDelayTime(out DateTime time))
                {
                    if (time.CompareTo(DateTime.Now) > 0)
                    {
                        traceMessage = $"Maintenance is paused until {time}.";
                        return true;
                    }
                    else if (!this.repoRegistry.TryRemovePauseFile(out string innerError))
                    {
                        traceMessage = $"Failed to remove pause file: {innerError}";
                        return false;
                    }
                }

                traceMessage = null;
                return false;
            }
        }

        private class MaintenanceSchedule
        {
            public MaintenanceSchedule(MaintenanceTasks.Task task, TimeSpan dueTime, TimeSpan period, bool ignorePause = false)
            {
                this.Task = task;
                this.DueTime = dueTime;
                this.Period = period;
                this.IgnorePause = ignorePause;
            }

            public MaintenanceTasks.Task Task { get; }
            public TimeSpan DueTime { get; }
            public TimeSpan Period { get; }
            public bool IgnorePause { get; }
        }
    }
}
