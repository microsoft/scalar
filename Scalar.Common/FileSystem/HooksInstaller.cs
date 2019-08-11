using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Scalar.Common.FileSystem
{
    public static class HooksInstaller
    {
        private static readonly string ExecutingDirectory;
        private static readonly HookData[] NativeHooks = new[]
        {
            new HookData(ScalarConstants.DotGit.Hooks.ReadObjectName, ScalarConstants.DotGit.Hooks.ReadObjectPath, ScalarPlatform.Instance.Constants.ScalarReadObjectHookExecutableName),
        };

        static HooksInstaller()
        {
            ExecutingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public static bool InstallHooks(ScalarContext context, out string error)
        {
            error = string.Empty;
            try
            {
                foreach (HookData hook in NativeHooks)
                {
                    string installedHookPath = Path.Combine(ExecutingDirectory, hook.ExecutableName);
                    string targetHookPath = Path.Combine(context.Enlistment.WorkingDirectoryBackingRoot, hook.Path + ScalarPlatform.Instance.Constants.ExecutableExtension);
                    if (!TryHooksInstallationAction(() => CopyHook(context, installedHookPath, targetHookPath), out error))
                    {
                        error = "Failed to copy " + installedHookPath + "\n" + error;
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                error = e.ToString();
                return false;
            }

            return true;
        }

        public static bool TryUpdateHooks(ScalarContext context, out string errorMessage)
        {
            errorMessage = string.Empty;
            foreach (HookData hook in NativeHooks)
            {
                if (!TryUpdateHook(context, hook, out errorMessage))
                {
                    return false;
                }
            }

            return true;
        }

        public static void CopyHook(ScalarContext context, string sourcePath, string destinationPath)
        {
            Exception ex;
            if (!context.FileSystem.TryCopyToTempFileAndRename(sourcePath, destinationPath, out ex))
            {
                throw new RetryableException($"Error installing {sourcePath} to {destinationPath}", ex);
            }
        }

        /// <summary>
        /// Try to perform the specified action.  The action will be retried (with backoff) up to 3 times.
        /// </summary>
        /// <param name="action">Action to perform</param>
        /// <param name="errorMessage">Error message</param>
        /// <returns>True if the action succeeded and false otherwise</returns>
        /// <remarks>This method is optimized for the hooks installation process and should not be used
        /// as a generic retry mechanism.  See RetryWrapper for a general purpose retry mechanism</remarks>
        public static bool TryHooksInstallationAction(Action action, out string errorMessage)
        {
            int retriesLeft = 3;
            int retryWaitMillis = 500; // Will grow exponentially on each retry attempt
            errorMessage = null;

            while (true)
            {
                try
                {
                    action();
                    return true;
                }
                catch (RetryableException re)
                {
                    if (retriesLeft == 0)
                    {
                        errorMessage = re.InnerException.ToString();
                        return false;
                    }

                    Thread.Sleep(retryWaitMillis);
                    retriesLeft -= 1;
                    retryWaitMillis *= 2;
                }
                catch (Exception e)
                {
                    errorMessage = e.ToString();
                    return false;
                }
            }
        }

        private static bool TryUpdateHook(
            ScalarContext context,
            HookData hook,
            out string errorMessage)
        {
            bool copyHook = false;
            string enlistmentHookPath = Path.Combine(context.Enlistment.WorkingDirectoryBackingRoot, hook.Path + ScalarPlatform.Instance.Constants.ExecutableExtension);
            string installedHookPath = Path.Combine(ExecutingDirectory, hook.ExecutableName);

            if (!context.FileSystem.FileExists(installedHookPath))
            {
                errorMessage = hook.ExecutableName + " cannot be found at " + installedHookPath;
                return false;
            }

            if (!context.FileSystem.FileExists(enlistmentHookPath))
            {
                copyHook = true;

                EventMetadata metadata = new EventMetadata();
                metadata.Add("Area", "Mount");
                metadata.Add(nameof(enlistmentHookPath), enlistmentHookPath);
                metadata.Add(nameof(installedHookPath), installedHookPath);
                metadata.Add(TracingConstants.MessageKey.WarningMessage, hook.Name + " not found in enlistment, copying from installation folder");
                context.Tracer.RelatedWarning(hook.Name + " MissingFromEnlistment", metadata);
            }
            else
            {
                try
                {
                    FileVersionInfo enlistmentVersion = FileVersionInfo.GetVersionInfo(enlistmentHookPath);
                    FileVersionInfo installedVersion = FileVersionInfo.GetVersionInfo(installedHookPath);
                    copyHook = enlistmentVersion.FileVersion != installedVersion.FileVersion;
                }
                catch (Exception e)
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", "Mount");
                    metadata.Add(nameof(enlistmentHookPath), enlistmentHookPath);
                    metadata.Add(nameof(installedHookPath), installedHookPath);
                    metadata.Add("Exception", e.ToString());
                    context.Tracer.RelatedError(metadata, "Failed to compare " + hook.Name + " version");
                    errorMessage = "Error comparing " + hook.Name + " versions. " + ConsoleHelper.GetScalarLogMessage(context.Enlistment.EnlistmentRoot);
                    return false;
                }
            }

            if (copyHook)
            {
                try
                {
                    CopyHook(context, installedHookPath, enlistmentHookPath);
                }
                catch (Exception e)
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", "Mount");
                    metadata.Add(nameof(enlistmentHookPath), enlistmentHookPath);
                    metadata.Add(nameof(installedHookPath), installedHookPath);
                    metadata.Add("Exception", e.ToString());
                    context.Tracer.RelatedError(metadata, "Failed to copy " + hook.Name + " to enlistment");
                    errorMessage = "Error copying " + hook.Name + " to enlistment. " + ConsoleHelper.GetScalarLogMessage(context.Enlistment.EnlistmentRoot);
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        private class HookData
        {
            public HookData(string name, string path, string executableName)
            {
                this.Name = name;
                this.Path = path;
                this.ExecutableName = executableName;
            }

            public string Name { get; }
            public string Path { get; }
            public string ExecutableName { get; }
        }
    }
}
