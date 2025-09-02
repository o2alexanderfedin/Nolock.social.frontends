using System;

namespace NoLock.Social.Core.Storage
{
    /// <summary>
    /// Exception thrown when storage verification fails.
    /// </summary>
    public class StorageVerificationException : Exception
    {
        public StorageVerificationException()
            : base()
        {
        }

        public StorageVerificationException(string message)
            : base(message)
        {
        }

        public StorageVerificationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}