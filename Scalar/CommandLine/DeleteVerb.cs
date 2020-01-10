using CommandLine;
using Scalar.Common;
using System;
using System.ComponentModel;
using System.IO;

namespace Scalar.CommandLine
{
    [Verb(DeleteVerb.DeleteVerbName, HelpText = "Delete a Scalar enlistment")]
    public class DeleteVerb : ScalarVerb.ForNoEnlistment
    {
        private const string DeleteVerbName = "delete";

        protected override string VerbName => DeleteVerbName;

        private string enlistmentRoot;

        [Value(
                0,
            Required = true,
            MetaName = "Enlistment folder",
            HelpText = "The folder containing the repository to remove")]
        public string EnlistmentFolder { get; set; }

        public override void Execute()
        {
            if (!Path.IsPathRooted(this.EnlistmentFolder))
            {
                this.EnlistmentFolder = Path.Combine(Directory.GetCurrentDirectory(), this.EnlistmentFolder);
            }

            if (!ScalarPlatform.Instance.FileSystem.TryGetNormalizedPath(this.EnlistmentFolder, out this.enlistmentRoot, out string error))
            {
                this.ReportErrorAndExit($"Error while finding normalized path for '{this.EnlistmentFolder}': {error}");
            }

            // Move out of enlistment and into parent folder
            string parentDir = Path.GetDirectoryName(this.enlistmentRoot);
            Directory.SetCurrentDirectory(parentDir);

            this.StopFileSystemWatcher();
            this.UnregisterRepo(this.enlistmentRoot);
            this.DeleteEnlistment();
        }

        private void StopFileSystemWatcher()
        {
            try
            {
                string watchmanProcess = "watchman";
                string watchmanLocation = ProcessHelper.GetProgramLocation(ScalarPlatform.Instance.Constants.ProgramLocaterCommand, watchmanProcess);

                if (!string.IsNullOrEmpty(watchmanLocation))
                {
                    string watchmanPath = Path.Combine(watchmanLocation, watchmanProcess);
                    string workDir = Path.Combine(this.enlistmentRoot, ScalarConstants.WorkingDirectoryRootName);

                    // Stop watching watchman, if exists
                    string argument = $"watch-del {workDir}";
                    ProcessResult result = ProcessHelper.Run(watchmanPath, argument);

                    if (result.ExitCode != 0)
                    {
                        Console.Error.WriteLine($"Errors during 'watchman {argument}':");
                        Console.Error.WriteLine(result.Errors);
                    }

                    // Shutdown server, clearing handles (it will restart on a new query)
                    argument = "shutdown-server";
                    result = ProcessHelper.Run(watchmanPath, argument);

                    if (result.ExitCode != 0)
                    {
                        Console.Error.WriteLine($"Errors during 'watchman {argument}':");
                        Console.Error.WriteLine(result.Errors);
                    }
                }
            }
            catch (Win32Exception)
            {
                // Probably watchman is not on PATH
            }
        }

        private void DeleteEnlistment()
        {
            try
            {
                Directory.Delete(this.enlistmentRoot, recursive: true);
            }
            catch (IOException e)
            {
                this.ReportErrorAndExit($"Exception while deleting enlistment: {e.Message}");
            }
        }
    }
}
