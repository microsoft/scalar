using System;
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
                VstsInfoData vstsInfo = new VstsInfoData();
                vstsInfo.Repository = new VstsInfoData.RepositoryDetails();
                vstsInfo.Repository.Id = repoId;

                localCacheResolver.TryGetLocalCacheKeyFromRepoInfoOrURL(
                    tracer,
                    vstsInfo,
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

            VstsInfoData vstsInfo = new VstsInfoData();
            LocalKeyShouldBeResolvedFromURL(localCacheResolver, vstsInfo);

            vstsInfo.Repository = new VstsInfoData.RepositoryDetails();
            LocalKeyShouldBeResolvedFromURL(localCacheResolver, vstsInfo);

            vstsInfo.Repository.Id = string.Empty;
            LocalKeyShouldBeResolvedFromURL(localCacheResolver, vstsInfo);

            vstsInfo.Repository.Id = "   ";
            LocalKeyShouldBeResolvedFromURL(localCacheResolver, vstsInfo);
        }

        [TestCase]
        public void CanGetLocalCacheKeyFromURL()
        {
            MockScalarEnlistment enlistment = CreateEnlistment("mock://repoUrl");
            LocalCacheResolver localCacheResolver = new LocalCacheResolver(enlistment);
            LocalKeyShouldBeResolvedFromURL(localCacheResolver, vstsInfo: null);
        }

        [TestCase]
        public void LocalCacheKeyFromURLIsCaseInsensitiveAndStable()
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
            foreach (LocalCacheResolver resolver in localCacheResolvers)
            {
                resolver.TryGetLocalCacheKeyFromRepoInfoOrURL(
                    tracer,
                    vstsInfo: null,
                    localCacheKey: out string localCacheKey,
                    errorMessage: out string ErrorMessage).ShouldBeTrue();

                // Use an explicit result to ensure the hash function is stable
                localCacheKey.ShouldEqual("url_0d95e2600bac6918e2073de5278eed6a6a06f79f");
            }
        }

        [TestCase]
        public void LocalCacheKeysFromDifferentURLsAreDifferent()
        {
            List<MockScalarEnlistment> enlistments = new List<MockScalarEnlistment>
            {
                CreateEnlistment("mock://repourl"),
                CreateEnlistment("mock://repourl2"),
                CreateEnlistment("MOCK://repoUrl3"),
                CreateEnlistment("mock://RepoUrl4")
            };

            IEnumerable<LocalCacheResolver> localCacheResolvers = enlistments.Select(x => new LocalCacheResolver(x));

            HashSet<string> localCacheKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (LocalCacheResolver resolver in localCacheResolvers)
            {
                LocalKeyShouldBeResolvedFromURL(resolver, vstsInfo: null, localCacheKey: out string localCacheKey);
                localCacheKeys.Add(localCacheKey).ShouldBeTrue("Different URLs should have unique cache keys");
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

        private static void LocalKeyShouldBeResolvedFromURL(LocalCacheResolver localCacheResolver, VstsInfoData vstsInfo)
        {
            LocalKeyShouldBeResolvedFromURL(localCacheResolver, vstsInfo, out _);
        }

        private static void LocalKeyShouldBeResolvedFromURL(LocalCacheResolver localCacheResolver, VstsInfoData vstsInfo, out string localCacheKey)
        {
            localCacheResolver.TryGetLocalCacheKeyFromRepoInfoOrURL(
                new MockTracer(),
                vstsInfo,
                out localCacheKey,
                out string errorMessage).ShouldBeTrue();
            errorMessage.ShouldBeEmpty();
            localCacheKey.Substring(0, UrlKeyPrefix.Length).ShouldEqual(UrlKeyPrefix);
            SHA1Util.IsValidShaFormat(localCacheKey.Substring(UrlKeyPrefix.Length)).ShouldBeTrue();
        }
    }
}
