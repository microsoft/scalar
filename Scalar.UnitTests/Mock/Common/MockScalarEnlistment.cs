using Scalar.Common;
using Scalar.Common.Git;
using Scalar.UnitTests.Mock.Git;
using System.IO;

namespace Scalar.UnitTests.Mock.Common
{
    public class MockScalarEnlistment : ScalarEnlistment
    {
        private MockGitProcess gitProcess;

        public MockScalarEnlistment()
            : base(Path.Combine("mock:", "path"), Path.Combine("mock:", "path"), "mock://repoUrl", Path.Combine("mock:", "git"), authentication: null)
        {
            this.GitObjectsRoot = Path.Combine("mock:", "path", ".git", "objects");
            this.LocalObjectsRoot = this.GitObjectsRoot;
            this.GitPackRoot = Path.Combine("mock:", "path", ".git", "objects", "pack");
        }

        public MockScalarEnlistment(string enlistmentRoot, string repoUrl, string gitBinPath, MockGitProcess gitProcess)
            : base(enlistmentRoot, enlistmentRoot, repoUrl, gitBinPath, authentication: null)
        {
            this.gitProcess = gitProcess;
        }

        public MockScalarEnlistment(MockGitProcess gitProcess)
            : this()
        {
            this.gitProcess = gitProcess;
        }

        public override string GitObjectsRoot { get; protected set; }

        public override string LocalObjectsRoot { get; protected set; }

        public override string GitPackRoot { get; protected set; }

        public override GitProcess CreateGitProcess()
        {
            return this.gitProcess ?? new MockGitProcess();
        }
    }
}
