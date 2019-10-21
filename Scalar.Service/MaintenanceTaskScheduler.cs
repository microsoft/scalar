﻿using Scalar.Common.Git;
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

        private readonly TimeSpan prefetchPeriod = TimeSpan.FromMinutes(15);

        private List<Timer> stepTimers;
        private GitMaintenanceQueue queue;

        public GitMaintenanceScheduler(ScalarContext context, GitObjects gitObjects)
        {
            this.stepTimers = new List<Timer>();
            this.queue = new GitMaintenanceQueue(context);

            this.ScheduleRecurringSteps();
        }

        public void Dispose()
        {
            this.queue.Stop();

            foreach (Timer timer in this.stepTimers)
            {
                timer?.Dispose();
            }

            this.stepTimers = null;
        }

        private void ScheduleRecurringSteps()
        {
            if (this.context.Unattended)
            {
                return;
            }

            if (this.gitObjects.IsUsingCacheServer())
            {
                TimeSpan prefetchPeriod = TimeSpan.FromMinutes(15);
                this.stepTimers.Add(new Timer(
                    (state) => this.queue.TryEnqueue(new PrefetchStep(this.context, this.gitObjects)),
                    state: null,
                    dueTime: this.prefetchPeriod,
                    period: this.prefetchPeriod));
            }

            this.stepTimers.Add(new Timer(
                (state) => this.queue.TryEnqueue(new LooseObjectsStep(this.context)),
                state: null,
                dueTime: this.looseObjectsDueTime,
                period: this.looseObjectsPeriod));

            this.stepTimers.Add(new Timer(
                (state) => this.queue.TryEnqueue(new PackfileMaintenanceStep(this.context)),
                state: null,
                dueTime: this.packfileDueTime,
                period: this.packfilePeriod));
        }
    }
}
