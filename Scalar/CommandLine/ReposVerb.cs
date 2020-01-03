using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Maintenance;
using Scalar.Common.RepoRegistry;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Scalar.CommandLine
{
    [Verb(ReposVerb.ReposVerbName, HelpText = "Track repos registered with the Scalar service")]
    public class ReposVerb : ScalarVerb
    {
        private const string ReposVerbName = "repos";

        private const string AddSubcommand = "add";
        private const string ListSubcommand = "list";
        private const string RemoveSubcommand = "remove";

        private string enlistmentRoot;
        private PhysicalFileSystem fileSystem = new PhysicalFileSystem();

        protected override string VerbName => ReposVerb.ReposVerbName;

        [Value(
            0,
            Required = true,
            MetaName = "Subcommand",
            HelpText = "The subcommand to execute")]
        public string Subcommand { get; set; }

        [Value(
            1,
            Required = false,
            Default = null,
            MetaName = "Enlistment Root Path",
            HelpText = "Full or relative path to the Scalar enlistment root")]
        public override string EnlistmentRootPathParameter { get; set; }

        [Option(
            "from-disk",
            Required = false,
            Default = false,
            HelpText = "Remove the enlistment from disk in addition to the service registry")]
        public bool FromDisk { get; set; }

        public override void Execute()
        {

            switch (this.Subcommand)
            {
                case ReposVerb.AddSubcommand:
                    this.Add();
                    break;

                case ReposVerb.ListSubcommand:
                    foreach (string repoRoot in this.GetRepoList())
                    {
                        this.Output.WriteLine(repoRoot);
                    }
                    break;

                case ReposVerb.RemoveSubcommand:
                    this.Remove();
                    break;

                default:
                    StringBuilder messageBuilder = new StringBuilder();
                    messageBuilder.AppendLine($"Unknown subcommand '{this.Subcommand}'");
                    messageBuilder.AppendLine("Options are:");
                    messageBuilder.AppendLine($"\t{ReposVerb.AddSubcommand}");
                    messageBuilder.AppendLine($"\t{ReposVerb.ListSubcommand}");

                    this.ReportErrorAndExit(messageBuilder.ToString());
                    break;
            }
        }

        private void Add()
        {
            this.ValidatePathParameter(this.EnlistmentRootPathParameter);

            ScalarEnlistment enlistment = this.CreateEnlistment(this.EnlistmentRootPathParameter ?? Directory.GetCurrentDirectory(), null);

            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, ReposVerb.ReposVerbName))
            {
                if (this.TryRegisterRepo(tracer, enlistment, fileSystem, out string error))
                {
                    this.Output.WriteLine($"Successfully registered repo at '{enlistment.EnlistmentRoot}'");
                }
                else
                {
                    string message = $"Failed to register repo: {error}";
                    tracer.RelatedError(message);
                    this.ReportErrorAndExit(message);
                    return;
                }

                ScalarContext context = new ScalarContext(tracer, fileSystem, enlistment);
                new ConfigStep(context).Execute();
            }
        }

        private IEnumerable<string> GetRepoList()
        {
            string repoRegistryLocation = ScalarPlatform.Instance.GetCommonAppDataRootForScalarComponent(ScalarConstants.RepoRegistry.RegistryDirectoryName);
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, ReposVerb.ReposVerbName))
            {
                ScalarRepoRegistry repoRegistry = new ScalarRepoRegistry(
                    tracer,
                    new PhysicalFileSystem(),
                    repoRegistryLocation);

                return repoRegistry.GetRegisteredRepos().Select(x => x.NormalizedRepoRoot);
            }
        }

        private void Remove()
        {
            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, ReposVerb.ReposVerbName))
            {
                ScalarEnlistment enlistment = this.CreateEnlistment(this.EnlistmentRootPathParameter ?? Directory.GetCurrentDirectory(), null);

                string logFileName = ScalarEnlistment.GetNewScalarLogFileName(
                                                        enlistment.ScalarLogsRoot,
                                                        ScalarConstants.LogFileTypes.ReposRemove);
                this.ValidatePathParameter(this.EnlistmentRootPathParameter);

                if (this.TryUnregisterRepo(enlistment.EnlistmentRoot, tracer))
                {
                    this.Output.WriteLine($"Successfully unregistered repo at '{enlistment.EnlistmentRoot}'");
                }

                if (this.FromDisk)
                {
                    this.RemoveFromDisk();
                }
            }
        }

        private void RemoveFromDisk()
        {
            if (!Path.IsPathRooted(this.EnlistmentRootPathParameter))
            {
                this.EnlistmentRootPathParameter = Path.Combine(Directory.GetCurrentDirectory(), this.EnlistmentRootPathParameter);
            }

            if (!ScalarPlatform.Instance.FileSystem.TryGetNormalizedPath(this.EnlistmentRootPathParameter, out this.enlistmentRoot, out string error))
            {
                Console.Error.WriteLine($"Error while finding normalized path for '{this.EnlistmentRootPathParameter}': {error}");
                Environment.Exit(1);
            }

            // Move out of enlistment and into parent folder
            string parentDir = Path.GetDirectoryName(this.enlistmentRoot);
            Directory.SetCurrentDirectory(parentDir);

            this.StopFileSystemWatcher();
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

        private bool TryUnregisterRepo(string enlistmentRoot, ITracer tracer)
        {
            string repoRegistryLocation = ScalarPlatform.Instance.GetCommonAppDataRootForScalarComponent(ScalarConstants.RepoRegistry.RegistryDirectoryName);
            ScalarRepoRegistry repoRegistry = new ScalarRepoRegistry(
                                                        new JsonTracer(nameof(ReposVerb), nameof(this.Execute)),
                                                        new PhysicalFileSystem(),
                                                        repoRegistryLocation);

            if (!repoRegistry.TryUnregisterRepo(enlistmentRoot, out string error))
            {
                tracer.RelatedError(error);
                Output.WriteLine($"Error while unregistering repo: {error}");
                return false;
            }

            return true;
        }

        private void DeleteEnlistment()
        {
            try
            {
                Directory.Delete(this.enlistmentRoot, recursive: true);
            }
            catch (IOException e)
            {
                Output.WriteLine($"Exception while deleting enlistment: {e.Message}");
                Environment.Exit(1);
            }
        }
    }
}
