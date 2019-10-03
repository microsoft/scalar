using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Scalar.Common;
using Scalar.Tests.Should;
using Scalar.UnitTests.Mock.Common;

namespace Scalar.UnitTests.Common
{
    [TestFixture]
    public class LocalCacheResolverTests
    {
        private const string UrlKeyPrefix = "url_";
        private const string RepoIdKeyPrefix = "id_";

        [TestCase]
        public void CanGetLocalCacheKeyFromRepoInfo()
        {
            List<string> repoIds = new List<string>
            {
                "df3216c6-6d33-476e-8d89-e877a6d74c79",
                "testId",
                "826847f5da3ef78114b2a9d5253ada9d95265c76"
            };

            MockTracer tracer = new MockTracer();
            MockScalarEnlistment enlistment = CreateEnlistment("mock://repoUrl");
            LocalCacheResolver localCacheResolver = new LocalCacheResolver(enlistment);

            foreach (string repoId in repoIds)
            {
                RepoInfo repoInfo = new RepoInfo();
                repoInfo.repository = new RepoInfo.Repository();
                repoInfo.repository.id = repoId;

                localCacheResolver.TryGetLocalCacheKeyFromRepoInfoOrURL(
                    tracer,
                    repoInfo,
                    out string localCacheKey,
                    out string errorMessage).ShouldBeTrue();
                errorMessage.ShouldBeEmpty();
                localCacheKey.ShouldEqual($"{RepoIdKeyPrefix}{repoId}");
            }
        }

        [TestCase]
        public void FallBackToUsingURLWhenRepoInfoEmpty()
        {
            MockScalarEnlistment enlistment = CreateEnlistment("mock://repoUrl");
            LocalCacheResolver localCacheResolver = new LocalCacheResolver(enlistment);

            RepoInfo repoInfo = new RepoInfo();
            LocalKeyShouldBeResolvedFromURL(localCacheResolver, repoInfo);

            repoInfo.repository = new RepoInfo.Repository();
            LocalKeyShouldBeResolvedFromURL(localCacheResolver, repoInfo);

            repoInfo.repository.id = string.Empty;
            LocalKeyShouldBeResolvedFromURL(localCacheResolver, repoInfo);

            repoInfo.repository.id = "   ";
            LocalKeyShouldBeResolvedFromURL(localCacheResolver, repoInfo);
        }

        [TestCase]
        public void CanGetLocalCacheKeyFromURL()
        {
            MockScalarEnlistment enlistment = CreateEnlistment("mock://repoUrl");
            LocalCacheResolver localCacheResolver = new LocalCacheResolver(enlistment);
            LocalKeyShouldBeResolvedFromURL(localCacheResolver, repoInfo: null);
        }

        [TestCase]
        public void LocalCacheKeyFromURLIsCaseInsensitive()
        {
            MockTracer tracer = new MockTracer();
            List<MockScalarEnlistment> enlistments = new List<MockScalarEnlistment>
            {
                CreateEnlistment("mock://repourl"),
                CreateEnlistment("mock://repoUrl"),
                CreateEnlistment("MOCK://repoUrl"),
                CreateEnlistment("mock://RepoUrl")
            };

            IEnumerable<LocalCacheResolver> localCacheResolvers = enlistments.Select(x => new LocalCacheResolver(x));

            localCacheResolvers.First().TryGetLocalCacheKeyFromRepoInfoOrURL(
                tracer,
                repoInfo: null,
                localCacheKey: out string localCacheKey,
                errorMessage: out _).ShouldBeTrue();

            foreach (LocalCacheResolver resolver in localCacheResolvers)
            {
                resolver.TryGetLocalCacheKeyFromRepoInfoOrURL(
                    tracer,
                    repoInfo: null,
                    localCacheKey: out string tempCacheKey,
                    errorMessage: out string ErrorMessage).ShouldBeTrue();
                localCacheKey.ShouldEqual(tempCacheKey);
            }
        }

        [TestCase]
        public void LocalCacheKeysFromDifferentURLsAreDifferent()
        {
            MockTracer tracer = new MockTracer();
            List<MockScalarEnlistment> enlistments = new List<MockScalarEnlistment>
            {
                CreateEnlistment("mock://repourl"),
                CreateEnlistment("mock://repourl2"),
                CreateEnlistment("MOCK://repoUrl3"),
                CreateEnlistment("mock://RepoUrl4")
            };

            IEnumerable<LocalCacheResolver> localCacheResolvers = enlistments.Select(x => new LocalCacheResolver(x));

            List<string> localCacheKeys = new List<string>();

            foreach (LocalCacheResolver resolver in localCacheResolvers)
            {
                resolver.TryGetLocalCacheKeyFromRepoInfoOrURL(
                    tracer,
                    repoInfo: null,
                    localCacheKey: out string localCacheKey,
                    errorMessage: out string ErrorMessage).ShouldBeTrue();
                localCacheKeys.Add(localCacheKey);
            }

            for (int i = 0; i < localCacheKeys.Count; ++i)
            {
                for (int j = 0; j < localCacheKeys.Count; j++)
                {
                    if (i != j)
                    {
                        string.Equals(
                            localCacheKeys[i],
                            localCacheKeys[j],
                            System.StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
                    }
                }
            }
        }

        private static MockScalarEnlistment CreateEnlistment(string repoUrl)
        {
            return new MockScalarEnlistment(
                Path.Combine("mock:", "path"),
                repoUrl,
                Path.Combine("mock:", "git"),
                gitProcess: null);
        }

        private static void LocalKeyShouldBeResolvedFromURL(LocalCacheResolver localCacheResolver, RepoInfo repoInfo)
        {
            localCacheResolver.TryGetLocalCacheKeyFromRepoInfoOrURL(
                new MockTracer(),
                repoInfo,
                out string localCacheKey,
                out string errorMessage).ShouldBeTrue();
            errorMessage.ShouldBeEmpty();
            localCacheKey.Substring(0, UrlKeyPrefix.Length).ShouldEqual(UrlKeyPrefix);
            SHA1Util.IsValidShaFormat(localCacheKey.Substring(UrlKeyPrefix.Length)).ShouldBeTrue();
        }
    }
}
