using Newtonsoft.Json;
using Scalar.Common.Tracing;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace Scalar.Common.Http
{
    public class ConfigHttpRequestor : HttpRequestor
    {
        private readonly string repoUrl;

        public ConfigHttpRequestor(ITracer tracer, Enlistment enlistment, RetryConfig retryConfig)
            : base(tracer, retryConfig, enlistment)
        {
            this.repoUrl = enlistment.RepoUrl;
        }

        public bool TryQueryScalarConfig(bool logErrors, out ServerScalarConfig serverScalarConfig, out HttpStatusCode? httpStatus, out string errorMessage)
        {
            serverScalarConfig = null;
            httpStatus = null;
            errorMessage = null;

            Uri scalarConfigEndpoint;
            string scalarConfigEndpointString = this.repoUrl + ScalarConstants.Endpoints.ScalarConfig;
            try
            {
                scalarConfigEndpoint = new Uri(scalarConfigEndpointString);
            }
            catch (UriFormatException e)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add("Method", nameof(this.TryQueryScalarConfig));
                metadata.Add("Exception", e.ToString());
                metadata.Add("Url", scalarConfigEndpointString);
                this.Tracer.RelatedError(metadata, "UriFormatException when constructing Uri", Keywords.Network);

                return false;
            }

            long requestId = HttpRequestor.GetNewRequestId();
            RetryWrapper<ServerScalarConfig> retrier = new RetryWrapper<ServerScalarConfig>(this.RetryConfig.MaxAttempts, CancellationToken.None);

            if (logErrors)
            {
                retrier.OnFailure += RetryWrapper<ServerScalarConfig>.StandardErrorHandler(this.Tracer, requestId, "QueryGvfsConfig");
            }

            RetryWrapper<ServerScalarConfig>.InvocationResult output = retrier.Invoke(
                tryCount =>
                {
                    using (GitEndPointResponseData response = this.SendRequest(
                        requestId,
                        scalarConfigEndpoint,
                        HttpMethod.Get,
                        requestContent: null,
                        cancellationToken: CancellationToken.None))
                    {
                        if (response.HasErrors)
                        {
                            return new RetryWrapper<ServerScalarConfig>.CallbackResult(response.Error, response.ShouldRetry);
                        }

                        try
                        {
                            string configString = response.RetryableReadToEnd();
                            ServerScalarConfig config = JsonConvert.DeserializeObject<ServerScalarConfig>(configString);
                            return new RetryWrapper<ServerScalarConfig>.CallbackResult(config);
                        }
                        catch (JsonReaderException e)
                        {
                            return new RetryWrapper<ServerScalarConfig>.CallbackResult(e, shouldRetry: false);
                        }
                    }
                });

            if (output.Succeeded)
            {
                serverScalarConfig = output.Result;
                httpStatus = HttpStatusCode.OK;
                return true;
            }

            GitObjectsHttpException httpException = output.Error as GitObjectsHttpException;
            if (httpException != null)
            {
                httpStatus = httpException.StatusCode;
                errorMessage = httpException.Message;
            }

            if (logErrors)
            {
                this.Tracer.RelatedError(
                    new EventMetadata
                    {
                        { "Exception", output.Error.ToString() }
                    },
                    $"{nameof(this.TryQueryScalarConfig)} failed");
            }

            return false;
        }
    }
}
