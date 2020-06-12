using CommandLine;
using Scalar.Common;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

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
            // TODO: Integrate with Git's internal FSMonitor, if required.
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
