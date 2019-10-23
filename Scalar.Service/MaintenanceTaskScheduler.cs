using Scalar.Common;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Scalar.Service
{
    public class MaintenanceTaskScheduler : IDisposable
    {
        private readonly TimeSpan looseObjectsDueTime = TimeSpan.FromMinutes(5);
        private readonly TimeSpan looseObjectsPeriod = TimeSpan.FromHours(6);

        private readonly TimeSpan packfileDueTime = TimeSpan.FromMinutes(30);
        private readonly TimeSpan packfilePeriod = TimeSpan.FromHours(12);

        private readonly TimeSpan commitGraphDueTime = TimeSpan.FromMinutes(15);
        private readonly TimeSpan commitGraphPeriod = TimeSpan.FromHours(1);

        private readonly TimeSpan fetchCommitsAndTreesPeriod = TimeSpan.FromMinutes(15);

        private readonly ITracer tracer;
        private readonly ServiceTaskQueue taskQueue;
        private List<Timer> stepTimers;
        private UserAndSession registeredUser;

        public MaintenanceTaskScheduler(ITracer tracer, IRepoRegistry repoRegistry)
        {
            this.tracer = tracer;
            this.stepTimers = new List<Timer>();
            this.taskQueue = new ServiceTaskQueue(this.tracer);
            this.ScheduleRecurringSteps(repoRegistry);
        }

        public void RegisterUser(string userId, int sessionId)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add(nameof(userId), userId);
            metadata.Add(nameof(sessionId), sessionId);
            metadata.Add(
                TracingConstants.MessageKey.InfoMessage,
                $"{nameof(MaintenanceTaskScheduler)}: Registering user");
            this.tracer.RelatedEvent(EventLevel.Informational, nameof(this.RegisterUser), metadata);

            this.registeredUser = new UserAndSession(userId, sessionId);
        }

        public void UnregisterUser(string userId, int sessionId)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add(nameof(userId), userId);
            metadata.Add(nameof(sessionId), sessionId);
            metadata.Add($"{nameof(this.registeredUser)}.{nameof(UserAndSession.UserId)}", this.registeredUser?.UserId);
            metadata.Add($"{nameof(this.registeredUser)}.{nameof(UserAndSession.SessionId)}", this.registeredUser?.SessionId);

            if (this.registeredUser?.UserId == userId && this.registeredUser?.SessionId == sessionId)
            {
                metadata.Add(
                    TracingConstants.MessageKey.InfoMessage,
                    $"{nameof(MaintenanceTaskScheduler)}: Unregistering user");

                this.registeredUser = null;
            }
            else
            {
                metadata.Add(
                    TracingConstants.MessageKey.InfoMessage,
                    $"{nameof(MaintenanceTaskScheduler)}: UnregisterUser request does not match active user, ignoring request");
            }

            this.tracer.RelatedEvent(EventLevel.Informational, nameof(this.RegisterUser), metadata);
        }

        public void Dispose()
        {
            this.taskQueue.Stop();

            foreach (Timer timer in this.stepTimers)
            {
                timer?.Dispose();
            }

            this.stepTimers = null;
        }

        private void ScheduleRecurringSteps(IRepoRegistry repoRegistry)
        {
            if (ScalarEnlistment.IsUnattended(this.tracer))
            {
                this.tracer.RelatedInfo($"{nameof(this.ScheduleRecurringSteps)}: Skipping maintenance tasks due to running unattended");
                return;
            }

            Func<UserAndSession> getRegisteredUser = () => { return this.registeredUser; };

            this.stepTimers.Add(new Timer(
                (state) => this.taskQueue.TryEnqueue(
                    new MaintenanceTask(
                        this.tracer,
                        repoRegistry,
                        getRegisteredUser,
                        ScalarConstants.VerbParameters.Maintenance.FetchCommitsAndTreesTaskName)),
                state: null,
                dueTime: this.fetchCommitsAndTreesPeriod,
                period: this.fetchCommitsAndTreesPeriod));

            this.stepTimers.Add(new Timer(
                (state) => this.taskQueue.TryEnqueue(
                    new MaintenanceTask(
                        this.tracer,
                        repoRegistry,
                        getRegisteredUser,
                        ScalarConstants.VerbParameters.Maintenance.LooseObjectsTaskName)),
                state: null,
                dueTime: this.looseObjectsDueTime,
                period: this.looseObjectsPeriod));

            this.stepTimers.Add(new Timer(
                (state) => this.taskQueue.TryEnqueue(
                    new MaintenanceTask(
                        this.tracer,
                        repoRegistry,
                        getRegisteredUser,
                        ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName)),
                state: null,
                dueTime: this.packfileDueTime,
                period: this.packfilePeriod));

            this.stepTimers.Add(new Timer(
                (state) => this.taskQueue.TryEnqueue(
                    new MaintenanceTask(
                        this.tracer,
                        repoRegistry,
                        getRegisteredUser,
                        ScalarConstants.VerbParameters.Maintenance.CommitGraphTaskName)),
                state: null,
                dueTime: this.commitGraphDueTime,
                period: this.commitGraphPeriod));
        }

        private class MaintenanceTask : IServiceTask
        {
            private readonly string task;
            private readonly IRepoRegistry repoRegistry;
            private readonly ITracer tracer;
            private readonly Func<UserAndSession> getRegisteredUser;

            public MaintenanceTask(
                ITracer tracer,
                IRepoRegistry repoRegistry,
                Func<UserAndSession> getRegisteredUser,
                string task)
            {
                this.tracer = tracer;
                this.repoRegistry = repoRegistry;
                this.getRegisteredUser = getRegisteredUser;
                this.task = task;
            }

            public void Execute()
            {
                UserAndSession registeredUser = this.getRegisteredUser();
                if (registeredUser != null)
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add(nameof(registeredUser.UserId), registeredUser.UserId);
                    metadata.Add(nameof(registeredUser.SessionId), registeredUser.SessionId);
                    metadata.Add(nameof(this.task), this.task);
                    metadata.Add(TracingConstants.MessageKey.InfoMessagem "Executing maintenance task");
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

        private class UserAndSession
        {
            public UserAndSession(string userId, int sessionId)
            {
                this.UserId = userId;
                this.SessionId = sessionId;
            }

            public string UserId { get; }
            public int SessionId { get; }
        }
    }
}
