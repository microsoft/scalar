using Scalar.CommandLine;
using Scalar.Common;
using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System.Collections.Generic;
using System.IO;

namespace Scalar.RepairJobs
{
    public class GitConfigRepairJob : RepairJob
    {
        public GitConfigRepairJob(ITracer tracer, TextWriter output, ScalarEnlistment enlistment)
            : base(tracer, output, enlistment)
        {
        }

        public override string Name
        {
            get { return ".git\\config"; }
        }

        public override IssueType HasIssue(List<string> messages)
        {
            GitProcess git = new GitProcess(this.Enlistment);
            GitProcess.ConfigResult originResult = git.GetOriginUrl();
            string error;
            string originUrl;
            if (!originResult.TryParseAsString(out originUrl, out error))
            {
                if (error.Contains("--local"))
                {
                    // example error: '--local can only be used inside a git repository'
                    // Corrupting the git config does not cause git to not recognize the current folder as "not a git repository".
                    // This is a symptom of deeper issues such as missing HEAD file or refs folders.
                    messages.Add("An issue was found that may be a side-effect of other issues. Fix them with 'scalar repair --confirm' then 'scalar repair' again.");
                    return IssueType.CantFix;
                }

                messages.Add("Could not read origin url: " + error);
                return IssueType.Fixable;
            }

            if (originUrl == null)
            {
                messages.Add("Remote 'origin' is not configured for this repo. You can fix this by running 'git remote add origin <repourl>'");
                return IssueType.CantFix;
            }

            // We've validated the repo URL, so now make sure we can authenticate
            try
            {
                ScalarEnlistment enlistment = ScalarEnlistment.CreateFromDirectory(
                    this.Enlistment.EnlistmentRoot,
                    this.Enlistment.GitBinPath,
                    authentication: null);

                string authError;
                if (!enlistment.Authentication.TryInitialize(this.Tracer, enlistment, out authError))
                {
                    messages.Add("Authentication failed. Run 'scalar log' for more info.");
                    messages.Add(".git\\config is valid and remote 'origin' is set, but may have a typo:");
                    messages.Add(originUrl.Trim());
                    return IssueType.CantFix;
                }
            }
            catch (InvalidRepoException)
            {
                messages.Add("An issue was found that may be a side-effect of other issues. Fix them with 'scalar repair --confirm' then 'scalar repair' again.");
                return IssueType.CantFix;
            }

            return IssueType.None;
        }

        public override FixResult TryFixIssues(List<string> messages)
        {
            string configPath = Path.Combine(this.Enlistment.WorkingDirectoryRoot, ScalarConstants.DotGit.Config);
            string configBackupPath;
            if (!this.TryRenameToBackupFile(configPath, out configBackupPath, messages))
            {
                return FixResult.Failure;
            }

            File.WriteAllText(configPath, string.Empty);
            this.Tracer.RelatedInfo("Created empty file: " + configPath);

            if (!ScalarVerb.TrySetRequiredGitConfigSettings(this.Enlistment) ||
                !ScalarVerb.TrySetOptionalGitConfigSettings(this.Enlistment))
            {
                messages.Add("Unable to create default .git\\config.");
                this.RestoreFromBackupFile(configBackupPath, configPath, messages);

                return FixResult.Failure;
            }

            // Don't output the validation output unless it turns out we couldn't fix the problem
            List<string> validationMessages = new List<string>();

            // HasIssue should return CantFix because we can't set the repo url ourselves,
            // but getting Fixable means that we still failed
            if (this.HasIssue(validationMessages) == IssueType.Fixable)
            {
                messages.Add("Reinitializing the .git\\config did not fix the issue. Check the errors below for more details:");
                messages.AddRange(validationMessages);

                this.RestoreFromBackupFile(configBackupPath, configPath, messages);

                return FixResult.Failure;
            }

            if (!this.TryDeleteFile(configBackupPath))
            {
                messages.Add("Failed to delete .git\\config backup file: " + configBackupPath);
            }

            messages.Add("Reinitialized .git\\config. You will need to manually add the origin remote by running");
            messages.Add("git remote add origin <repo url>");
            messages.Add("If you previously configured a custom cache server, you will need to configure it again.");

            return FixResult.ManualStepsRequired;
        }
    }
}
