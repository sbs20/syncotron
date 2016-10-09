using System;

namespace Sbs20.Syncotron
{
    public class SyncotronException : Exception
    {
        public SyncotronException() : base() { }
        public SyncotronException(string message) : base(message) { }
    }
}
