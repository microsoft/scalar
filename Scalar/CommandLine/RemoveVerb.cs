using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
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

        private string enlistmentRoot;

        [Value(
                0,
            Required = true,
            MetaName = "Enlistment folder",
            HelpText = "The folder containing the repository to remove")]
        public string EnlistmentFolder { get; set; }
        public override string EnlistmentRootPathParameter { get; set; }

        public override void Execute()
        {
            this.enlistmentRoot = this.EnlistmentFolder;

            if (!Path.IsPathRooted(enlistmentRoot))
            {
                this.enlistmentRoot = Path.Combine(Directory.GetCurrentDirectory(), this.EnlistmentFolder);
            }

            // Move out of enlistment and into parent folder
            string parentDir = Path.GetDirectoryName(this.enlistmentRoot);
            Directory.SetCurrentDirectory(parentDir);

            this.StopFileSystemWatcher();
            this.TryUnegisterRepo();
            this.DeleteEnlistment();
        }

        private void StopFileSystemWatcher()
        {
            try
            {
                string watchmanLocation = ProcessHelper.GetProgramLocation(ScalarPlatform.Instance.Constants.ProgramLocaterCommand, "watchman");

                if (!string.IsNullOrEmpty(watchmanLocation))
                {
                    // Stop watching watchman, if exists
                    ProcessResult result = ProcessHelper.Run(watchmanLocation, $"watch-del {Path.Combine(this.enlistmentRoot, ScalarConstants.WorkingDirectoryRootName)}");

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
        }

        private void TryUnegisterRepo()
        {
            string repoRegistryLocation = ScalarPlatform.Instance.GetDataRootForScalarComponent(ScalarConstants.RepoRegistry.RegistryDirectoryName);
            ScalarRepoRegistry repoRegistry = new ScalarRepoRegistry(
                                                        new JsonTracer(nameof(RemoveVerb), nameof(this.Execute)),
                                                        new PhysicalFileSystem(),
                                                        repoRegistryLocation);

            if (!repoRegistry.TryUnregisterRepo(this.enlistmentRoot, out string error))
            {
                Console.Error.WriteLine($"Error while unregistering repo: {error}");
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
                Console.Error.WriteLine($"Exception while deleting enlistment: {e.Message}");
                Environment.Exit(1);
            }
        }
    }
}
