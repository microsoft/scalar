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
        private Dictionary<string, GitConfigSetting> existingConfigSettings = null;

        public ConfigStep(ScalarContext context, bool? useGvfsProtocol = null, GitFeatureFlags gitFeatures = GitFeatureFlags.None)
            : base(context, requireObjectCacheLock: false, gitFeatures: gitFeatures)
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
                { "core.fscache", "true" },
                { "core.multiPackIndex", "true" },
                { "core.preloadIndex", "true" },
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
                { "fetch.unpackLimit", "1" },
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
                { "core.autocrlf", "false" },
                { "core.safecrlf", "false" },
                { "core.repositoryFormatVersion", "1" },
                { "maintenance.gc.enabled", "false" },
                { "maintenance.prefetch.enabled", "true" },
                { "maintenance.prefetch.auto", "0" },
                { "maintenance.prefetch.schedule", "hourly" },
                { "maintenance.commit-graph.enabled", "true" },
                { "maintenance.commit-graph.auto", "0" },
                { "maintenance.commit-graph.schedule", "hourly" },
                { "maintenance.loose-objects.enabled", "true" },
                { "maintenance.loose-objects.auto", "0" },
                { "maintenance.loose-objects.schedule", "daily" },
                { "maintenance.incremental-repack.enabled", "true" },
                { "maintenance.incremental-repack.auto", "0" },
                { "maintenance.incremental-repack.schedule", "daily" },
            };

            if (this.UseGvfsProtocol.Value)
            {
                // If a user's global config has "status.submoduleSummary=true", then
                // that could slow "git status" significantly here, even though the
                // GVFS protocol forbids submodules. Still, disable it optionally in
                // case the user really wants it in their local config.
                optionalSettings.Add("status.submoduleSummary", "false");
            }

            if (!this.TrySetConfig(optionalSettings, isRequired: false, out error))
            {
                error = $"Failed to set some optional settings: {error}";
                this.Context.Tracer.RelatedError(error);
                return false;
            }

            string excludeDecoration = "log.excludeDecoration";
            List<string> excludeValues = new List<string>
            {
                "refs/scalar/*",
                "refs/prefetch/*",
            };

            if (!this.TrySetMultiConfig(excludeDecoration, excludeValues, out error))
            {
                error = $"Failed to set some multi-value settings: {error}";
                this.Context.Tracer.RelatedError(error);
                return false;
            }

            if (!this.TryStartBackgroundMaintenance(out error))
            {
                error = $"Failed to start background maintenance: {error}";
                this.Context.Tracer.RelatedError(error);
                return false;
            }

            GitProcess.ConfigResult config = null;
            GitProcess.Result getResult = this.RunGitCommand(
                process => {
                    config = process.GetFromLocalConfig("feature.scalar");
                    return null;
                },
                nameof(GitProcess.GetFromLocalConfig)
            );
            GitFeatureFlags flags = GitVersion.GetAvailableGitFeatures(this.Context.Tracer);
            if (!config.TryParseAsString(out string scalar, out error, defaultValue: "true"))
            {
                string envVar = Environment.GetEnvironmentVariable("SCALAR_FUNCTIONAL_TEST_EXPERIMENTAL");

                if (bool.TryParse(envVar, out bool result) && result)
                {
                    scalar = "experimental";
                }
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(scalar, "false"))
            {
                GitProcess.Result deleteResult = this.RunGitCommand(
                    process => process.DeleteFromLocalConfig("core.fsmonitor"),
                    nameof(GitProcess.DeleteFromLocalConfig)
                );

                return deleteResult.ExitCodeIsSuccess;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(scalar, "experimental")
                     // Make sure Git supports builtin FS Monitor
                     && flags.HasFlag(GitFeatureFlags.BuiltinFSMonitor)
                     // but not on Linux yet
                      && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // ":internal:" is a custom value to specify the builtin
                // FS Monitor feature.
                GitProcess.Result setResult = this.RunGitCommand(
                    process => process.SetInLocalConfig("core.fsmonitor", ":internal:"),
                    nameof(GitProcess.SetInLocalConfig)
                );

                return setResult.ExitCodeIsSuccess;
            }
            else
            {
                return this.ConfigureWatchmanIntegration(out error);
            }
        }

        protected override void PerformMaintenance()
        {
            this.TrySetConfig(out _);
        }

        private Dictionary<string, GitConfigSetting> GetConfigSettings()
        {
            if (this.existingConfigSettings != null)
            {
                return this.existingConfigSettings;
            }

            GitProcess.Result getConfigResult = this.RunGitCommand(
                process => process.TryGetAllConfig(localOnly: true, configSettings: out this.existingConfigSettings),
                nameof(GitProcess.TryGetAllConfig));

            return this.existingConfigSettings;
        }

        private bool TrySetConfig(Dictionary<string, string> configSettings, bool isRequired, out string error, bool add = false)
        {
            Dictionary<string, GitConfigSetting> existingConfigSettings = this.GetConfigSettings();

            // If the settings are required, then only check local config settings, because we don't want to depend on
            // global settings that can then change independent of this repo.
            if (existingConfigSettings == null)
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
                                                                    process => process.SetInLocalConfig(setting.Key, setting.Value, add: add),
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

        private bool TrySetMultiConfig(string key, List<string> values, out string error)
        {
            GitProcess.Result result = this.RunGitCommand(process => process.GetMultiConfig(key),
                                                                     nameof(GitProcess.GetMultiConfig));

            // Note: if the result fails, then it means there are no matching values.
            GitProcess.MultiConfigResult configResult = new GitProcess.MultiConfigResult(result);

            foreach (string value in values)
            {
                if (!configResult.Values.Contains(value))
                {
                    this.Context.Tracer.RelatedInfo($"Adding config value {key}={value}");
                    GitProcess.Result setConfigResult = this.RunGitCommand(
                                                                process => process.SetInLocalConfig(key, value, add: true),
                                                                nameof(GitProcess.SetInLocalConfig));
                    if (setConfigResult.ExitCodeIsFailure)
                    {
                        error = setConfigResult.Errors;
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private bool TryStartBackgroundMaintenance(out string error)
        {
            if (!this.GitFeatures.HasFlag(GitFeatureFlags.MaintenanceBuiltin))
            {
                // No error, so move forward.
                error = null;
                return true;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // We still use Scalar.Service on this platform
                error = null;
                return true;
            }

            GitProcess.Result result = this.RunGitCommand(process => process.MaintenanceStart(), nameof(GitProcess.MaintenanceStart));
            error = result.Errors;
            return result.ExitCodeIsSuccess;
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
                string hooksDir = ScalarPlatform.Instance.GetTemplateHooksDirectory();

                if (string.IsNullOrEmpty(hooksDir))
                {
                    error = "Could not find hook templates directory. Skipping watchman integration.";
                    this.Context.Tracer.RelatedError(error);
                    return false;
                }

                // Install query-watchman hook from latest Git path
                string fsMonitorWatchmanSampleHookPath = Path.Combine(
                    hooksDir,
                    ScalarConstants.DotGit.Hooks.FsMonitorWatchmanSampleName);

                string queryWatchmanPath = Path.Combine(
                    this.Context.Enlistment.WorkingDirectoryRoot,
                    ScalarConstants.DotGit.Hooks.QueryWatchmanPath);

                this.Context.FileSystem.CopyFile(
                    fsMonitorWatchmanSampleHookPath,
                    queryWatchmanPath,
                    overwrite: true);

                string dotGitRoot = Paths.ConvertPathToGitFormat(this.Context.Enlistment.DotGitRoot);
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
    }
}
