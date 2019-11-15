using CommandLine;
using Scalar.Common;
using System;
using System.ComponentModel;
using System.IO;

namespace Scalar.CommandLine
{
    [Verb(RemoveVerb.RemoveVerbName, HelpText = "Remove a Scalar enlistment")]
    public class RemoveVerb : ScalarVerb
    {
        private const string RemoveVerbName = "remove";

        protected override string VerbName => RemoveVerbName;

        [Value(
                0,
            Required = true,
            MetaName = "Enlistment folder",
            HelpText = "The folder containing the repository to remove")]
        public string EnlistmentFolder { get; set; }
        public override string EnlistmentRootPathParameter { get; set; }

        public override void Execute()
        {
            string enlistmentRoot = this.EnlistmentFolder;

            if (!Path.IsPathRooted(enlistmentRoot))
            {
                enlistmentRoot = Path.Combine(Directory.GetCurrentDirectory(), this.EnlistmentFolder);
            }

            // Move out of enlistment and into parent folder
            string parentDir = Path.GetDirectoryName(enlistmentRoot);
            Directory.SetCurrentDirectory(parentDir);

            try
            {
                string watchmanLocation = ProcessHelper.GetProgramLocation(ScalarPlatform.Instance.Constants.ProgramLocaterCommand, "watchman");

                if (!string.IsNullOrEmpty(watchmanLocation))
                {
                    // Stop watching watchman, if exists
                    ProcessResult result = ProcessHelper.Run(watchmanLocation, $"watch-del {Path.Combine(enlistmentRoot, ScalarConstants.WorkingDirectoryRootName)}");

                    if (result.ExitCode != 0)
                    {
                        Console.Error.WriteLine($"Errors while communicating with Watchman (may not be installed):");
                        Console.Error.WriteLine(result.Errors);
                    }
                }
            }
            catch (Win32Exception)
            {
                // Probably watchman is not on PATH
            }

            try
            {
                Directory.Delete(enlistmentRoot, recursive: true);
            }
            catch (IOException e)
            {
                Console.Error.WriteLine($"Exception while deleting enlistment: {e.Message}");
                Environment.Exit(1);
            }
        }
    }
}
