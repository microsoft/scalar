using Scalar.Common;
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

        private List<Timer> stepTimers;
        private MaintenanceTaskRunner taskRunner;

        public MaintenanceTaskScheduler()
        {
            this.stepTimers = new List<Timer>();
            this.taskRunner = new MaintenanceTaskRunner();
            this.ScheduleRecurringSteps();
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
            if (ScalarEnlistment.IsUnattended(tracer: null))
            {
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
