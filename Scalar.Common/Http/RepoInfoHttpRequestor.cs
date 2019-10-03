using Newtonsoft.Json;
using Scalar.Common.Tracing;
using System;
using System.Net.Http;
using System.Threading;

namespace Scalar.Common.Http
{
    public class RepoInfoHttpRequestor : HttpRequestor
    {
        // Limit the number of retries because not all servers support providing repo info
        private const int MaxRepoInfoRetries = 3;
        private readonly string repoUrl;

        public RepoInfoHttpRequestor(ITracer tracer, Enlistment enlistment, RetryConfig retryConfig)
            : base(tracer, retryConfig, enlistment)
        {
            this.repoUrl = enlistment.RepoUrl;
        }

        public bool TryQueryRepoInfo(bool logErrors, out RepoInfo repoInfo, out string errorMessage)
        {
            Uri repoInfoEndpoint;
            string repoInfoEndpointString = this.repoUrl + ScalarConstants.Endpoints.RepoInfo;
            try
            {
                repoInfoEndpoint = new Uri(repoInfoEndpointString);
            }
            catch (UriFormatException e)
            {
                repoInfo = null;
                errorMessage = "UriFormatException when constructing Uri";

                EventMetadata metadata = new EventMetadata();
                metadata.Add("Method", nameof(this.TryQueryRepoInfo));
                metadata.Add("Exception", e.ToString());
                metadata.Add("Url", repoInfoEndpointString);
                this.Tracer.RelatedError(metadata, $"{nameof(this.TryQueryRepoInfo)}: {errorMessage}", Keywords.Network);

                return false;
            }

            long requestId = HttpRequestor.GetNewRequestId();
            RetryWrapper<RepoInfo> retrier = new RetryWrapper<RepoInfo>(
                Math.Min(this.RetryConfig.MaxAttempts, MaxRepoInfoRetries),
                CancellationToken.None);

            if (logErrors)
            {
                retrier.OnFailure += RetryWrapper<RepoInfo>.StandardErrorHandler(
                    this.Tracer,
                    requestId,
                    "QueryRepoInfo",
                    forceLogAsWarning: true); // Not all servers support repo info
            }

            RetryWrapper<RepoInfo>.InvocationResult output = retrier.Invoke(
                tryCount =>
                {
                    using (GitEndPointResponseData response = this.SendRequest(
                        requestId,
                        repoInfoEndpoint,
                        HttpMethod.Get,
                        requestContent: null,
                        cancellationToken: CancellationToken.None))
                    {
                        if (response.HasErrors)
                        {
                            return new RetryWrapper<RepoInfo>.CallbackResult(response.Error, response.ShouldRetry);
                        }

                        try
                        {
                            string configString = response.RetryableReadToEnd();
                            RepoInfo config = JsonConvert.DeserializeObject<RepoInfo>(
                                configString,
                                new JsonSerializerSettings
                                {
                                    MissingMemberHandling = MissingMemberHandling.Ignore
                                });
                            return new RetryWrapper<RepoInfo>.CallbackResult(config);
                        }
                        catch (JsonReaderException e)
                        {
                            return new RetryWrapper<RepoInfo>.CallbackResult(e, shouldRetry: false);
                        }
                    }
                });

            if (output.Succeeded)
            {
                repoInfo = output.Result;
                errorMessage = null;
                return true;
            }

            GitObjectsHttpException httpException = output.Error as GitObjectsHttpException;
            if (httpException != null)
            {
                errorMessage = httpException.Message;
            }

            if (logErrors)
            {
                this.Tracer.RelatedWarning(
                    new EventMetadata
                    {
                        { "Exception", output.Error.ToString() }
                    },
                    $"{nameof(this.TryQueryRepoInfo)} failed");
            }

            repoInfo = null;
            errorMessage = null;

            // Any failures other than UriFormatException are OK because
            // not all servers support repo info
            return true;
        }
    }
}
