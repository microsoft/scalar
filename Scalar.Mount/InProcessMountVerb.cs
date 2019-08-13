using CommandLine;
using Scalar.Common;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Common.Tracing;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Scalar.Mount
{
    [Verb("mount", HelpText = "Starts the background mount process")]
    public class InProcessMountVerb
    {
        private TextWriter output;

        public InProcessMountVerb()
        {
            this.output = Console.Out;
            this.ReturnCode = ReturnCode.Success;

            this.InitializeDefaultParameterValues();
        }

        public ReturnCode ReturnCode { get; private set; }

        [Option(
            'v',
            ScalarConstants.VerbParameters.Mount.Verbosity,
            Default = ScalarConstants.VerbParameters.Mount.DefaultVerbosity,
            Required = false,
            HelpText = "Sets the verbosity of console logging. Accepts: Verbose, Informational, Warning, Error")]
        public string Verbosity { get; set; }

        [Option(
            'k',
            ScalarConstants.VerbParameters.Mount.Keywords,
            Default = ScalarConstants.VerbParameters.Mount.DefaultKeywords,
            Required = false,
            HelpText = "A CSV list of logging filter keywords. Accepts: Any, Network")]
        public string KeywordsCsv { get; set; }

        [Option(
            'd',
            ScalarConstants.VerbParameters.Mount.DebugWindow,
            Default = false,
            Required = false,
            HelpText = "Show the debug window.  By default, all output is written to a log file and no debug window is shown.")]
        public bool ShowDebugWindow { get; set; }

        [Option(
            's',
            ScalarConstants.VerbParameters.Mount.StartedByService,
            Default = "false",
            Required = false,
            HelpText = "Service initiated mount.")]
        public string StartedByService { get; set; }

        [Option(
            'b',
            ScalarConstants.VerbParameters.Mount.StartedByVerb,
            Default = false,
            Required = false,
            HelpText = "Verb initiated mount.")]
        public bool StartedByVerb { get; set; }

        [Value(
                0,
                Required = true,
                MetaName = "Enlistment Root Path",
                HelpText = "Full or relative path to the Scalar enlistment root")]
        public string EnlistmentRootPathParameter { get; set; }

        public void InitializeDefaultParameterValues()
        {
            this.Verbosity = ScalarConstants.VerbParameters.Mount.DefaultVerbosity;
            this.KeywordsCsv = ScalarConstants.VerbParameters.Mount.DefaultKeywords;
        }

        public void Execute()
        {
            if (this.StartedByVerb)
            {
                // If this process was started by a verb it means that StartBackgroundScalarProcess was used
                // and we should be running in the background.  PrepareProcessToRunInBackground will perform
                // any platform specific preparation required to run as a background process.
                ScalarPlatform.Instance.PrepareProcessToRunInBackground();
            }

            ScalarEnlistment enlistment = this.CreateEnlistment(this.EnlistmentRootPathParameter);

            EventLevel verbosity;
            Keywords keywords;
            this.ParseEnumArgs(out verbosity, out keywords);

            JsonTracer tracer = this.CreateTracer(enlistment, verbosity, keywords);

            CacheServerInfo cacheServer = CacheServerResolver.GetCacheServerFromConfig(enlistment);

            tracer.WriteStartEvent(
                enlistment.EnlistmentRoot,
                enlistment.RepoUrl,
                cacheServer.Url,
                new EventMetadata
                {
                    { "IsElevated", ScalarPlatform.Instance.IsElevated() },
                    { nameof(this.EnlistmentRootPathParameter), this.EnlistmentRootPathParameter },
                    { nameof(this.StartedByService), this.StartedByService },
                    { nameof(this.StartedByVerb), this.StartedByVerb },
                });

            AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) =>
            {
                this.UnhandledScalarExceptionHandler(tracer, sender, e);
            };

            string error;
            RetryConfig retryConfig;
            if (!RetryConfig.TryLoadFromGitConfig(tracer, enlistment, out retryConfig, out error))
            {
                this.ReportErrorAndExit(tracer, "Failed to determine Scalar timeout and max retries: " + error);
            }

            InProcessMount mountHelper = new InProcessMount(tracer, enlistment, cacheServer, retryConfig, this.ShowDebugWindow);

            try
            {
                mountHelper.Mount(verbosity, keywords);
            }
            catch (Exception ex)
            {
                this.ReportErrorAndExit(tracer, "Failed to mount: {0}", ex.Message);
            }
        }

        private void UnhandledScalarExceptionHandler(ITracer tracer, object sender, UnhandledExceptionEventArgs e)
        {
            Exception exception = e.ExceptionObject as Exception;

            EventMetadata metadata = new EventMetadata();
            metadata.Add("Exception", exception.ToString());
            metadata.Add("IsTerminating", e.IsTerminating);
            tracer.RelatedError(metadata, "UnhandledScalarExceptionHandler caught unhandled exception");
        }

        private JsonTracer CreateTracer(ScalarEnlistment enlistment, EventLevel verbosity, Keywords keywords)
        {
            JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, "ScalarMount", enlistment.GetEnlistmentId(), enlistment.GetMountId());
            tracer.AddLogFileEventListener(
                ScalarEnlistment.GetNewScalarLogFileName(enlistment.ScalarLogsRoot, ScalarConstants.LogFileTypes.MountProcess),
                verbosity,
                keywords);
            if (this.ShowDebugWindow)
            {
                tracer.AddDiagnosticConsoleEventListener(verbosity, keywords);
            }

            return tracer;
        }

        private void ParseEnumArgs(out EventLevel verbosity, out Keywords keywords)
        {
            if (!Enum.TryParse(this.KeywordsCsv, out keywords))
            {
                this.ReportErrorAndExit("Error: Invalid logging filter keywords: " + this.KeywordsCsv);
            }

            if (!Enum.TryParse(this.Verbosity, out verbosity))
            {
                this.ReportErrorAndExit("Error: Invalid logging verbosity: " + this.Verbosity);
            }
        }

        private ScalarEnlistment CreateEnlistment(string enlistmentRootPath)
        {
            string gitBinPath = ScalarPlatform.Instance.GitInstallation.GetInstalledGitBinPath();
            if (string.IsNullOrWhiteSpace(gitBinPath))
            {
                this.ReportErrorAndExit("Error: " + ScalarConstants.GitIsNotInstalledError);
            }

            ScalarEnlistment enlistment = null;
            try
            {
                enlistment = ScalarEnlistment.CreateFromDirectory(enlistmentRootPath, gitBinPath, authentication: null);
            }
            catch (InvalidRepoException e)
            {
                this.ReportErrorAndExit(
                    "Error: '{0}' is not a valid Scalar enlistment. {1}",
                    enlistmentRootPath,
                    e.Message);
            }

            return enlistment;
        }

        private void ReportErrorAndExit(string error, params object[] args)
        {
            this.ReportErrorAndExit(null, error, args);
        }

        private void ReportErrorAndExit(ITracer tracer, string error, params object[] args)
        {
            if (tracer != null)
            {
                tracer.RelatedError(error, args);
            }

            if (error != null)
            {
                this.output.WriteLine(error, args);
            }

            if (this.ShowDebugWindow)
            {
                Console.WriteLine("\nPress Enter to Exit");
                Console.ReadLine();
            }

            this.ReturnCode = ReturnCode.GenericError;
            throw new MountAbortedException(this);
        }
    }
}
