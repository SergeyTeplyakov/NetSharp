using System;

namespace NetSharp
{
    [Serializable]
    public class CommunicationException : ApplicationException
    {
        public CommunicationException() { }
        public CommunicationException(string message) : base(message) { }
        public CommunicationException(string message, Exception ex) : base(message, ex) { }
        protected CommunicationException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext contex)
            : base(info, contex) { }
    }

    [Serializable]
    public class AcceptorException : ApplicationException
    {
        public AcceptorException() { }
        public AcceptorException(string message) : base(message) { }
        public AcceptorException(string message, Exception ex) : base(message, ex) { }
        protected AcceptorException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext contex)
            : base(info, contex) { }
    }

    [Serializable]
    public class InitializationException : ApplicationException
    {
        public InitializationException() { }
        public InitializationException(string message) : base(message) { }
        public InitializationException(string message, Exception ex) : base(message, ex) { }
        protected InitializationException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext contex)
            : base(info, contex) { }
    }
}
