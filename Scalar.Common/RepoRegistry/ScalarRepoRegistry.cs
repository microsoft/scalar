using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace Scalar.Common.RepoRegistry
{
    public class ScalarRepoRegistry : IScalarRepoRegistry
    {
        private const string EtwArea = nameof(ScalarRepoRegistry);
        private const string RegistryFileExtension = ".repo";
        private const string RegistryTempFileExtension = ".temp";

        private string registryFolderPath;
        private ITracer tracer;
        private PhysicalFileSystem fileSystem;

        public ScalarRepoRegistry(
            ITracer tracer,
            PhysicalFileSystem fileSystem,
            string repoRegistryLocation)
        {
            this.tracer = tracer;
            this.fileSystem = fileSystem;
            this.registryFolderPath = repoRegistryLocation;
        }

        public bool TryRegisterRepo(string normalizedRepoRoot, string userId, out string errorMessage)
        {
            if (!this.TryCreateRepoRegistryDirectory(out errorMessage))
            {
                return false;
            }

            string tempRegistryPath = this.GetRepoRegistryTempFilePath(normalizedRepoRoot);

            try
            {
                ScalarRepoRegistration repoRegistration = new ScalarRepoRegistration(normalizedRepoRoot, userId);
                string registryFileContents = repoRegistration.ToJson();

                using (Stream tempFile = this.fileSystem.OpenFileStream(
                    tempRegistryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    callFlushFileBuffers: true))
                using (StreamWriter writer = new StreamWriter(tempFile))
                {
                    writer.WriteLine(registryFileContents);
                    tempFile.Flush();
                }
            }
            catch (Exception e)
            {
                errorMessage = $"Error while registering repo {normalizedRepoRoot}: {e.Message}";

                EventMetadata metadata = CreateEventMetadata(e);
                metadata.Add(nameof(normalizedRepoRoot), normalizedRepoRoot);
                metadata.Add(nameof(tempRegistryPath), tempRegistryPath);
                this.tracer.RelatedError(metadata, $"{nameof(this.TryRegisterRepo)}: Exception while writing temp registry file");
                return false;
            }

            string registryFilePath = this.GetRepoRegistryFilePath(normalizedRepoRoot);
            try
            {
                this.fileSystem.MoveAndOverwriteFile(tempRegistryPath, registryFilePath);
            }
            catch (Win32Exception e)
            {
                errorMessage = $"Error while registering repo {normalizedRepoRoot}: {e.Message}";

                EventMetadata metadata = CreateEventMetadata(e);
                metadata.Add(nameof(normalizedRepoRoot), normalizedRepoRoot);
                metadata.Add(nameof(tempRegistryPath), tempRegistryPath);
                metadata.Add(nameof(registryFilePath), registryFilePath);
                this.tracer.RelatedError(metadata, $"{nameof(this.TryRegisterRepo)}: Exception while renaming temp registry file");
                return false;
            }

            errorMessage = null;
            return true;
        }

        public bool TryUnregisterRepo(string normalizedRepoRoot, out string errorMessage)
        {
            string registryPath = this.GetRepoRegistryFilePath(normalizedRepoRoot);
            if (!this.fileSystem.FileExists(registryPath))
            {
                errorMessage = $"Attempted to remove non-existent repo '{normalizedRepoRoot}'";

                EventMetadata metadata = CreateEventMetadata();
                metadata.Add(nameof(normalizedRepoRoot), normalizedRepoRoot);
                metadata.Add(nameof(registryPath), registryPath);
                this.tracer.RelatedWarning(
                    metadata,
                    $"{nameof(this.TryUnregisterRepo)}: Attempted to remove non-existent repo");

                return false;
            }

            try
            {
                this.fileSystem.DeleteFile(registryPath);
            }
            catch (Exception e)
            {
                errorMessage = $"Error while removing repo {normalizedRepoRoot}: {e.Message}";

                EventMetadata metadata = CreateEventMetadata(e);
                metadata.Add(nameof(normalizedRepoRoot), normalizedRepoRoot);
                metadata.Add(nameof(registryPath), registryPath);
                this.tracer.RelatedWarning(
                    metadata,
                    $"{nameof(this.TryUnregisterRepo)}: Exception while removing repo");

                return false;
            }

            errorMessage = null;
            return true;
        }

        public bool TryPauseMaintenanceUntil(DateTime time, out string errorMessage)
        {
            if (!this.TryCreateRepoRegistryDirectory(out errorMessage))
            {
                return false;
            }

            string timeFileName = this.GetMaintenanceDelayFilePath();
            long seconds = EpochConverter.ToUnixEpochSeconds(time);

            if (!this.fileSystem.TryWriteAllText(timeFileName, seconds.ToString()))
            {
                errorMessage = $"Failed to write epoch {seconds} to '{timeFileName}'";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public bool TryRemovePauseFile(out string errorMessage)
        {
            string timeFileName = this.GetMaintenanceDelayFilePath();

            if (this.fileSystem.FileExists(timeFileName) && !this.fileSystem.TryDeleteFile(timeFileName))
            {
                errorMessage = $"Failed to delete '{timeFileName}'";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public bool TryGetMaintenanceDelayTime(out DateTime time)
        {
            string timeFileName = this.GetMaintenanceDelayFilePath();

            if (this.fileSystem.FileExists(timeFileName))
            {
                try
                {
                    string contents = this.fileSystem.ReadAllText(timeFileName);

                    if (long.TryParse(contents, out long seconds))
                    {
                        time = EpochConverter.FromUnixEpochSeconds(seconds);
                        return true;
                    }
                }
                catch (IOException)
                {
                    // Eat any issue reading this file
                }
            }

            time = DateTime.MinValue;
            return false;
        }

        public IEnumerable<ScalarRepoRegistration> GetRegisteredRepos()
        {
            if (this.fileSystem.DirectoryExists(this.registryFolderPath))
            {
                IEnumerable<string> registryFilePaths = this.fileSystem.EnumerateFiles(this.registryFolderPath, $"*{RegistryFileExtension}");
                foreach (string registryFilePath in registryFilePaths)
                {
                    ScalarRepoRegistration registration = null;
                    try
                    {
                        string repoData = this.fileSystem.ReadAllText(registryFilePath);
                        registration = ScalarRepoRegistration.FromJson(repoData);
                    }
                    catch (Exception e)
                    {
                        EventMetadata metadata = CreateEventMetadata(e);
                        metadata.Add(nameof(registryFilePath), registryFilePath);
                        this.tracer.RelatedWarning(
                            metadata,
                            $"{nameof(this.GetRegisteredRepos)}: Failed to read registry file");
                    }

                    if (registration != null)
                    {
                        yield return registration;
                    }
                }
            }
        }

        internal static string GetRepoRootSha(string normalizedRepoRoot)
        {
            return SHA1Util.SHA1HashStringForUTF8String(normalizedRepoRoot.ToLowerInvariant());
        }

        private static EventMetadata CreateEventMetadata(Exception e = null)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add("Area", EtwArea);
            if (e != null)
            {
                metadata.Add("Exception", e.ToString());
            }

            return metadata;
        }

        private bool TryCreateRepoRegistryDirectory(out string errorMessage)
        {
            try
            {
                if (!this.fileSystem.DirectoryExists(this.registryFolderPath))
                {
                    EventMetadata metadata = CreateEventMetadata();
                    metadata.Add(nameof(this.registryFolderPath), this.registryFolderPath);
                    this.tracer.RelatedEvent(
                        EventLevel.Informational,
                        $"{nameof(this.TryRegisterRepo)}_CreatingRegistryDirectory",
                        metadata);

                    // TODO #136: Make sure this does the right thing with ACLs on Windows
                    this.fileSystem.CreateDirectory(this.registryFolderPath);
                }
            }
            catch (Exception e)
            {
                errorMessage = $"Error while ensuring registry directory '{this.registryFolderPath}' exists: {e.Message}";

                EventMetadata metadata = CreateEventMetadata(e);
                metadata.Add(nameof(this.registryFolderPath), this.registryFolderPath);
                this.tracer.RelatedError(metadata, $"{nameof(this.TryRegisterRepo)}: Exception while ensuring registry directory exists");
                return false;
            }

            errorMessage = null;
            return true;
        }

        private string GetRepoRegistryTempFilePath(string normalizedRepoRoot)
        {
            string repoTempFilename = $"{GetRepoRootSha(normalizedRepoRoot)}{RegistryTempFileExtension}";
            return Path.Combine(this.registryFolderPath, repoTempFilename);
        }

        private string GetRepoRegistryFilePath(string normalizedRepoRoot)
        {
            string repoFilename = $"{GetRepoRootSha(normalizedRepoRoot)}{RegistryFileExtension}";
            return Path.Combine(this.registryFolderPath, repoFilename);
        }

        private string GetMaintenanceDelayFilePath()
        {
            return Path.Combine(this.registryFolderPath, "maintenance-pause.time");
        }
    }
}
