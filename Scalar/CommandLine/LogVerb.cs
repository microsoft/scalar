using CommandLine;
using Scalar.Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scalar.CommandLine
{
    [Verb(LogVerb.LogVerbName, HelpText = "Show the most recent Scalar log files")]
    public class LogVerb : ScalarVerb
    {
        private const string LogVerbName = "log";
        private static readonly int LogNameConsoleOutputFormatWidth = GetMaxLogNameLength();

        [Value(
            0,
            Required = false,
            Default = "",
            MetaName = "Enlistment Root Path",
            HelpText = "Full or relative path to the Scalar enlistment root")]
        public override string EnlistmentRootPathParameter { get; set; }

        [Option(
            "type",
            Default = null,
            HelpText = "The type of log file to display on the console")]
        public string LogType { get; set; }

        protected override string VerbName
        {
            get { return LogVerbName; }
        }

        public override void Execute()
        {
            this.ValidatePathParameter(this.EnlistmentRootPathParameter);

            this.Output.WriteLine("Most recent log files:");

            string errorMessage;
            string enlistmentRoot;
            if (!ScalarPlatform.Instance.TryGetScalarEnlistmentRoot(this.EnlistmentRootPathParameter, out enlistmentRoot, out errorMessage))
            {
                this.ReportErrorAndExit(
                    "Error: '{0}' is not a valid Scalar enlistment",
                    this.EnlistmentRootPathParameter);
            }

            string scalarLogsRoot = Path.Combine(
                enlistmentRoot,
                ScalarPlatform.Instance.Constants.DotScalarRoot,
                ScalarConstants.DotScalar.LogName);

            if (this.LogType == null)
            {
                this.DisplayMostRecent(scalarLogsRoot, ScalarConstants.LogFileTypes.Clone);

                // By using MountPrefix ("mount") DisplayMostRecent will display either mount_verb, mount_upgrade, or mount_process, whichever is more recent
                this.DisplayMostRecent(scalarLogsRoot, ScalarConstants.LogFileTypes.MountPrefix);
                this.DisplayMostRecent(scalarLogsRoot, ScalarConstants.LogFileTypes.Repair);

                string serviceLogsRoot = Path.Combine(
                    ScalarPlatform.Instance.GetDataRootForScalarComponent(ScalarConstants.Service.ServiceName),
                    ScalarConstants.Service.LogDirectory);
                this.DisplayMostRecent(serviceLogsRoot, ScalarConstants.LogFileTypes.Service);

                this.DisplayMostRecent(ProductUpgraderInfo.GetLogDirectoryPath(), ScalarConstants.LogFileTypes.UpgradePrefix);
            }
            else
            {
                string logFile = FindNewestFileInFolder(scalarLogsRoot, this.LogType);
                if (logFile == null)
                {
                    this.ReportErrorAndExit("No log file found");
                }
                else
                {
                    foreach (string line in File.ReadAllLines(logFile))
                    {
                        this.Output.WriteLine(line);
                    }
                }
            }
        }

        private static string FindNewestFileInFolder(string folderName, string logFileType)
        {
            string logFilePattern = GetLogFilePatternForType(logFileType);

            DirectoryInfo logDirectory = new DirectoryInfo(folderName);
            if (!logDirectory.Exists)
            {
                return null;
            }

            FileInfo[] files = logDirectory.GetFiles(logFilePattern ?? "*");
            if (files.Length == 0)
            {
                return null;
            }

            return
                files
                .OrderByDescending(fileInfo => fileInfo.CreationTime)
                .First()
                .FullName;
        }

        private static string GetLogFilePatternForType(string logFileType)
        {
            return "scalar_" + logFileType + "_*.log";
        }

        private static int GetMaxLogNameLength()
        {
            List<string> lognames = new List<string>
            {
                ScalarConstants.LogFileTypes.Clone,
                ScalarConstants.LogFileTypes.MountPrefix,
                ScalarConstants.LogFileTypes.Repair,
                ScalarConstants.LogFileTypes.Service,
                ScalarConstants.LogFileTypes.UpgradePrefix,
            };

            return lognames.Max(s => s.Length) + 1;
        }

        private void DisplayMostRecent(string logFolder, string logFileType)
        {
            string logFile = FindNewestFileInFolder(logFolder, logFileType);
            this.Output.WriteLine(
                $"  {{0, -{LogNameConsoleOutputFormatWidth}}}: {{1}}",
                logFileType,
                logFile == null ? "None" : logFile);
        }
    }
}
