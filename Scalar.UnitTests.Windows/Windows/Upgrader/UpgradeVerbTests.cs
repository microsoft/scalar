using NUnit.Framework;
using Scalar.CommandLine;
using Scalar.Common;
using Scalar.Tests.Should;
using Scalar.UnitTests.Category;
using Scalar.UnitTests.Mock.Upgrader;
using Scalar.UnitTests.Upgrader;
using System.Collections.Generic;

namespace Scalar.UnitTests.Windows.Upgrader
{
    [TestFixture]
    public class UpgradeVerbTests : UpgradeTests
    {
        private MockProcessLauncher processLauncher;
        private UpgradeVerb upgradeVerb;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            this.processLauncher = new MockProcessLauncher(exitCode: 0, hasExited: true, startResult: true);
            this.upgradeVerb = new UpgradeVerb(
                this.Upgrader,
                this.Tracer,
                this.FileSystem,
                this.PrerunChecker,
                this.processLauncher,
                this.Output);
            this.upgradeVerb.Confirmed = false;
            this.PrerunChecker.SetCommandToRerun("`scalar upgrade`");
        }

        [TestCase]
        public void UpgradeAvailabilityReporting()
        {
            this.ConfigureRunAndVerify(
                configure: () =>
                {
                    this.SetUpgradeRing("Slow");
                    this.Upgrader.PretendNewReleaseAvailableAtRemote(
                        upgradeVersion: NewerThanLocalVersion,
                        remoteRing: GitHubUpgrader.GitHubUpgraderConfig.RingType.Slow);
                },
                expectedReturn: ReturnCode.Success,
                expectedOutput: new List<string>
                {
                    "New Scalar version " + NewerThanLocalVersion + " available in ring Slow",
                    "When ready, run `scalar upgrade --confirm` from an elevated command prompt."
                },
                expectedErrors: null);
        }

        [TestCase]
        public void DowngradePrevention()
        {
            this.ConfigureRunAndVerify(
                configure: () =>
                {
                    this.SetUpgradeRing("Slow");
                    this.Upgrader.PretendNewReleaseAvailableAtRemote(
                        upgradeVersion: OlderThanLocalVersion,
                        remoteRing: GitHubUpgrader.GitHubUpgraderConfig.RingType.Slow);
                },
                expectedReturn: ReturnCode.Success,
                expectedOutput: new List<string>
                {
                    "Checking for Scalar upgrades...Succeeded",
                    "Great news, you're all caught up on upgrades in the Slow ring!"
                },
                expectedErrors: null);
        }

        [TestCase]
        public void LaunchInstaller()
        {
            this.ConfigureRunAndVerify(
                configure: () =>
                {
                    this.SetUpgradeRing("Slow");
                    this.upgradeVerb.Confirmed = true;
                    this.PrerunChecker.SetCommandToRerun("`scalar upgrade --confirm`");
                },
                expectedReturn: ReturnCode.Success,
                expectedOutput: new List<string>
                {
                    "New Scalar version " + NewerThanLocalVersion + " available in ring Slow",
                    "Launching upgrade tool..."
                },
                expectedErrors: null);

            this.processLauncher.IsLaunched.ShouldBeTrue();
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public override void NoneLocalRing()
        {
            base.NoneLocalRing();
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public override void InvalidUpgradeRing()
        {
            base.InvalidUpgradeRing();
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void CopyTools()
        {
            this.ConfigureRunAndVerify(
                configure: () =>
                {
                    this.SetUpgradeRing("Slow");
                    this.Upgrader.SetFailOnAction(MockGitHubUpgrader.ActionType.CopyTools);
                    this.upgradeVerb.Confirmed = true;
                    this.PrerunChecker.SetCommandToRerun("`scalar upgrade --confirm`");
                },
                expectedReturn: ReturnCode.GenericError,
                expectedOutput: new List<string>
                {
                    "Could not launch upgrade tool. Unable to copy upgrader tools"
                },
                expectedErrors: new List<string>
                {
                    "Could not launch upgrade tool. Unable to copy upgrader tools"
                });
        }

        [TestCase]
        public void IsScalarServiceRunningPreCheck()
        {
            this.PrerunChecker.SetCommandToRerun("`scalar upgrade --confirm`");
            this.ConfigureRunAndVerify(
                configure: () =>
                {
                    this.upgradeVerb.Confirmed = true;
                    this.PrerunChecker.SetReturnTrueOnCheck(MockInstallerPrerunChecker.FailOnCheckType.IsServiceInstalledAndNotRunning);
                },
                expectedReturn: ReturnCode.GenericError,
                expectedOutput: new List<string>
                {
                    "Scalar Service is not running.",
                    "Run `sc start Scalar.Service` and run `scalar upgrade --confirm` again from an elevated command prompt."
                },
                expectedErrors: null,
                expectedWarnings: new List<string>
                {
                    "Scalar Service is not running."
                });
        }

        [TestCase]
        public void ElevatedRunPreCheck()
        {
            this.PrerunChecker.SetCommandToRerun("`scalar upgrade --confirm`");
            this.ConfigureRunAndVerify(
                configure: () =>
                {
                    this.upgradeVerb.Confirmed = true;
                    this.PrerunChecker.SetReturnFalseOnCheck(MockInstallerPrerunChecker.FailOnCheckType.IsElevated);
                },
                expectedReturn: ReturnCode.GenericError,
                expectedOutput: new List<string>
                {
                    "The installer needs to be run from an elevated command prompt.",
                    "Run `scalar upgrade --confirm` again from an elevated command prompt."
                },
                expectedErrors: null,
                expectedWarnings: new List<string>
                {
                    "The installer needs to be run from an elevated command prompt."
                });
        }

        [TestCase]
        public void UnAttendedModePreCheck()
        {
            this.ConfigureRunAndVerify(
                configure: () =>
                {
                    this.upgradeVerb.Confirmed = true;
                    this.PrerunChecker.SetReturnTrueOnCheck(MockInstallerPrerunChecker.FailOnCheckType.UnattendedMode);
                },
                expectedReturn: ReturnCode.GenericError,
                expectedOutput: new List<string>
                {
                    "`scalar upgrade` is not supported in unattended mode"
                },
                expectedErrors: null,
                expectedWarnings: new List<string>
                {
                    "`scalar upgrade` is not supported in unattended mode"
                });
        }

        [TestCase]
        public void DryRunLaunchesUpgradeTool()
        {
            this.ConfigureRunAndVerify(
                configure: () =>
                {
                    this.upgradeVerb.DryRun = true;
                    this.SetUpgradeRing("Slow");
                    this.Upgrader.PretendNewReleaseAvailableAtRemote(
                        upgradeVersion: NewerThanLocalVersion,
                        remoteRing: GitHubUpgrader.GitHubUpgraderConfig.RingType.Slow);
                },
                expectedReturn: ReturnCode.Success,
                expectedOutput: new List<string>
                {
                    "Installer launched in a new window."
                },
                expectedErrors: null);
        }

        protected override ReturnCode RunUpgrade()
        {
            try
            {
                this.upgradeVerb.Execute();
            }
            catch (ScalarVerb.VerbAbortedException)
            {
                // ignore. exceptions are expected while simulating some failures.
            }

            return this.upgradeVerb.ReturnCode;
        }
    }
}
