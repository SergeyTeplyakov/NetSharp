using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace NetSharp.ServerSide
{
    /// <summary>
    /// Является хранилищем ассепторов и позволяет управлять ими.
    /// </summary>
    public class AcceptorManager
    {
        readonly Host host;
        readonly ConcurrentDictionary<IPEndPoint, Acceptor> acceptors;
        int count;

        public int Count => count;
        
        public AcceptorManager(Host host)
        {
            acceptors = new ConcurrentDictionary<IPEndPoint, Acceptor>();
            this.host = host;
        }

        internal void Add(Acceptor acceptor)
        {
            if (acceptor == null)
                throw new ArgumentNullException(nameof(acceptor));

            if (!acceptors.TryAdd(acceptor.LocalIPEndPoint, acceptor))
                throw new ArgumentException($"Ошибка добавления нового ассептора: ассептор для конечной точки {acceptor.LocalIPEndPoint} уже был добавлен ранее.");

            Interlocked.Increment(ref count);

            host.RaiseHostStateChange(this, ReasonChange.AddAcceptor, acceptor);
        }

        public IEnumerator<Acceptor> GetEnumerator()
        {
            foreach(var pair in acceptors)
                yield return pair.Value;
        }

        internal Acceptor GetAcceptor(IPEndPoint ipEndPoint)
        {
            Acceptor acceptor;

            if (acceptors.TryGetValue(ipEndPoint, out acceptor))
                return acceptor;
            else
                throw new ArgumentException($"Ошибка получения ассептора: ассептор для конечной точки {ipEndPoint} не найден.");
        }

        internal void RemoveAcceptor(IPEndPoint ipEndPoint)
        {
            Acceptor acceptor;

            if (acceptors.TryRemove(ipEndPoint, out acceptor))
                acceptor.Close();
            else
                throw new ArgumentException($"Ошибка удаления ассептора: ассептор для конечной точки {ipEndPoint} не найден.");

            Interlocked.Decrement(ref count);

            host.RaiseHostStateChange(this, ReasonChange.RemoveAcceptor);
        }

        internal void CloseAll()
        {
            Acceptor acceptor;

            foreach (var value in acceptors)
                if (acceptors.TryRemove(value.Key, out acceptor) && acceptor != null)
                    acceptor.Close();

            count = 0;
        }
    }
}