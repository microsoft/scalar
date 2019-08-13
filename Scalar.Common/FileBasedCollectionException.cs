using System;

namespace Scalar.Common
{
    public class FileBasedCollectionException : Exception
    {
        public FileBasedCollectionException(Exception innerException)
            : base(innerException.Message, innerException)
        {
        }
    }
}
