using System;

namespace Scalar.Common.Maintenance
{
    public static class MaintenanceTasks
    {
        public enum Task
        {
            Invalid = 0,
            FetchCommitsAndTrees,
            LooseObjects,
            PackFiles,
            CommitGraph,
            Config,
            Status,
        }

        public static string GetVerbTaskName(Task task)
        {
            switch (task)
            {
                case Task.FetchCommitsAndTrees:
                    return ScalarConstants.VerbParameters.Maintenance.FetchTaskName;
                case Task.LooseObjects:
                    return ScalarConstants.VerbParameters.Maintenance.LooseObjectsTaskName;
                case Task.PackFiles:
                    return ScalarConstants.VerbParameters.Maintenance.PackFilesTaskName;
                case Task.CommitGraph:
                    return ScalarConstants.VerbParameters.Maintenance.CommitGraphTaskName;
                case Task.Config:
                    return ScalarConstants.VerbParameters.Maintenance.ConfigTaskName;
                default:
                    throw new ArgumentException($"Invalid or unknown task {task.ToString()}", nameof(task));
            }
        }
    }
}
