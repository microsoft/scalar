using NUnit.Framework;
using Scalar.Common;
using Scalar.Common.Git;
using Scalar.Common.Http;
using Scalar.Tests.Should;
using Scalar.UnitTests.Category;
using Scalar.UnitTests.Mock;
using Scalar.UnitTests.Mock.Common;
using Scalar.UnitTests.Mock.FileSystem;
using Scalar.UnitTests.Mock.Git;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

namespace Scalar.UnitTests.Git
{
    [TestFixture]
    public class ScalarGitObjectsTests
    {
        private const string ValidTestObjectFileContents = "421dc4df5e1de427e363b8acd9ddb2d41385dbdf";
        private const string TestEnlistmentRoot = "mock:\\src";
        private const string TestLocalCacheRoot = "mock:\\.scalar";
        private const string TestObjectRoot = "mock:\\.scalar\\gitObjectCache";

        [TestCase]
        public void SucceedsForNormalLookingLooseObjectDownloads()
        {
            MockFileSystemWithCallbacks fileSystem = new Mock.FileSystem.MockFileSystemWithCallbacks();
            fileSystem.OnFileExists = () => true;
            fileSystem.OnOpenFileStream = (path, mode, access) => new MemoryStream();
            MockHttpGitObjects httpObjects = new MockHttpGitObjects();
            using (httpObjects.InputStream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(ValidTestObjectFileContents)))
            {
                httpObjects.MediaType = ScalarConstants.MediaTypes.LooseObjectMediaType;
                ScalarGitObjects dut = this.CreateTestableScalarGitObjects(httpObjects, fileSystem);

                dut.TryDownloadAndSaveObject(ValidTestObjectFileContents, ScalarGitObjects.RequestSource.FileStreamCallback)
                    .ShouldEqual(GitObjects.DownloadAndSaveObjectResult.Success);
            }
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void FailsZeroByteLooseObjectsDownloads()
        {
            this.AssertRetryableExceptionOnDownload(
                new MemoryStream(),
                ScalarConstants.MediaTypes.LooseObjectMediaType,
                gitObjects => gitObjects.TryDownloadAndSaveObject("aabbcc", ScalarGitObjects.RequestSource.FileStreamCallback));
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void FailsNullByteLooseObjectsDownloads()
        {
            this.AssertRetryableExceptionOnDownload(
                new MemoryStream(new byte[256]),
                ScalarConstants.MediaTypes.LooseObjectMediaType,
                gitObjects => gitObjects.TryDownloadAndSaveObject("aabbcc", ScalarGitObjects.RequestSource.FileStreamCallback));
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void FailsZeroBytePackDownloads()
        {
            this.AssertRetryableExceptionOnDownload(
                new MemoryStream(),
                ScalarConstants.MediaTypes.PackFileMediaType,
                gitObjects => gitObjects.TryDownloadCommit("object0"));
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void FailsNullBytePackDownloads()
        {
            this.AssertRetryableExceptionOnDownload(
                new MemoryStream(new byte[256]),
                ScalarConstants.MediaTypes.PackFileMediaType,
                gitObjects => gitObjects.TryDownloadCommit("object0"));
        }

        private void AssertRetryableExceptionOnDownload(
            MemoryStream inputStream,
            string mediaType,
            Action<ScalarGitObjects> download)
        {
            MockHttpGitObjects httpObjects = new MockHttpGitObjects();
            httpObjects.InputStream = inputStream;
            httpObjects.MediaType = mediaType;
            MockFileSystemWithCallbacks fileSystem = new MockFileSystemWithCallbacks();

            using (ReusableMemoryStream downloadDestination = new ReusableMemoryStream(string.Empty))
            {
                fileSystem.OnFileExists = () => false;
                fileSystem.OnOpenFileStream = (path, mode, access) => downloadDestination;

                ScalarGitObjects gitObjects = this.CreateTestableScalarGitObjects(httpObjects, fileSystem);

                Assert.Throws<RetryableException>(() => download(gitObjects));
                inputStream.Dispose();
            }
        }

        private ScalarGitObjects CreateTestableScalarGitObjects(MockHttpGitObjects httpObjects, MockFileSystemWithCallbacks fileSystem)
        {
            MockTracer tracer = new MockTracer();
            ScalarEnlistment enlistment = new ScalarEnlistment(TestEnlistmentRoot, "https://fakeRepoUrl", "fakeGitBinPath", authentication: null);
            enlistment.InitializeCachePathsFromKey(TestLocalCacheRoot, TestObjectRoot);
            GitRepo repo = new GitRepo(tracer, enlistment, fileSystem);

            ScalarContext context = new ScalarContext(tracer, fileSystem, repo, enlistment);
            ScalarGitObjects dut = new ScalarGitObjects(context, httpObjects);
            return dut;
        }

        private string GetDataPath(string fileName)
        {
            string workingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(workingDirectory, "Data", fileName);
        }

        private class MockHttpGitObjects : GitObjectsHttpRequestor
        {
            public MockHttpGitObjects()
                : this(new MockScalarEnlistment())
            {
            }

            private MockHttpGitObjects(MockScalarEnlistment enlistment)
                : base(new MockTracer(), enlistment, new MockCacheServerInfo(), new RetryConfig(maxRetries: 1))
            {
            }

            public Stream InputStream { get; set; }
            public string MediaType { get; set; }

            public static MemoryStream GetRandomStream(int size)
            {
                Random randy = new Random(0);
                MemoryStream stream = new MemoryStream();
                byte[] buffer = new byte[size];

                randy.NextBytes(buffer);
                stream.Write(buffer, 0, buffer.Length);

                stream.Position = 0;
                return stream;
            }

            public override RetryWrapper<GitObjectTaskResult>.InvocationResult TryDownloadLooseObject(
                string objectId,
                bool retryOnFailure,
                CancellationToken cancellationToken,
                string requestSource,
                Func<int, GitEndPointResponseData, RetryWrapper<GitObjectTaskResult>.CallbackResult> onSuccess)
            {
                return this.TryDownloadObjects(new[] { objectId }, onSuccess, null, false);
            }

            public override RetryWrapper<GitObjectTaskResult>.InvocationResult TryDownloadObjects(
                IEnumerable<string> objectIds,
                Func<int, GitEndPointResponseData, RetryWrapper<GitObjectTaskResult>.CallbackResult> onSuccess,
                Action<RetryWrapper<GitObjectTaskResult>.ErrorEventArgs> onFailure,
                bool preferBatchedLooseObjects)
            {
                using (GitEndPointResponseData response = new GitEndPointResponseData(
                    HttpStatusCode.OK,
                    this.MediaType,
                    this.InputStream,
                    message: null,
                    onResponseDisposed: null))
                {
                    onSuccess(0, response);
                }

                GitObjectTaskResult result = new GitObjectTaskResult(true);
                return new RetryWrapper<GitObjectTaskResult>.InvocationResult(0, true, result);
            }

            public override List<GitObjectSize> QueryForFileSizes(IEnumerable<string> objectIds, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
