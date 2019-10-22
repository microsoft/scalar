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

        private readonly ServiceTaskRunner taskRunner;
        private readonly Dictionary<string, ServiceTask> maintenanceTasks;
        private List<Timer> stepTimers;

        private UserAndSession registeredUser;

        public MaintenanceTaskScheduler(ITracer tracer, IRepoRegistry repoRegistry)
        {
            this.tracer = tracer;
            this.stepTimers = new List<Timer>();
            this.maintenanceTasks = this.CreateMaintenanceTasks(repoRegistry);
            this.taskRunner = new ServiceTaskRunner(this.tracer, this.maintenanceTasks.Values);
            this.ScheduleRecurringSteps();
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
            this.taskRunner.Stop();

            foreach (Timer timer in this.stepTimers)
            {
                timer?.Dispose();
            }

            this.stepTimers = null;
        }

        private void ScheduleRecurringSteps()
        {
            if (ScalarEnlistment.IsUnattended(this.tracer))
            {
                this.tracer.RelatedInfo($"{nameof(this.ScheduleRecurringSteps)}: Skipping maintenance tasks due to running unattended");
                return;
            }

            this.stepTimers.Add(new Timer(
                (state) => this.maintenanceTasks[ScalarConstants.VerbParameters.Maintenance.FetchCommitsAndTreesTaskName].TaskSignaled.Set(),
                state: null,
                dueTime: this.fetchCommitsAndTreesPeriod,
                period: this.fetchCommitsAndTreesPeriod));

            this.stepTimers.Add(new Timer(
                (state) => this.maintenanceTasks[ScalarConstants.VerbParameters.Maintenance.LooseObjectsTaskName].TaskSignaled.Set(),
                state: null,
                dueTime: this.looseObjectsDueTime,
                period: this.looseObjectsPeriod));

            this.stepTimers.Add(new Timer(
                (state) => this.maintenanceTasks[ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName].TaskSignaled.Set(),
                state: null,
                dueTime: this.packfileDueTime,
                period: this.packfilePeriod));

            this.stepTimers.Add(new Timer(
                (state) => this.maintenanceTasks[ScalarConstants.VerbParameters.Maintenance.CommitGraphTaskName].TaskSignaled.Set(),
                state: null,
                dueTime: this.commitGraphDueTime,
                period: this.commitGraphPeriod));
        }

        private Dictionary<string, ServiceTask> CreateMaintenanceTasks(IRepoRegistry repoRegistry)
        {
            Func<UserAndSession> getRegisteredUser = () => { return this.registeredUser; };

            return new Dictionary<string, ServiceTask>()
            {
                {
                    ScalarConstants.VerbParameters.Maintenance.FetchCommitsAndTreesTaskName,
                    new MaintenanceTask(this.tracer, repoRegistry, getRegisteredUser, ScalarConstants.VerbParameters.Maintenance.FetchCommitsAndTreesTaskName)
                },
                {
                    ScalarConstants.VerbParameters.Maintenance.LooseObjectsTaskName,
                    new MaintenanceTask(this.tracer, repoRegistry, getRegisteredUser, ScalarConstants.VerbParameters.Maintenance.LooseObjectsTaskName)
                },
                {
                    ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName,
                    new MaintenanceTask(this.tracer, repoRegistry, getRegisteredUser, ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName)
                },
                {
                    ScalarConstants.VerbParameters.Maintenance.CommitGraphTaskName,
                    new MaintenanceTask(this.tracer, repoRegistry, getRegisteredUser, ScalarConstants.VerbParameters.Maintenance.CommitGraphTaskName)
                },
            };
        }

        private class MaintenanceTask : ServiceTask
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

            public override void Execute()
            {
                this.tracer.RelatedInfo($"{nameof(MaintenanceTask)}: Executing '{this.task}'");
                UserAndSession registeredUser = this.getRegisteredUser();
                if (registeredUser != null)
                {
                    this.repoRegistry.RunMainteanceTaskForRepos(
                        this.task,
                        registeredUser.UserId,
                        registeredUser.SessionId);
                }
            }

            public override void Stop()
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
