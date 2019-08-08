﻿using GVFS.FunctionalTests.Properties;
using NUnit.Framework;

namespace GVFS.FunctionalTests.Tests.GitCommands
{
    [TestFixtureSource(typeof(GitRepoTests), nameof(GitRepoTests.ValidateWorkingTree))]
    [Category(Categories.GitCommands)]
    [Category(Categories.NeedsUpdatesForNonVirtualizedMode)]
    public class EnumerationMergeTest : GitRepoTests
    {
        // Commit that found GvFlt Bug 12258777: Entries are sometimes skipped during
        // enumeration when they don't fit in a user's buffer
        private const string EnumerationReproCommitish = "FunctionalTests/20170602";

        public EnumerationMergeTest(Settings.ValidateWorkingTreeMode validateWorkingTree)
            : base(enlistmentPerTest: true, validateWorkingTree: validateWorkingTree)
        {
        }

        [TestCase]
        public void ConfirmEnumerationMatches()
        {
            this.ControlGitRepo.Fetch(GitRepoTests.ConflictSourceBranch);
            this.ValidateGitCommand("checkout " + GitRepoTests.ConflictSourceBranch);
        }

        protected override void CreateEnlistment()
        {
            this.CreateEnlistment(EnumerationReproCommitish);
        }
    }
}
