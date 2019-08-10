using Scalar.Common;
using Scalar.Common.Git;
using Scalar.Common.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Scalar.UnitTests.Mock.Git
{
    public class MockScalarGitObjects : ScalarGitObjects
    {
        private ScalarContext context;

        public MockScalarGitObjects(ScalarContext context, GitObjectsHttpRequestor httpGitObjects)
            : base(context, httpGitObjects)
        {
            this.context = context;
        }

        public uint FileLength { get; set; }

        public override bool TryDownloadCommit(string objectSha)
        {
            RetryWrapper<GitObjectsHttpRequestor.GitObjectTaskResult>.InvocationResult result = this.GitObjectRequestor.TryDownloadObjects(
                new[] { objectSha },
                onSuccess: (tryCount, response) =>
                {
                    // Add the contents to the mock repo
                    ((MockGitRepo)this.Context.Repository).AddBlob(objectSha, "DownloadedFile", response.RetryableReadToEnd());

                    return new RetryWrapper<GitObjectsHttpRequestor.GitObjectTaskResult>.CallbackResult(new GitObjectsHttpRequestor.GitObjectTaskResult(true));
                },
                onFailure: null,
                preferBatchedLooseObjects: false);

            return result.Succeeded && result.Result.Success;
        }

        public override string[] ReadPackFileNames(string packFolderPath, string prefixFilter = "")
        {
            return Array.Empty<string>();
        }

        public override GitProcess.Result IndexPackFile(string packfilePath, GitProcess process)
        {
            return new GitProcess.Result("mocked", null, 0);
        }

        public override void DeleteStaleTempPrefetchPackAndIdxs()
        {
        }

        public override bool TryDownloadPrefetchPacks(GitProcess gitProcess, long latestTimestamp, out List<string> packIndexes)
        {
            packIndexes = new List<string>();
            return true;
        }
    }
}
