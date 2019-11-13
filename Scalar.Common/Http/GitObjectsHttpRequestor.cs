using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace Scalar.Common.Http
{
    public class GitObjectsHttpRequestor : HttpRequestor
    {
        private Enlistment enlistment;

        public GitObjectsHttpRequestor(ITracer tracer, Enlistment enlistment, CacheServerInfo cacheServer, RetryConfig retryConfig)
            : base(tracer, retryConfig, enlistment)
        {
            this.enlistment = enlistment;
            this.CacheServer = cacheServer;
        }

        public CacheServerInfo CacheServer { get; private set; }

        public virtual GitRefs QueryInfoRefs(string branch)
        {
            long requestId = HttpRequestor.GetNewRequestId();

            Uri infoRefsEndpoint;
            if (!this.TryCreateRepoEndpointUri(this.enlistment.RepoUrl, ScalarConstants.Endpoints.InfoRefs, out infoRefsEndpoint, out _))
            {
                return null;
            }

            RetryWrapper<GitRefs> retrier = new RetryWrapper<GitRefs>(this.RetryConfig.MaxAttempts, CancellationToken.None);
            retrier.OnFailure += RetryWrapper<GitRefs>.StandardErrorHandler(this.Tracer, requestId, "QueryInfoRefs");

            RetryWrapper<GitRefs>.InvocationResult output = retrier.Invoke(
                tryCount =>
                {
                    using (GitEndPointResponseData response = this.SendRequest(
                        requestId,
                        infoRefsEndpoint,
                        HttpMethod.Get,
                        requestContent: null,
                        cancellationToken: CancellationToken.None))
                    {
                        if (response.HasErrors)
                        {
                            return new RetryWrapper<GitRefs>.CallbackResult(response.Error, response.ShouldRetry);
                        }

                        List<string> infoRefsResponse = response.RetryableReadAllLines();
                        return new RetryWrapper<GitRefs>.CallbackResult(new GitRefs(infoRefsResponse, branch));
                    }
                });

            return output.Result;
        }

        public virtual RetryWrapper<GitObjectTaskResult>.InvocationResult TrySendProtocolRequest(
            long requestId,
            Func<int, GitEndPointResponseData, RetryWrapper<GitObjectTaskResult>.CallbackResult> onSuccess,
            Action<RetryWrapper<GitObjectTaskResult>.ErrorEventArgs> onFailure,
            HttpMethod method,
            Uri endPoint,
            CancellationToken cancellationToken,
            string requestBody = null,
            MediaTypeWithQualityHeaderValue acceptType = null,
            bool retryOnFailure = true)
        {
            return this.TrySendProtocolRequest(
                requestId,
                onSuccess,
                onFailure,
                method,
                endPoint,
                cancellationToken,
                () => requestBody,
                acceptType,
                retryOnFailure);
        }

        public virtual RetryWrapper<GitObjectTaskResult>.InvocationResult TrySendProtocolRequest(
            long requestId,
            Func<int, GitEndPointResponseData, RetryWrapper<GitObjectTaskResult>.CallbackResult> onSuccess,
            Action<RetryWrapper<GitObjectTaskResult>.ErrorEventArgs> onFailure,
            HttpMethod method,
            Uri endPoint,
            CancellationToken cancellationToken,
            Func<string> requestBodyGenerator,
            MediaTypeWithQualityHeaderValue acceptType = null,
            bool retryOnFailure = true)
        {
            return this.TrySendProtocolRequest(
                requestId,
                onSuccess,
                onFailure,
                method,
                () => endPoint,
                requestBodyGenerator,
                cancellationToken,
                acceptType,
                retryOnFailure);
        }

        public virtual RetryWrapper<GitObjectTaskResult>.InvocationResult TrySendProtocolRequest(
            long requestId,
            Func<int, GitEndPointResponseData, RetryWrapper<GitObjectTaskResult>.CallbackResult> onSuccess,
            Action<RetryWrapper<GitObjectTaskResult>.ErrorEventArgs> onFailure,
            HttpMethod method,
            Func<Uri> endPointGenerator,
            Func<string> requestBodyGenerator,
            CancellationToken cancellationToken,
            MediaTypeWithQualityHeaderValue acceptType = null,
            bool retryOnFailure = true)
        {
            RetryWrapper<GitObjectTaskResult> retrier = new RetryWrapper<GitObjectTaskResult>(
                retryOnFailure ? this.RetryConfig.MaxAttempts : 1,
                cancellationToken);
            if (onFailure != null)
            {
                retrier.OnFailure += onFailure;
            }

            return retrier.Invoke(
                tryCount =>
                {
                    using (GitEndPointResponseData response = this.SendRequest(
                        requestId,
                        endPointGenerator(),
                        method,
                        requestBodyGenerator(),
                        cancellationToken,
                        acceptType))
                    {
                        if (response.HasErrors)
                        {
                            return new RetryWrapper<GitObjectTaskResult>.CallbackResult(response.Error, response.ShouldRetry, new GitObjectTaskResult(response.StatusCode));
                        }

                        return onSuccess(tryCount, response);
                    }
                });
        }

        public class GitObjectTaskResult
        {
            public GitObjectTaskResult(bool success)
            {
                this.Success = success;
            }

            public GitObjectTaskResult(HttpStatusCode statusCode)
                : this(statusCode == HttpStatusCode.OK)
            {
                this.HttpStatusCodeResult = statusCode;
            }

            public bool Success { get; }
            public HttpStatusCode HttpStatusCodeResult { get; }
        }
    }
}
