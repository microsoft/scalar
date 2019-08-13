using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.IO;

namespace Scalar.UnitTests.Mock.Git
{
    public class MockLibGit2Repo : LibGit2Repo
    {
        public MockLibGit2Repo(ITracer tracer)
            : base()
        {
        }

        public override bool CommitAndRootTreeExists(string commitish)
        {
            return false;
        }

        public override bool ObjectExists(string sha)
        {
            return false;
        }
    }
}
