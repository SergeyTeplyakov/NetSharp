using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.ServerSide
{
    /// <summary>
    /// Представляет абстракцию подключенного клиента.
    /// Содержит обработчики данного клиента и позволяет управлять ими.
    /// </summary>
    public class ConnectedClient
    {
        ConcurrentDictionary<Guid, ConnectingHandler> handlers;
        int handlerCount;
        readonly Host host;
        List<Task> incompleteTasksHandlers;

        internal event EventHandler RemoveLastHandler;

        /// <summary>
        /// Менеджер восстановления связи. Является общим для всех обработчиков данного клиента.
        /// </summary>
        public ReconnectManager ReconnectManager { get; private set; }
        /// <summary>
        /// ID клиента.
        /// </summary>
        public Guid ID { get; private set; }
        /// <summary>
        /// Число обработчиков клиента.
        /// </summary>
        public int HandlerCount => handlerCount;

        public ConnectedClient(Host host)
        {
            handlers = new ConcurrentDictionary<Guid, ConnectingHandler>();
            ID = Guid.NewGuid();
            ReconnectManager = new ReconnectManager();
            incompleteTasksHandlers = new List<Task>();

            this.host = host;
        }

        void OnRemoveLastHandler()
        {
            if (handlerCount == 0)
                Volatile.Read(ref RemoveLastHandler).Invoke(this, EventArgs.Empty);
        }

        internal void AddHandler(ConnectingHandler handler)
        {
            handlers.TryAdd(handler.ID, handler);
            Interlocked.Increment(ref handlerCount);

            host.RaiseHostStateChange(this, ReasonChange.AddHandler, handler);
        }

        internal void StopHandler(Guid handlerID, StopMode stopMode)
        {
            ConnectingHandler handler;

            if (handlers.TryGetValue(handlerID, out handler))
                incompleteTasksHandlers.Add(handler.StopService(stopMode));
            else
                Logger.Write(Source.Server, $"Ошибка остановки обработчика: обработчик с ID {handlerID} не найден.");
        }

        internal void RemoveHandler(Guid handlerID)
        {
            ConnectingHandler handler;

            if (handlers.TryRemove(handlerID, out handler))
            {
                Interlocked.Decrement(ref handlerCount);

                OnRemoveLastHandler();

                host.RaiseHostStateChange(this, ReasonChange.RemoveHandler);
            }
            else
            {
                Logger.Write(Source.Server, $"Ошибка удаления обработчика: обработчик с ID {handlerID} не найден.");
            }
        }

        public async Task CloseAsync()
        {
            ConnectingHandler handler;

            foreach (var key in handlers.Keys)
            {
                handlers.TryRemove(key, out handler);
                incompleteTasksHandlers.Add(handler.StopService(StopMode.Soft));
            }

            handlerCount = 0;
            
            await Task.WhenAll(incompleteTasksHandlers).ConfigureAwait(false);
        }

        public List<ConnectingHandler> GetHandlersByName(string handlerName)
        {
            List<ConnectingHandler> foundHandlers = new List<ConnectingHandler>();
            ConnectingHandler handler;

            foreach (var key in handlers.Keys)
                if (handlers.TryGetValue(key, out handler) && handler.Name == handlerName)
                    foundHandlers.Add(handler);

            return foundHandlers;
        }
    }
}
