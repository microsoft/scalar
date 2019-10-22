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

        // Used for both "fetch-commits-and-trees" and "commit-graph" tasks
        private readonly TimeSpan commitsAndTreesPeriod = TimeSpan.FromMinutes(15);

        private readonly ITracer tracer;

        private readonly MaintenanceTaskRunner taskRunner;
        private List<Timer> stepTimers;

        public MaintenanceTaskScheduler(ITracer tracer, IRepoRegistry repoRegistry)
        {
            this.tracer = tracer;
            this.stepTimers = new List<Timer>();
            this.taskRunner = new MaintenanceTaskRunner(tracer, repoRegistry);
            this.ScheduleRecurringSteps();
        }

        public void RegisterActiveUser(string userId, int sessionId)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add(nameof(userId), userId);
            metadata.Add(nameof(sessionId), sessionId);
            metadata.Add(
                TracingConstants.MessageKey.InfoMessage,
                $"{nameof(MaintenanceTaskScheduler)}: Registering user");
            this.tracer.RelatedEvent(EventLevel.Informational, nameof(this.RegisterActiveUser), metadata);

            this.taskRunner.RegisterActiveUser(userId, sessionId);
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
                (state) => this.taskRunner.RunCommitsAndTreesTasks(),
                state: null,
                dueTime: this.commitsAndTreesPeriod,
                period: this.commitsAndTreesPeriod));

            this.stepTimers.Add(new Timer(
                (state) => this.taskRunner.RunLooseObjectsTask(),
                state: null,
                dueTime: this.looseObjectsDueTime,
                period: this.looseObjectsPeriod));

            this.stepTimers.Add(new Timer(
                (state) => this.taskRunner.RunPackFilesTask(),
                state: null,
                dueTime: this.packfileDueTime,
                period: this.packfilePeriod));
        }
    }
}
