using Scalar.Common.Http;
using Scalar.Common.Tracing;
using System;
using System.IO;

namespace Scalar.Common
{
    public class LocalCacheResolver
    {
        private const string EtwArea = nameof(LocalCacheResolver);
        private ScalarEnlistment enlistment;

        public LocalCacheResolver(ScalarEnlistment enlistment)
        {
            this.enlistment = enlistment;
        }

        public static bool TryGetDefaultLocalCacheRoot(ScalarEnlistment enlistment, out string localCacheRoot, out string localCacheRootError)
        {
            if (ScalarEnlistment.IsUnattended(tracer: null))
            {
                localCacheRoot = Path.Combine(enlistment.EnlistmentRoot, ScalarConstants.DefaultScalarCacheFolderName);
                localCacheRootError = null;
                return true;
            }

            return ScalarPlatform.Instance.TryGetDefaultLocalCacheRoot(enlistment.EnlistmentRoot, out localCacheRoot, out localCacheRootError);
        }

        public bool TryGetLocalCacheKeyFromRepoInfoOrURL(
            ITracer tracer,
            VstsInfoData vstsInfo,
            out string localCacheKey,
            out string errorMessage)
        {
            try
            {
                EventMetadata metadata = CreateEventMetadata();
                metadata.Add($"{nameof(this.enlistment)}.{nameof(this.enlistment.RepoUrl)}", this.enlistment.RepoUrl);

                if (string.IsNullOrWhiteSpace(vstsInfo?.Repository?.Id))
                {
                    // Generate cache key with SHA1 hash of the URL
                    localCacheKey = $"url_{SHA1Util.SHA1HashStringForUTF8String(this.enlistment.RepoUrl.ToLowerInvariant())}";
                    metadata.Add(nameof(localCacheKey), localCacheKey);
                    metadata.Add("repoInfo_is_null", vstsInfo == null ? "true" : "false");
                    metadata.Add("repository_is_null", vstsInfo?.Repository == null ? "true" : "false");
                    metadata.Add("repository_id", vstsInfo?.Repository?.Id);
                    tracer.RelatedEvent(EventLevel.Informational, "LocalCacheResolver_KeyFromURL", metadata);

                    errorMessage = string.Empty;
                    return true;
                }

                metadata.Add($"{nameof(vstsInfo.Repository)}.{nameof(vstsInfo.Repository.Id)}", vstsInfo.Repository.Id);
                metadata.Add($"{nameof(vstsInfo.Repository)}.{nameof(vstsInfo.Repository.Name)}", vstsInfo.Repository.Name);
                metadata.Add($"{nameof(vstsInfo.Repository)}.{nameof(vstsInfo.Repository.RemoteUrl)}", vstsInfo.Repository.RemoteUrl);

                localCacheKey = $"id_{vstsInfo.Repository.Id}";
                metadata.Add(nameof(localCacheKey), localCacheKey);
                tracer.RelatedEvent(EventLevel.Informational, "LocalCacheResolver_KeyFromRepoInfo", metadata);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception e)
            {
                EventMetadata metadata = CreateEventMetadata(e);
                metadata.Add($"{nameof(this.enlistment)}.{nameof(this.enlistment.RepoUrl)}", this.enlistment.RepoUrl);
                tracer.RelatedError(metadata, nameof(this.TryGetLocalCacheKeyFromRepoInfoOrURL) + ": Caught exception");

                errorMessage = string.Format("Exception while getting local cache key: {0}", e.Message);
                localCacheKey = null;
                return false;
            }
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
