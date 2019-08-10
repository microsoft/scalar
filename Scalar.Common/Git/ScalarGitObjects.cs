using Scalar.Common.Http;
using Scalar.Common.Tracing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

namespace Scalar.Common.Git
{
    public class ScalarGitObjects : GitObjects
    {
        private static readonly TimeSpan NegativeCacheTTL = TimeSpan.FromSeconds(30);

        private ConcurrentDictionary<string, DateTime> objectNegativeCache;

        public ScalarGitObjects(ScalarContext context, GitObjectsHttpRequestor objectRequestor)
            : base(context.Tracer, context.Enlistment, objectRequestor, context.FileSystem)
        {
            this.Context = context;
            this.objectNegativeCache = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        }

        public enum RequestSource
        {
            Invalid = 0,
            FileStreamCallback,
            ScalarVerb,
            NamedPipeMessage,
            SymLinkCreation,
        }

        protected ScalarContext Context { get; private set; }

        public DownloadAndSaveObjectResult TryDownloadAndSaveObject(string objectId, RequestSource requestSource)
        {
            return this.TryDownloadAndSaveObject(objectId, CancellationToken.None, requestSource, retryOnFailure: true);
        }

        public bool TryGetBlobSizeLocally(string sha, out long length)
        {
            return this.Context.Repository.TryGetBlobLength(sha, out length);
        }

        public List<GitObjectsHttpRequestor.GitObjectSize> GetFileSizes(IEnumerable<string> objectIds, CancellationToken cancellationToken)
        {
            return this.GitObjectRequestor.QueryForFileSizes(objectIds, cancellationToken);
        }

        private DownloadAndSaveObjectResult TryDownloadAndSaveObject(
            string objectId,
            CancellationToken cancellationToken,
            RequestSource requestSource,
            bool retryOnFailure)
        {
            if (objectId == ScalarConstants.AllZeroSha)
            {
                return DownloadAndSaveObjectResult.Error;
            }

            DateTime negativeCacheRequestTime;
            if (this.objectNegativeCache.TryGetValue(objectId, out negativeCacheRequestTime))
            {
                if (negativeCacheRequestTime > DateTime.Now.Subtract(NegativeCacheTTL))
                {
                    return DownloadAndSaveObjectResult.ObjectNotOnServer;
                }

                this.objectNegativeCache.TryRemove(objectId, out negativeCacheRequestTime);
            }

            // To reduce allocations, reuse the same buffer when writing objects in this batch
            byte[] bufToCopyWith = new byte[StreamUtil.DefaultCopyBufferSize];

            RetryWrapper<GitObjectsHttpRequestor.GitObjectTaskResult>.InvocationResult output = this.GitObjectRequestor.TryDownloadLooseObject(
                objectId,
                retryOnFailure,
                cancellationToken,
                requestSource.ToString(),
                onSuccess: (tryCount, response) =>
                {
                    // If the request is from git.exe (i.e. NamedPipeMessage) then we should assume that if there is an
                    // object on disk it's corrupt somehow (which is why git is asking for it)
                    this.WriteLooseObject(
                        response.Stream,
                        objectId,
                        overwriteExistingObject: requestSource == RequestSource.NamedPipeMessage,
                        bufToCopyWith: bufToCopyWith);

                    return new RetryWrapper<GitObjectsHttpRequestor.GitObjectTaskResult>.CallbackResult(new GitObjectsHttpRequestor.GitObjectTaskResult(true));
                });

            if (output.Result != null)
            {
                if (output.Succeeded && output.Result.Success)
                {
                    return DownloadAndSaveObjectResult.Success;
                }

                if (output.Result.HttpStatusCodeResult == HttpStatusCode.NotFound)
                {
                    this.objectNegativeCache.AddOrUpdate(objectId, DateTime.Now, (unused1, unused2) => DateTime.Now);
                    return DownloadAndSaveObjectResult.ObjectNotOnServer;
                }
            }

            return DownloadAndSaveObjectResult.Error;
        }
    }
}
