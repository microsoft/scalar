using Scalar.Common.FileSystem;
using Scalar.Common.Http;
using Scalar.Common.Tracing;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace Scalar.Common.Git
{
    public class GitObjects
    {
        protected readonly ITracer Tracer;
        protected readonly GitObjectsHttpRequestor GitObjectRequestor;
        protected readonly Enlistment Enlistment;

        private const string EtwArea = nameof(GitObjects);
        private const string TempPackFolder = "tempPacks";
        private const string TempIdxExtension = ".tempidx";

        private readonly PhysicalFileSystem fileSystem;

        public GitObjects(ITracer tracer, Enlistment enlistment, GitObjectsHttpRequestor objectRequestor, PhysicalFileSystem fileSystem = null)
        {
            this.Tracer = tracer;
            this.Enlistment = enlistment;
            this.GitObjectRequestor = objectRequestor;
            this.fileSystem = fileSystem ?? new PhysicalFileSystem();
        }

        public enum DownloadAndSaveObjectResult
        {
            Success,
            ObjectNotOnServer,
            Error
        }

        public static bool IsLooseObjectsDirectory(string value)
        {
            return value.Length == 2 && value.All(c => Uri.IsHexDigit(c));
        }

        public virtual void DeleteStaleTempPrefetchPackAndIdxs()
        {
            string[] staleTempPacks = this.ReadPackFileNames(Path.Combine(this.Enlistment.GitPackRoot, GitObjects.TempPackFolder), ScalarConstants.PrefetchPackPrefix);
            foreach (string stalePackPath in staleTempPacks)
            {
                string staleIdxPath = Path.ChangeExtension(stalePackPath, ".idx");
                string staleTempIdxPath = Path.ChangeExtension(stalePackPath, TempIdxExtension);

                EventMetadata metadata = CreateEventMetadata();
                metadata.Add("stalePackPath", stalePackPath);
                metadata.Add("staleIdxPath", staleIdxPath);
                metadata.Add("staleTempIdxPath", staleTempIdxPath);
                metadata.Add(TracingConstants.MessageKey.InfoMessage, "Deleting stale temp pack and/or idx file");

                this.fileSystem.TryDeleteFile(staleTempIdxPath, metadataKey: nameof(staleTempIdxPath), metadata: metadata);
                this.fileSystem.TryDeleteFile(staleIdxPath, metadataKey: nameof(staleIdxPath), metadata: metadata);
                this.fileSystem.TryDeleteFile(stalePackPath, metadataKey: nameof(stalePackPath), metadata: metadata);

                this.Tracer.RelatedEvent(EventLevel.Informational, nameof(this.DeleteStaleTempPrefetchPackAndIdxs), metadata);
            }
        }

        public virtual void DeleteTemporaryFiles()
        {
            string[] temporaryFiles = this.fileSystem.GetFiles(this.Enlistment.GitPackRoot, "tmp_*");
            foreach (string temporaryFilePath in temporaryFiles)
            {
                EventMetadata metadata = CreateEventMetadata();
                metadata.Add(nameof(temporaryFilePath), temporaryFilePath);
                metadata.Add(TracingConstants.MessageKey.InfoMessage, "Deleting temporary file");

                this.fileSystem.TryDeleteFile(temporaryFilePath, metadataKey: nameof(temporaryFilePath), metadata: metadata);

                this.Tracer.RelatedEvent(EventLevel.Informational, nameof(this.DeleteTemporaryFiles), metadata);
            }
        }

        public virtual GitProcess.Result IndexPackFile(string packfilePath, GitProcess gitProcess)
        {
            string tempIdxPath = Path.ChangeExtension(packfilePath, TempIdxExtension);
            string idxPath = Path.ChangeExtension(packfilePath, ".idx");

            Exception indexPackException = null;
            try
            {
                if (gitProcess == null)
                {
                    gitProcess = new GitProcess(this.Enlistment);
                }

                GitProcess.Result result = gitProcess.IndexPack(packfilePath, tempIdxPath);
                if (result.ExitCodeIsFailure)
                {
                    Exception exception;
                    if (!this.fileSystem.TryDeleteFile(tempIdxPath, exception: out exception))
                    {
                        EventMetadata metadata = CreateEventMetadata(exception);
                        metadata.Add("tempIdxPath", tempIdxPath);
                        this.Tracer.RelatedWarning(metadata, $"{nameof(this.IndexPackFile)}: Failed to cleanup temp idx file after index pack failure");
                    }
                }
                else
                {
                    if (this.Enlistment.FlushFileBuffersForPacks)
                    {
                        Exception exception;
                        string error;
                        if (!this.TryFlushFileBuffers(tempIdxPath, out exception, out error))
                        {
                            EventMetadata metadata = CreateEventMetadata(exception);
                            metadata.Add("packfilePath", packfilePath);
                            metadata.Add("tempIndexPath", tempIdxPath);
                            metadata.Add("error", error);
                            this.Tracer.RelatedWarning(metadata, $"{nameof(this.IndexPackFile)}: Failed to flush temp idx file buffers");
                        }
                    }

                    this.fileSystem.MoveAndOverwriteFile(tempIdxPath, idxPath);
                }

                return result;
            }
            catch (Win32Exception e)
            {
                indexPackException = e;
            }
            catch (IOException e)
            {
                indexPackException = e;
            }
            catch (UnauthorizedAccessException e)
            {
                indexPackException = e;
            }

            EventMetadata failureMetadata = CreateEventMetadata(indexPackException);
            failureMetadata.Add("packfilePath", packfilePath);
            failureMetadata.Add("tempIdxPath", tempIdxPath);
            failureMetadata.Add("idxPath", idxPath);

            this.fileSystem.TryDeleteFile(tempIdxPath, metadataKey: nameof(tempIdxPath), metadata: failureMetadata);
            this.fileSystem.TryDeleteFile(idxPath, metadataKey: nameof(idxPath), metadata: failureMetadata);

            this.Tracer.RelatedWarning(failureMetadata, $"{nameof(this.IndexPackFile): Exception caught while trying to index pack file}");

            return new GitProcess.Result(
                string.Empty,
                indexPackException != null ? indexPackException.Message : "Failed to index pack file",
                GitProcess.Result.GenericFailureCode);
        }

        public virtual string[] ReadPackFileNames(string packFolderPath, string prefixFilter = "")
        {
            if (this.fileSystem.DirectoryExists(packFolderPath))
            {
                try
                {
                    return this.fileSystem.GetFiles(packFolderPath, prefixFilter + "*.pack");
                }
                catch (DirectoryNotFoundException e)
                {
                    EventMetadata metadata = CreateEventMetadata(e);
                    metadata.Add("packFolderPath", packFolderPath);
                    metadata.Add("prefixFilter", prefixFilter);
                    metadata.Add(TracingConstants.MessageKey.InfoMessage, "${nameof(this.ReadPackFileNames)}: Caught DirectoryNotFoundException exception");
                    this.Tracer.RelatedEvent(EventLevel.Informational, $"{nameof(this.ReadPackFileNames)}_DirectoryNotFound", metadata);

                    return new string[0];
                }
            }

            return new string[0];
        }

        public virtual bool IsUsingCacheServer()
        {
            return !this.GitObjectRequestor.CacheServer.IsNone(this.Enlistment.RepoUrl);
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

        private bool TryFlushFileBuffers(string path, out Exception exception, out string error)
        {
            error = null;

            FileAttributes originalAttributes;
            if (!this.TryGetAttributes(path, out originalAttributes, out exception))
            {
                error = "Failed to get original attributes, skipping flush";
                return false;
            }

            bool readOnly = (originalAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;

            if (readOnly)
            {
                if (!this.TrySetAttributes(path, originalAttributes & ~FileAttributes.ReadOnly, out exception))
                {
                    error = "Failed to clear read-only attribute, skipping flush";
                    return false;
                }
            }

            bool flushedBuffers = false;
            try
            {
                ScalarPlatform.Instance.FileSystem.FlushFileBuffers(path);
                flushedBuffers = true;
            }
            catch (Win32Exception e)
            {
                exception = e;
                error = "Win32Exception while trying to flush file buffers";
            }

            if (readOnly)
            {
                Exception setAttributesException;
                if (!this.TrySetAttributes(path, originalAttributes, out setAttributesException))
                {
                    EventMetadata metadata = CreateEventMetadata(setAttributesException);
                    metadata.Add("path", path);
                    this.Tracer.RelatedWarning(metadata, $"{nameof(this.TryFlushFileBuffers)}: Failed to re-enable read-only bit");
                }
            }

            return flushedBuffers;
        }

        private bool TryGetAttributes(string path, out FileAttributes attributes, out Exception exception)
        {
            attributes = 0;
            exception = null;
            try
            {
                attributes = this.fileSystem.GetAttributes(path);
                return true;
            }
            catch (IOException e)
            {
                exception = e;
            }
            catch (UnauthorizedAccessException e)
            {
                exception = e;
            }

            return false;
        }

        private bool TrySetAttributes(string path, FileAttributes attributes, out Exception exception)
        {
            exception = null;

            try
            {
                this.fileSystem.SetAttributes(path, attributes);
                return true;
            }
            catch (IOException e)
            {
                exception = e;
            }
            catch (UnauthorizedAccessException e)
            {
                exception = e;
            }

            return false;
        }
    }
}
