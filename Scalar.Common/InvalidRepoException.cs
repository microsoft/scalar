using System;

namespace Scalar.Common
{
    public class InvalidRepoException : Exception
    {
        public InvalidRepoException(string message)
            : base(message)
        {
        }
    }
}
