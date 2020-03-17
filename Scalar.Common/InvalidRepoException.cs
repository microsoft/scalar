using System;

namespace Scalar.Common
{
    public class InvalidRepoException : Exception
    {
        public string RepoPath { get; }

        public InvalidRepoException(string repoPath, string message)
            : base($"path: '{repoPath}', message: '{message}'")
        {
            this.RepoPath = repoPath;
        }
    }
}
