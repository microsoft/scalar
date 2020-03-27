using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Scalar.Common.Maintenance
{
    public class ConfigStep : GitMaintenanceStep
    {
        public override string Area => nameof(ConfigStep);

        [Flags]
        private enum GitCoreGVFSFlags
        {
            // GVFS_SKIP_SHA_ON_INDEX
            // Disables the calculation of the sha when writing the index
            SkipShaOnIndex = 1 << 0,

            // GVFS_BLOCK_COMMANDS
            // Blocks git commands that are not allowed in a GVFS/Scalar repo
            BlockCommands = 1 << 1,

            // GVFS_MISSING_OK
            // Normally git write-tree ensures that the objects referenced by the
            // directory exist in the object database.This option disables this check.
            MissingOk = 1 << 2,

            // GVFS_NO_DELETE_OUTSIDE_SPARSECHECKOUT
            // When marking entries to remove from the index and the working
            // directory this option will take into account what the
            // skip-worktree bit was set to so that if the entry has the
            // skip-worktree bit set it will not be removed from the working
            // directory.  This will allow virtualized working directories to
            // detect the change to HEAD and use the new commit tree to show
            // the files that are in the working directory.
            NoDeleteOutsideSparseCheckout = 1 << 3,

            // GVFS_FETCH_SKIP_REACHABILITY_AND_UPLOADPACK
            // While performing a fetch with a virtual file system we know
            // that there will be missing objects and we don't want to download
            // them just because of the reachability of the commits.  We also
            // don't want to download a pack file with commits, trees, and blobs
            // since these will be downloaded on demand.  This flag will skip the
            // checks on the reachability of objects during a fetch as well as
            // the upload pack so that extraneous objects don't get downloaded.
            FetchSkipReachabilityAndUploadPack = 1 << 4,

            // 1 << 5 has been deprecated

            // GVFS_BLOCK_FILTERS_AND_EOL_CONVERSIONS
            // With a virtual file system we only know the file size before any
            // CRLF or smudge/clean filters processing is done on the client.
            // To prevent file corruption due to truncation or expansion with
            // garbage at the end, these filters must not run when the file
            // is first accessed and brought down to the client. Git.exe can't
            // currently tell the first access vs subsequent accesses so this
            // flag just blocks them from occurring at all.
            BlockFiltersAndEolConversions = 1 << 6,

            // GVFS_PREFETCH_DURING_FETCH
            // While performing a `git fetch` command, use the gvfs-helper to
            // perform a "prefetch" of commits and trees.
            PrefetchDuringFetch = 1 << 7,
        }

        private bool? UseGvfsProtocol = true;

        public ConfigStep(ScalarContext context, bool? useGvfsProtocol = null) : base(context, requireObjectCacheLock: false)
        {
            this.UseGvfsProtocol = useGvfsProtocol;
        }

        public override string ProgressMessage => "Setting recommended config settings";

        public bool TrySetConfig(out string error)
        {
            string coreGVFSFlags = Convert.ToInt32(
                GitCoreGVFSFlags.BlockCommands |
                GitCoreGVFSFlags.MissingOk |
                GitCoreGVFSFlags.FetchSkipReachabilityAndUploadPack |
                GitCoreGVFSFlags.PrefetchDuringFetch)
                .ToString();

            string expectedHooksPath = Path.Combine(this.Context.Enlistment.WorkingDirectoryRoot, ScalarConstants.DotGit.Hooks.Root);
            expectedHooksPath = Paths.ConvertPathToGitFormat(expectedHooksPath);

            if (!this.UseGvfsProtocol.HasValue)
            {
                this.UseGvfsProtocol = this.Context.Enlistment.UsesGvfsProtocol;
            }

            // These settings are required for normal Scalar functionality.
            // They will override any existing local configuration values.
            //
            // IMPORTANT! These must parallel the settings in ControlGitRepo:Initialize
            //
            Dictionary<string, string> requiredSettings = new Dictionary<string, string>
            {
                { "am.keepcr", "true" },
                { "core.autocrlf", "false" },
                { "checkout.optimizenewbranch", "true" },
                { "core.fscache", "true" },
                { "core.multiPackIndex", "true" },
                { "core.preloadIndex", "true" },
                { "core.safecrlf", "false" },
                { "core.untrackedCache", ScalarPlatform.Instance.FileSystem.SupportsUntrackedCache ? "true" : "false" },
                { "core.filemode", ScalarPlatform.Instance.FileSystem.SupportsFileMode ? "true" : "false" },
                { "core.bare", "false" },
                { "core.logallrefupdates", "true" },
                { "core.hookspath", expectedHooksPath },
                { GitConfigSetting.CredentialUseHttpPath, "true" },
                { "credential.validate", "false" },
                { "gc.auto", "0" },
                { "gui.gcwarning", "false" },
                { "index.threads", "true" },
                { "index.version", "4" },
                { "merge.stat", "false" },
                { "merge.renames", "false" },
                { "pack.useBitmaps", "false" },
                { "pack.useSparse", "true" },
                { "receive.autogc", "false" },
                { "reset.quiet", "true" },
                { "feature.manyFiles", "false" },
                { "feature.experimental", "false" },
                { "fetch.writeCommitGraph", "false" },
            };

            if (this.UseGvfsProtocol.Value)
            {
                requiredSettings.Add("core.gvfs", coreGVFSFlags);
                requiredSettings.Add(ScalarConstants.GitConfig.UseGvfsHelper, "true");
                requiredSettings.Add("http.version", "HTTP/1.1");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                requiredSettings.Add("http.sslBackend", "schannel");
            }

            // If we do not use the GVFS protocol, then these config settings
            // are in fact optional.
            if (!this.TrySetConfig(requiredSettings, isRequired: this.UseGvfsProtocol.Value, out error))
            {
                error = $"Failed to set some required settings: {error}";
                this.Context.Tracer.RelatedError(error);
                return false;
            }

            // These settings are optional, because they impact performance but not functionality of Scalar.
            // These settings should only be set by the clone or repair verbs, so that they do not
            // overwrite the values set by the user in their local config.
            Dictionary<string, string> optionalSettings = new Dictionary<string, string>
            {
                { "status.aheadbehind", "false" },
            };

            if (!this.TrySetConfig(optionalSettings, isRequired: false, out error))
            {
                error = $"Failed to set some optional settings: {error}";
                this.Context.Tracer.RelatedError(error);
                return false;
            }

            return this.ConfigureWatchmanIntegration(out error);
        }

        protected override void PerformMaintenance()
        {
            this.TrySetConfig(out _);
        }

        private bool TrySetConfig(Dictionary<string, string> configSettings, bool isRequired, out string error)
        {
            Dictionary<string, GitConfigSetting> existingConfigSettings = null;

            GitProcess.Result getConfigResult = this.RunGitCommand(
                process => process.TryGetAllConfig(localOnly: isRequired, configSettings: out existingConfigSettings),
                nameof(GitProcess.TryGetAllConfig));

            // If the settings are required, then only check local config settings, because we don't want to depend on
            // global settings that can then change independent of this repo.
            if (getConfigResult.ExitCodeIsFailure)
            {
                error = "Failed to get all config entries";
                return false;
            }

            foreach (KeyValuePair<string, string> setting in configSettings)
            {
                GitConfigSetting existingSetting;
                if (setting.Value != null)
                {
                    if (!existingConfigSettings.TryGetValue(setting.Key, out existingSetting) ||
                        (isRequired && !existingSetting.HasValue(setting.Value)))
                    {
                        this.Context.Tracer.RelatedInfo($"Setting config value {setting.Key}={setting.Value}");
                        GitProcess.Result setConfigResult = this.RunGitCommand(
                                                                    process => process.SetInLocalConfig(setting.Key, setting.Value),
                                                                    nameof(GitProcess.SetInLocalConfig));
                        if (setConfigResult.ExitCodeIsFailure)
                        {
                            error = setConfigResult.Errors;
                            return false;
                        }
                    }
                }
                else
                {
                    if (existingConfigSettings.TryGetValue(setting.Key, out _))
                    {
                        this.RunGitCommand(
                                process => process.DeleteFromLocalConfig(setting.Key),
                                nameof(GitProcess.DeleteFromLocalConfig));
                    }
                }
            }

            error = null;
            return true;
        }

        private bool ConfigureWatchmanIntegration(out string error)
        {
            string watchmanLocation = ProcessHelper.GetProgramLocation(ScalarPlatform.Instance.Constants.ProgramLocaterCommand, "watchman");
            if (string.IsNullOrEmpty(watchmanLocation))
            {
                this.Context.Tracer.RelatedWarning("Watchman is not installed - skipping Watchman configuration.");
                error = null;
                return true;
            }

            try
            {
                string queryWatchmanPath = Path.Combine(
                    this.Context.Enlistment.WorkingDirectoryRoot,
                    ScalarConstants.DotGit.Hooks.QueryWatchmanPath);

                this.Context.FileSystem.WriteAllText(
                    queryWatchmanPath,
                    queryWatchmanHookData);

                string dotGitRoot = this.Context.Enlistment.DotGitRoot.Replace(Path.DirectorySeparatorChar, ScalarConstants.GitPathSeparator);
                this.RunGitCommand(
                    process => process.SetInLocalConfig("core.fsmonitor", dotGitRoot + "/hooks/query-watchman"),
                    "config");
                this.RunGitCommand(
                    process => process.SetInLocalConfig("core.fsmonitorHookVersion", "2"),
                    "config");

                this.Context.Tracer.RelatedInfo("Watchman configured!");
                error = null;
                return true;
            }
            catch (IOException ex)
            {
                EventMetadata metadata = this.CreateEventMetadata(ex);
                error = $"Failed to configure Watchman integration: {ex.Message}";
                this.Context.Tracer.RelatedError(metadata, error);
                return false;
            }
        }

        private const string queryWatchmanHookData = @"#!/usr/bin/perl

use strict;
use warnings;
use IPC::Open2;

# An example hook script to integrate Watchman
# (https://facebook.github.io/watchman/) with git to speed up detecting
# new and modified files.
#
# The hook is passed a version (currently 2) and last update token
# formatted as a string and outputs to stdout a new update token and
# all files that have been modified since the update token. Paths must
# be relative to the root of the working tree and separated by a single NUL.
#
# To enable this hook, rename this file to ""query-watchman"" and set
# 'git config core.fsmonitor .git/hooks/query-watchman'
#
my ($version, $last_update_token) = @ARGV;

# Uncomment for debugging
# print STDERR ""$0 $version $last_update_token\n"";

# Check the hook interface version
if ($version ne 2) {
	die ""Unsupported query-fsmonitor hook version '$version'.\n"" .
	    ""Falling back to scanning...\n"";
}

my $git_work_tree = get_working_dir();

my $retry = 1;

my $json_pkg;
eval {
	require JSON::XS;
	$json_pkg = ""JSON::XS"";
	1;
} or do {
	require JSON::PP;
	$json_pkg = ""JSON::PP"";
};

launch_watchman();

sub launch_watchman {
	my $o = watchman_query();
	if (is_work_tree_watched($o)) {
		output_result($o->{clock}, @{$o->{files}});
	}
}

sub output_result {
	my ($clockid, @files) = @_;

	# Uncomment for debugging watchman output
	# open (my $fh, "">"", "".git/watchman-output.out"");
	# binmode $fh, "":utf8"";
	# print $fh ""$clockid\n@files\n"";
	# close $fh;

	binmode STDOUT, "":utf8"";
	print $clockid;
	print ""\0"";
	local $, = ""\0"";
	print @files;
}

sub watchman_clock {
	my $response = qx/watchman clock ""$git_work_tree""/;
	die ""Failed to get clock id on '$git_work_tree'.\n"" .
		""Falling back to scanning...\n"" if $? != 0;

	return $json_pkg->new->utf8->decode($response);
}

sub watchman_query {
	my $pid = open2(\*CHLD_OUT, \*CHLD_IN, 'watchman -j --no-pretty')
	or die ""open2() failed: $!\n"" .
	""Falling back to scanning...\n"";

	# In the query expression below we're asking for names of files that
	# changed since $last_update_token but not from the .git folder.
	#
	# To accomplish this, we're using the ""since"" generator to use the
	# recency index to select candidate nodes and ""fields"" to limit the
	# output to file names only. Then we're using the ""expression"" term to
	# further constrain the results.
	if (substr($last_update_token, 0, 1) eq ""c"") {
		$last_update_token = ""\""$last_update_token\"""";
	}
	my $query = <<""	END"";
		[""query"", ""$git_work_tree"", {
			""since"": $last_update_token,
			""fields"": [""name""],
			""expression"": [""not"", [""dirname"", "".git""]]
		}]
	END

	# Uncomment for debugging the watchman query
	# open (my $fh, "">"", "".git/watchman-query.json"");
	# print $fh $query;
	# close $fh;

	print CHLD_IN $query;
	close CHLD_IN;
	my $response = do {local $/; <CHLD_OUT>};

	# Uncomment for debugging the watch response
	# open ($fh, "">"", "".git/watchman-response.json"");
	# print $fh $response;
	# close $fh;

	die ""Watchman: command returned no output.\n"" .
	""Falling back to scanning...\n"" if $response eq """";
	die ""Watchman: command returned invalid output: $response\n"" .
	""Falling back to scanning...\n"" unless $response =~ /^\{/;

	return $json_pkg->new->utf8->decode($response);
}

sub is_work_tree_watched {
	my ($output) = @_;
	my $error = $output->{error};
	if ($retry > 0 and $error and $error =~ m/unable to resolve root .* directory (.*) is not watched/) {
		$retry--;
		my $response = qx/watchman watch ""$git_work_tree""/;
		die ""Failed to make watchman watch '$git_work_tree'.\n"" .
		    ""Falling back to scanning...\n"" if $? != 0;
		$output = $json_pkg->new->utf8->decode($response);
		$error = $output->{error};
		die ""Watchman: $error.\n"" .
		""Falling back to scanning...\n"" if $error;

		# Uncomment for debugging watchman output
		# open (my $fh, "">"", "".git/watchman-output.out"");
		# close $fh;

		# Watchman will always return all files on the first query so
		# return the fast ""everything is dirty"" flag to git and do the
		# Watchman query just to get it over with now so we won't pay
		# the cost in git to look up each individual file.
		my $o = watchman_clock();
		$error = $output->{error};

		die ""Watchman: $error.\n"" .
		""Falling back to scanning...\n"" if $error;

		output_result($o->{clock}, (""/""));
		$last_update_token = $o->{clock};

		eval { launch_watchman() };
		return 0;
	}

	die ""Watchman: $error.\n"" .
	""Falling back to scanning...\n"" if $error;

	return 1;
}

sub get_working_dir {
	my $working_dir;
	if ($^O =~ 'msys' || $^O =~ 'cygwin') {
		$working_dir = Win32::GetCwd();
		$working_dir =~ tr/\\/\//;
	} else {
		require Cwd;
		$working_dir = Cwd::cwd();
	}

	return $working_dir;
}";
    }
}
