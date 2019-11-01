using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

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

        public bool TryRegisterRepo(string repoRoot, string ownerSID, out string errorMessage)
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
                metadata.Add(nameof(repoRoot), repoRoot);
                metadata.Add(nameof(this.registryFolderPath), this.registryFolderPath);
                this.tracer.RelatedError(metadata, $"{nameof(this.TryRegisterRepo)}: Exception while ensuring registry directory exists");
                return false;
            }

            string tempRegistryPath = this.GetRepoRegistryTempFilePath(repoRoot);

            try
            {
                ScalarRepoRegistration repoRegistration = new ScalarRepoRegistration(repoRoot, ownerSID);
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
                errorMessage = $"Error while registering repo {repoRoot}: {e.Message}";

                EventMetadata metadata = CreateEventMetadata(e);
                metadata.Add(nameof(repoRoot), repoRoot);
                metadata.Add(nameof(tempRegistryPath), tempRegistryPath);
                this.tracer.RelatedError(metadata, $"{nameof(this.TryRegisterRepo)}: Exception while writing temp registry file");
                return false;
            }

            string registryFilePath = this.GetRepoRegistryFilePath(repoRoot);
            try
            {
                this.fileSystem.MoveAndOverwriteFile(tempRegistryPath, registryFilePath);
            }
            catch (Win32Exception e)
            {
                errorMessage = $"Error while registering repo {repoRoot}: {e.Message}";

                EventMetadata metadata = CreateEventMetadata(e);
                metadata.Add(nameof(repoRoot), repoRoot);
                metadata.Add(nameof(tempRegistryPath), tempRegistryPath);
                metadata.Add(nameof(registryFilePath), registryFilePath);
                this.tracer.RelatedError(metadata, $"{nameof(this.TryRegisterRepo)}: Exception while renaming temp registry file");
                return false;
            }

            errorMessage = null;
            return true;
        }

        public bool TryRemoveRepo(string repoRoot, out string errorMessage)
        {
            string registryPath = this.GetRepoRegistryFilePath(repoRoot);
            if (!this.fileSystem.FileExists(registryPath))
            {
                errorMessage = $"Attempted to remove non-existent repo '{repoRoot}'";

                EventMetadata metadata = CreateEventMetadata();
                metadata.Add(nameof(repoRoot), repoRoot);
                metadata.Add(nameof(registryPath), registryPath);
                this.tracer.RelatedWarning(
                    metadata,
                    $"{nameof(this.TryRemoveRepo)}: Attempted to remove non-existent repo");

                return false;
            }

            try
            {
                this.fileSystem.DeleteFile(registryPath);
            }
            catch (Exception e)
            {
                errorMessage = $"Error while removing repo {repoRoot}: {e.Message}";

                EventMetadata metadata = CreateEventMetadata(e);
                metadata.Add(nameof(repoRoot), repoRoot);
                metadata.Add(nameof(registryPath), registryPath);
                this.tracer.RelatedWarning(
                    metadata,
                    $"{nameof(this.TryRemoveRepo)}: Exception while removing repo");

                return false;
            }

            errorMessage = null;
            return true;
        }

        public List<ScalarRepoRegistration> GetRegisteredRepos()
        {
            List<ScalarRepoRegistration>  repoList = new List<ScalarRepoRegistration>();
            if (!this.fileSystem.DirectoryExists(this.registryFolderPath))
            {
                return repoList;
            }

            IEnumerable<DirectoryItemInfo> registryDirContents = this.fileSystem.ItemsInDirectory(this.registryFolderPath);
            foreach (DirectoryItemInfo dirItem in registryDirContents)
            {
                if (dirItem.IsDirectory)
                {
                    continue;
                }

                if (!Path.GetExtension(dirItem.Name).Equals(RegistryFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    string repoData = this.fileSystem.ReadAllText(dirItem.FullName);
                    ScalarRepoRegistration registration = ScalarRepoRegistration.FromJson(repoData);
                    repoList.Add(registration);
                }
                catch (Exception e)
                {
                    EventMetadata metadata = CreateEventMetadata(e);
                    metadata.Add(nameof(dirItem.FullName), dirItem.FullName);
                    this.tracer.RelatedWarning(
                        metadata,
                        $"{nameof(this.GetRegisteredRepos)}: Failed to read registry file");
                }
            }

            return repoList;
        }

        public List<ScalarRepoRegistration> GetRegisteredReposForUser(string ownerSID)
        {
            List<ScalarRepoRegistration> registeredRepos = this.GetRegisteredRepos();
            IEnumerable<DirectoryItemInfo> registryDirContents = this.fileSystem.ItemsInDirectory(this.registryFolderPath);
            return registeredRepos.Where(x => x.OwnerSID.Equals(ownerSID, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }

        internal static string GetRepoRootSha(string repoRoot)
        {
            return SHA1Util.SHA1HashStringForUTF8String(repoRoot.ToLowerInvariant());
        }

        internal string GetRepoRegistryTempFilePath(string repoRoot)
        {
            string repoTempFilename = $"{GetRepoRootSha(repoRoot)}{RegistryTempFileExtension}";
            return Path.Combine(this.registryFolderPath, repoTempFilename);
        }

        internal string GetRepoRegistryFilePath(string repoRoot)
        {
            string repoFilename = $"{GetRepoRootSha(repoRoot)}{RegistryFileExtension}";
            return Path.Combine(this.registryFolderPath, repoFilename);
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
    }
}
