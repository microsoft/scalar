using System;

namespace Scalar.Common
{
    public class InvalidRepoException : Exception
    {
        public string RepoPath { get; }

        public InvalidRepoException(string repoPath, string message)
            : base(message)
        {
            this.RepoPath = repoPath;
        }
    }
}
