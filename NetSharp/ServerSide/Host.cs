using System;
using System.Net;
using System.Threading;
using NetSharp.Communications;

namespace NetSharp.ServerSide
{
    /// <summary>
    /// Причина изменения состояния сервера.
    /// </summary>
    public enum ReasonChange
    {
        AddAcceptor, RemoveAcceptor,
        HostOpen, HostClose,
        AddClient, RemoveClient,
        AddHandler, RemoveHandler
    }

    /// <summary>
    /// Аргументы для события изменения состояния сервера.
    /// </summary>
    public class HostStateChangeArgs : EventArgs
    {
        /// <summary>
        /// Причина изменения.
        /// </summary>
        public ReasonChange Reason { get; private set; }
        /// <summary>
        /// Связанный с причиной изменения состояния сервера объект.
        /// </summary>
        public object Value { get; private set; }

        public HostStateChangeArgs(ReasonChange reason, object value)
        {
            Reason = reason;
            Value = value;
        }
    }

    public class Host
    {
        const int BACKLOG = 10;
        
        internal readonly AcceptorManager AcceptorManager;
        internal readonly ConnectedClientManager ClientManager;

        public event EventHandler<HostStateChangeArgs> HostStateChange;
        public IPAddress ListenAddress { get; private set; }
        public int ConnectedClientCount => ClientManager.ClientCount;

        public Host(IPAddress listenAddress)
        {
            if (listenAddress == null)
                throw new ArgumentNullException(nameof(listenAddress));
                
            ListenAddress = listenAddress;
            AcceptorManager = new AcceptorManager(this);
            ClientManager = new ConnectedClientManager(this);
        }

        internal void RaiseHostStateChange(object source, ReasonChange reason, object value = null)
        {
            Volatile.Read(ref HostStateChange)?.Invoke(source, new HostStateChangeArgs(reason, value));
        }

        public void AddAcceptor(int port, Func<Connection, ConnectedClient, ConnectingHandler> handlerFactory)
        {
            if (handlerFactory == null)
                throw new ArgumentNullException(nameof(handlerFactory));

            AcceptorManager.Add(new Acceptor(ListenAddress, port, BACKLOG, handlerFactory, this));
        }

        public void RemoveAcceptor(int port)
        {
            AcceptorManager.RemoveAcceptor(new IPEndPoint(ListenAddress, port));
        }

        public void Open()
        {
            foreach (Acceptor acceptor in AcceptorManager)
                acceptor.Open();

            RaiseHostStateChange(this, ReasonChange.HostOpen);
        }

        public ConnectedClient GetConnectedClientByID(Guid clientID)
        {
            return ClientManager.GetClientByID(clientID);
        }

        public void Close()
        {
            AcceptorManager.CloseAll();

            ClientManager.RemoveAll();

            RaiseHostStateChange(this, ReasonChange.HostClose);
        }
    }
}
