using System;
using System.Threading;
using Scalar.Common;

namespace Scalar.Service
{
    public class MaintenanceTaskRunner
    {
        private readonly ManualResetEvent looseObjectsEvent;
        private readonly ManualResetEvent packFilesEvent;
        private readonly ManualResetEvent commitsAndTreesEvent;
        private readonly ManualResetEvent shutdownEvent;

        private readonly ConcurrentHashSet<Tuple<string, int>> registeredUsers;

        private readonly IRepoRegistry repoRegistry;

        public MaintenanceTaskRunner(IRepoRegistry repoRegistry)
        {
            this.looseObjectsEvent = new ManualResetEvent(initialState: false);
            this.packFilesEvent = new ManualResetEvent(initialState: false);
            this.commitsAndTreesEvent = new ManualResetEvent(initialState: false);
            this.shutdownEvent = new ManualResetEvent(initialState: false);

            this.registeredUsers = new ConcurrentHashSet<Tuple<string, int>>();

            this.repoRegistry = repoRegistry;

            Thread worker = new Thread(() => this.RunTasks());
            worker.Name = "MaintenanceWorker";
            worker.IsBackground = true;
            worker.Start();
        }

        public void RegisterActiveUser(string userId, int sessionId)
        {
            this.registeredUsers.Add(new Tuple<string, int>(userId, sessionId));
        }

        public void RunLooseObjectsTask()
        {
            this.looseObjectsEvent.Set();
        }

        public void RunPackFilesTask()
        {
            this.packFilesEvent.Set();
        }

        public void RunCommitsAndTreesTasks()
        {
            this.commitsAndTreesEvent.Set();
        }

        public void Stop()
        {
            this.shutdownEvent.Set();

            // TODO: Kill whatever step is currently running (if there is one
            // running)
        }

        private void RunTasks()
        {
            while (true)
            {
                WaitHandle.WaitAny(
                    new[]
                    {
                        this.looseObjectsEvent,
                        this.packFilesEvent,
                        this.commitsAndTreesEvent,
                        this.shutdownEvent,
                    });

                if (this.shutdownEvent.WaitOne(0))
                {
                    return;
                }

                // TODO: Find a replacement for EnlistmentRootReady
                // if (this.EnlistmentRootReady())

                if (this.looseObjectsEvent.WaitOne(0))
                {
                    this.RunMaintenanceTaskForRegisteredUsers(
                        ScalarConstants.VerbParameters.Maintenance.LooseObjectsTaskName);
                    this.looseObjectsEvent.Reset();
                }

                if (this.packFilesEvent.WaitOne(0))
                {
                    this.RunMaintenanceTaskForRegisteredUsers(
                        ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName);
                    this.packFilesEvent.Reset();
                }

                if (this.commitsAndTreesEvent.WaitOne(0))
                {
                    // Increase the chances that "commit-graph" has something to
                    // do by running "fetch-commits-and-trees" first
                    this.RunMaintenanceTaskForRegisteredUsers(
                        ScalarConstants.VerbParameters.Maintenance.FetchCommitsAndTreesTaskName);
                    this.RunMaintenanceTaskForRegisteredUsers(
                        ScalarConstants.VerbParameters.Maintenance.CommitGraphTaskName);
                    this.commitsAndTreesEvent.Reset();
                }
            }
        }

        private void RunMaintenanceTaskForRegisteredUsers(string task)
        {
            foreach (Tuple<string, int> user in this.registeredUsers)
            {
                this.repoRegistry.RunMainteanceTaskForRepos(
                    task,
                    user.Item1,
                    user.Item2);
            }
        }
    }
}
