using NUnit.Framework;
using Scalar.FunctionalTests.FileSystemRunners;
using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Scalar.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    [NonParallelizable]
    [Category(Categories.ExtraCoverage)]
    [Category(Categories.WindowsOnly)]
    [Category(Categories.NeedsUpdatesForNonVirtualizedMode)]
    public class UpgradeReminderTests : TestsWithEnlistmentPerFixture
    {
        private const string HighestAvailableVersionFileName = "HighestAvailableVersion";
        private const string UpgradeRingKey = "upgrade.ring";
        private const string NugetFeedURLKey = "upgrade.feedurl";
        private const string NugetFeedPackageNameKey = "upgrade.feedpackagename";
        private const string AlwaysUpToDateRing = "None";

        private string upgradeDownloadsDirectory;
        private FileSystemRunner fileSystem;

        public UpgradeReminderTests()
        {
            this.fileSystem = new SystemIORunner();
            this.upgradeDownloadsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.Create),
                "Scalar",
                "Scalar.Upgrade",
                "Downloads");
        }

        [TestCase]
        public void NoReminderWhenUpgradeNotAvailable()
        {
            this.EmptyDownloadDirectory();

            for (int count = 0; count < 50; count++)
            {
                ProcessResult result = GitHelpers.InvokeGitAgainstScalarRepo(
                    this.Enlistment.RepoRoot,
                    "status");

                string.IsNullOrEmpty(result.Errors).ShouldBeTrue();
            }
        }

        [TestCase]
        public void RemindWhenUpgradeAvailable()
        {
            this.CreateUpgradeAvailableMarkerFile();
            this.ReminderMessagingEnabled().ShouldBeTrue();
            this.EmptyDownloadDirectory();
        }

        [TestCase]
        public void NoReminderForLeftOverDownloads()
        {
            this.VerifyServiceRestartStopsReminder();

            // This test should not use Nuget upgrader because it will usually find an upgrade
            // to download.  The "None" ring config doesn't stop the Nuget upgrader from checking
            // its feed for updates, and the Scalar binaries installed during functional test
            // runs typically have a 0.X version number (meaning there will always be a newer
            // version of Scalar available to download from the feed).
            this.ReadNugetConfig(out string feedUrl, out string feedName);
            this.DeleteNugetConfig();
            this.VerifyUpgradeVerbStopsReminder();
            this.WriteNugetConfig(feedUrl, feedName);
        }

        [TestCase]
        public void UpgradeTimerScheduledOnServiceStart()
        {
            this.RestartService();

            bool timerScheduled = false;

            // Service starts upgrade checks after 60 seconds.
            Thread.Sleep(TimeSpan.FromSeconds(60));
            for (int trialCount = 0; trialCount < 30; trialCount++)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                if (this.ServiceLogContainsUpgradeMessaging())
                {
                    timerScheduled = true;
                    break;
                }
            }

            timerScheduled.ShouldBeTrue();
        }

        private void ReadNugetConfig(out string feedUrl, out string feedName)
        {
            ScalarProcess scalar = new ScalarProcess(ScalarTestConfig.PathToScalar, enlistmentRoot: null, localCacheRoot: null);

            // failOnError is set to false because scalar config read can exit with
            // GenericError when the key-value is not available in config file. That
            // is normal.
            feedUrl = scalar.ReadConfig(NugetFeedURLKey, failOnError: false);
            feedName = scalar.ReadConfig(NugetFeedPackageNameKey, failOnError: false);
        }

        private void DeleteNugetConfig()
        {
            ScalarProcess scalar = new ScalarProcess(ScalarTestConfig.PathToScalar, enlistmentRoot: null, localCacheRoot: null);
            scalar.DeleteConfig(NugetFeedURLKey);
            scalar.DeleteConfig(NugetFeedPackageNameKey);
        }

        private void WriteNugetConfig(string feedUrl, string feedName)
        {
            ScalarProcess scalar = new ScalarProcess(ScalarTestConfig.PathToScalar, enlistmentRoot: null, localCacheRoot: null);
            if (!string.IsNullOrEmpty(feedUrl))
            {
                scalar.WriteConfig(NugetFeedURLKey, feedUrl);
            }

            if (!string.IsNullOrEmpty(feedName))
            {
                scalar.WriteConfig(NugetFeedPackageNameKey, feedName);
            }
        }

        private bool ServiceLogContainsUpgradeMessaging()
        {
            // This test checks for the upgrade timer start message in the Service log
            // file. Scalar.Service should schedule the timer as it starts.
            string expectedTimerMessage = "Checking for product upgrades. (Start)";
            string serviceLogFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Scalar",
                ScalarServiceProcess.TestServiceName,
                "Logs");
            DirectoryInfo logsDirectory = new DirectoryInfo(serviceLogFolder);
            FileInfo logFile = logsDirectory.GetFiles()
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            if (logFile != null)
            {
                using (StreamReader fileStream = new StreamReader(File.Open(logFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    string nextLine = null;
                    while ((nextLine = fileStream.ReadLine()) != null)
                    {
                        if (nextLine.Contains(expectedTimerMessage))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void EmptyDownloadDirectory()
        {
            if (Directory.Exists(this.upgradeDownloadsDirectory))
            {
                Directory.Delete(this.upgradeDownloadsDirectory, recursive: true);
            }

            Directory.CreateDirectory(this.upgradeDownloadsDirectory);
            Directory.Exists(this.upgradeDownloadsDirectory).ShouldBeTrue();
            Directory.EnumerateFiles(this.upgradeDownloadsDirectory).Any().ShouldBeFalse();
        }

        private void CreateUpgradeAvailableMarkerFile()
        {
            string scalarUpgradeAvailableFilePath = Path.Combine(
                Path.GetDirectoryName(this.upgradeDownloadsDirectory),
                HighestAvailableVersionFileName);

            this.EmptyDownloadDirectory();

            this.fileSystem.CreateEmptyFile(scalarUpgradeAvailableFilePath);
            this.fileSystem.FileExists(scalarUpgradeAvailableFilePath).ShouldBeTrue();
        }

        private void SetUpgradeRing(string value)
        {
            this.RunScalar($"config {UpgradeRingKey} {value}");
        }

        private string RunUpgradeCommand()
        {
            return this.RunScalar("upgrade");
        }

        private string RunScalar(string argument)
        {
            ProcessResult result = ProcessHelper.Run(ScalarTestConfig.PathToScalar, argument);
            result.ExitCode.ShouldEqual(0, result.Errors);

            return result.Output;
        }

        private void RestartService()
        {
            ScalarServiceProcess.StopService();
            ScalarServiceProcess.StartService();
        }

        private bool ReminderMessagingEnabled()
        {
            Dictionary<string, string> environmentVariables = new Dictionary<string, string>();
            environmentVariables["Scalar_UPGRADE_DETERMINISTIC"] = "true";
            ProcessResult result = GitHelpers.InvokeGitAgainstScalarRepo(
                                                    this.Enlistment.RepoRoot,
                                                    "status",
                                                    environmentVariables,
                                                    removeWaitingMessages: true,
                                                    removeUpgradeMessages: false);

            if (!string.IsNullOrEmpty(result.Errors) &&
                result.Errors.Contains("A new version of Scalar is available."))
            {
                return true;
            }

            return false;
        }

        private void VerifyServiceRestartStopsReminder()
        {
            this.CreateUpgradeAvailableMarkerFile();
            this.ReminderMessagingEnabled().ShouldBeTrue("Upgrade marker file did not trigger reminder messaging");
            this.SetUpgradeRing(AlwaysUpToDateRing);
            this.RestartService();

            // Wait for sometime so service can detect product is up-to-date and delete left over downloads
            TimeSpan timeToWait = TimeSpan.FromMinutes(1);
            bool reminderMessagingEnabled = true;
            while ((reminderMessagingEnabled = this.ReminderMessagingEnabled()) && timeToWait > TimeSpan.Zero)
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                timeToWait = timeToWait.Subtract(TimeSpan.FromSeconds(5));
            }

            reminderMessagingEnabled.ShouldBeFalse("Service restart did not stop Upgrade reminder messaging");
        }

        private void VerifyUpgradeVerbStopsReminder()
        {
            this.SetUpgradeRing(AlwaysUpToDateRing);
            this.CreateUpgradeAvailableMarkerFile();
            this.ReminderMessagingEnabled().ShouldBeTrue("Marker file did not trigger Upgrade reminder messaging");
            this.RunUpgradeCommand();
            this.ReminderMessagingEnabled().ShouldBeFalse("Upgrade verb did not stop Upgrade reminder messaging");
        }
    }
}
