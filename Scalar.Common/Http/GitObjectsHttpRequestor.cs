using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
