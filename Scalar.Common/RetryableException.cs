using System;

namespace Scalar.Common
{
    public class RetryableException : Exception
    {
        public RetryableException(string message, Exception inner) : base(message, inner)
        {
        }

        public RetryableException(string message) : base(message)
        {
        }
    }
}
