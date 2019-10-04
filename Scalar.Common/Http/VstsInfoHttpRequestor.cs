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
            string repoInfoEndpointString = this.repoUrl + ScalarConstants.Endpoints.RepoInfo;
            try
            {
                repoInfoEndpoint = new Uri(repoInfoEndpointString);
            }
            catch (UriFormatException e)
            {
                vstsInfo = null;
                errorMessage = "UriFormatException when constructing Uri";

                EventMetadata metadata = new EventMetadata();
                metadata.Add("Method", nameof(this.TryQueryRepoInfo));
                metadata.Add("Exception", e.ToString());
                metadata.Add("Url", repoInfoEndpointString);
                this.Tracer.RelatedError(metadata, $"{nameof(this.TryQueryRepoInfo)}: {errorMessage}", Keywords.Network);

                return false;
            }

            long requestId = HttpRequestor.GetNewRequestId();
            RetryWrapper<VstsInfoData> retrier = new RetryWrapper<VstsInfoData>(
                this.RetryConfig.MaxAttempts,
                CancellationToken.None);

            if (logErrors)
            {
                retrier.OnFailure += RetryWrapper<VstsInfoData>.StandardErrorHandler(
                    this.Tracer,
                    requestId,
                    "QueryRepoInfo",
                    forceLogAsWarning: true); // Not all servers support /vsts/info
            }

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

            HttpStatusCode? httpStatusCode = null;
            GitObjectsHttpException httpException = output.Error as GitObjectsHttpException;
            if (httpException != null)
            {
                errorMessage = httpException.Message;
                httpStatusCode = httpException.StatusCode;
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

            vstsInfo = null;

            if (httpStatusCode == HttpStatusCode.NotFound ||
                (httpStatusCode == HttpStatusCode.Unauthorized && this.IsAnonymous))
            {
                errorMessage = null;

                // These failures are OK because not all servers support /vsts/info
                return true;
            }

            errorMessage = output.Error.Message;
            return false;
        }
    }
}
