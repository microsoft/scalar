using Newtonsoft.Json;
using Scalar.Common.Tracing;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace Scalar.Common.Http
{
    public class VstsInfoHttpRequestor : HttpRequestor
    {
        private readonly string repoUrl;

        public VstsInfoHttpRequestor(ITracer tracer, Enlistment enlistment, RetryConfig retryConfig)
            : base(tracer, retryConfig, enlistment)
        {
            this.repoUrl = enlistment.RepoUrl;
        }

        public bool TryQueryRepoInfo(bool logErrors, out VstsInfoData vstsInfo, out string errorMessage)
        {
            Uri repoInfoEndpoint;
            if (!this.TryCreateRepoEndpointUri(this.repoUrl, ScalarConstants.Endpoints.RepoInfo, out repoInfoEndpoint, out errorMessage))
            {
                vstsInfo = null;
                return false;
            }

            long requestId = HttpRequestor.GetNewRequestId();
            RetryWrapper<VstsInfoData> retrier = new RetryWrapper<VstsInfoData>(this.RetryConfig.MaxAttempts, CancellationToken.None);
            retrier.OnFailure += RetryWrapper<VstsInfoData>.StandardErrorHandler(
                this.Tracer,
                requestId,
                "QueryVstsInfo",
                forceLogAsWarning: true); // Not all servers support /vsts/info

            RetryWrapper<VstsInfoData>.InvocationResult output = retrier.Invoke(
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
                            return new RetryWrapper<VstsInfoData>.CallbackResult(response.Error, response.ShouldRetry);
                        }

                        try
                        {
                            string configString = response.RetryableReadToEnd();
                            VstsInfoData vstsInfoData = JsonConvert.DeserializeObject<VstsInfoData>(
                                configString,
                                new JsonSerializerSettings
                                {
                                    MissingMemberHandling = MissingMemberHandling.Ignore
                                });
                            return new RetryWrapper<VstsInfoData>.CallbackResult(vstsInfoData);
                        }
                        catch (JsonReaderException e)
                        {
                            return new RetryWrapper<VstsInfoData>.CallbackResult(e, shouldRetry: false);
                        }
                    }
                });

            if (output.Succeeded)
            {
                vstsInfo = output.Result;
                errorMessage = null;
                return true;
            }

            GitObjectsHttpException httpException = output.Error as GitObjectsHttpException;
            HttpStatusCode? httpStatusCode = httpException?.StatusCode;

            vstsInfo = null;

            EventMetadata metadata = new EventMetadata();
            metadata.Add(nameof(httpStatusCode), httpStatusCode.ToString());
            metadata.Add(nameof(this.IsAnonymous), this.IsAnonymous);

            if (httpStatusCode == HttpStatusCode.NotFound ||
                (httpStatusCode == HttpStatusCode.Unauthorized && this.IsAnonymous))
            {
                errorMessage = null;
                this.Tracer.RelatedEvent(
                    EventLevel.Informational,
                    $"{nameof(this.TryQueryRepoInfo)}_NoVstsInfo",
                    metadata);

                // These failures are OK because not all servers support /vsts/info
                return true;
            }

            metadata.Add("Exception", output.Error.ToString());
            this.Tracer.RelatedError(metadata, $"{nameof(this.TryQueryRepoInfo)} failed");

            errorMessage = output.Error.Message;
            return false;
        }
    }
}
