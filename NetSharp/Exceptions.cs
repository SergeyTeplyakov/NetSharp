using System;

namespace NetSharp
{
    // Культ-карго: нет мысла делать все конструкторы для исключений. Нужно использовать лишь те, что нужны.
    // Например, конструкторы, которые принимают SerializationInfo нужны только если исключения будут пересекать границы домена
    // Не уверен, что это нужно.

    // ApplicationExeption considered harmful. лучше наследоваться от System.Exception. Легкое гугление должно дать больше ссылок.
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

    // Нет комментариев, не ясно, ради чего оно нужно.
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

    // Название исключения не несет никакого смысла.
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
