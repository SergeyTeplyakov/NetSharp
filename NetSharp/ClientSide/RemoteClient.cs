using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Communications;
using NetSharp.Communications.Callbacks;
using NetSharp.ServerSide;

namespace NetSharp.ClientSide
{
    /// <summary>
    /// Является базовым классом для клиентов взаимодействующих с удалённым сервером согласно
    /// протоколу библиотеки NetSharp. Позволяет подключаться к командному и другим обработчикам
    /// и хранит созданные подключения в пуле. В классах - наследниках следует размещать
    /// прикладную логику взаимодействия с сервером.
    /// </summary>
    public class RemoteClient : IDisposable
    {
        ConnectionPool pool;
        CancellationTokenSource cSource;

        protected Connection mainConnection { get; private set; }
        protected ClientListener clientListener { get; private set; }

        public Guid ID { get; private set; }
        public IPAddress RemoteIPAddress { get; private set; }

        protected RemoteClient(IPAddress remoteIPAddress)
        {
            if (remoteIPAddress == null)
                throw new ArgumentNullException(nameof(remoteIPAddress));

            cSource = new CancellationTokenSource();
            pool = new ConnectionPool(this);

            RemoteIPAddress = remoteIPAddress;
        }

        /// <summary>
        /// Уведомляет серверный обработчик о завершении работы и закрывает подключение.
        /// </summary>
        protected async Task DisconnectFromHandler(Connection connection, StopMode stopMode)
        {
            bool useExclusiveAccess = false;
            Connection usingConnection = connection;

            if (stopMode == StopMode.Hard)
            {
                usingConnection = mainConnection;
                useExclusiveAccess = true;
            }

            if (connection.IsConnected)
            {
                CommunicationObject comObj = new CommunicationObject(usingConnection, useExclusiveAccess);

                try
                {
                    comObj.SetData(NetSharpProtocol.Commands.STOP_HANDLER, new HandlerStopData(connection.HandlerID, stopMode));
                    await comObj.SendAsync().ConfigureAwait(false);
                }
                finally
                {
                    comObj.Dispose();
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Осуществляет подключение к командному обработчику и выполняет действия
        /// необходимые при первом подключении клиента к серверу.
        /// </summary>
        protected async Task ConnectToCommandHandlerAsync(IPEndPoint endPoint)
        {
            mainConnection = new Connection(endPoint);

            ConnectionCallback callback = new NewConnectionCallback(mainConnection);

            await mainConnection.OpenAsync(callback).ConfigureAwait(false);

            Guid[] ids = (Guid[])callback.Result;

            mainConnection.InitializationOnClient(ids[0], ids[1]);

            ID = ids[0];

            clientListener = new ClientListener(mainConnection, cSource.Token);
        }

        /// <summary>
        /// Осуществляет подключение к какому - либо обработчику, за исключением командного,
        /// и выполняет действия необходимые при подключении от ранее подключившегося клиента.
        /// </summary>
        internal async Task<Connection> ConnectToAnotherHandlerAsync(IPEndPoint endPoint)
        {
            Guid handlerID;
            Connection connection = new Connection(endPoint);

            ConnectionCallback callback = new ExistingClientConnectionCallback(connection, ID);

            await connection.OpenAsync(callback).ConfigureAwait(false);

            handlerID = (Guid)callback.Result;

            connection.InitializationOnClient(ID, handlerID);

            return await Task.FromResult(connection).ConfigureAwait(false);
        }

        /// <summary>
        /// Получает подключение к указанному порту из пула.
        /// Если подключение отсутствует оно будет установлено.
        /// </summary>
        /// <param name="port">Удаленный порт.</param>
        protected async Task<Connection> TakeConnection(int port)
        {
            return await pool.Take(port).ConfigureAwait(false);
        }

        /// <summary>
        /// Возвращает подключение в пул.
        /// </summary>
        protected void ReleaseConnection(Connection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            pool.Release(connection.HandlerID, connection);
        }

        /// <summary>
        /// Выполняет отключение клиента от всех серверных обработчиков, включая командный,
        /// с уведомлением их о завершении работы.
        /// </summary>
        public async Task DisconnectAsync()
        {
            List<Task> tasks = new List<Task>();
            
            cSource.Cancel(true);

            foreach (Connection con in pool)
                if (con.IsConnected)
                    tasks.Add(DisconnectFromHandler(con, StopMode.Soft));

            tasks.Add(DisconnectFromHandler(mainConnection, StopMode.Soft));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public virtual void Dispose()
        {
            cSource.Dispose();
        }

        public class ClientListener
        {
            CancellationToken cToken;
            Connection mainConnection;
            RecordOnce<bool> isStart;
            ConcurrentDictionary<Guid, ConcurrentDictionary<short, Action>> clientHandlersMap;

            public ClientListener(Connection mainConnection, CancellationToken cToken)
            {
                isStart = new RecordOnce<bool>();
                clientHandlersMap = new ConcurrentDictionary<Guid, ConcurrentDictionary<short, Action>>();

                this.mainConnection = mainConnection;
                this.cToken = cToken;
            }

            public void Start()
            {
                if (isStart)
                    return;

                Action clientHandler;
                ConcurrentDictionary<short, Action> handlers;
                Guid handlerID;
                isStart.Value = true;

                Task.Factory.StartNew(async () =>
                {
                    CommunicationObject comObj = new CommunicationObject(mainConnection);

                    while (!cToken.IsCancellationRequested)
                    {
                        await comObj.ReceiveAsync().ConfigureAwait(false);

                        handlerID = comObj.GetData<Guid>();

                        if (clientHandlersMap.TryGetValue(handlerID, out handlers))
                        {
                            if (handlers.TryGetValue(comObj.CommandCode, out clientHandler))
                                clientHandler();
                            else
                                Logger.Write(Source.Client, $"Для команды с кодом {comObj.CommandCode} не зарегистрирован клиентский обработчик.");
                        }
                        else
                        {
                            Logger.Write(Source.Client, $"Для команд серверного обработчика с ID {handlerID} не найден ни один клиентский обработчик.");
                        }
                    }
                });
            }

            public void RegisterClientHandler(Guid handlerID, short commandCode, Action clientHandler)
            {
                if (clientHandler == null)
                    throw new ArgumentNullException(nameof(clientHandler));

                ConcurrentDictionary<short, Action> handlers;

                if (clientHandlersMap.TryGetValue(handlerID, out handlers))
                {
                    handlers.TryAdd(commandCode, clientHandler);
                }
                else
                {
                    handlers = new ConcurrentDictionary<short, Action>();
                    handlers.TryAdd(commandCode, clientHandler);

                    clientHandlersMap.TryAdd(handlerID, handlers);
                }
            }

            public void UnregisterClientHandler(Guid handlerID, short commandCode)
            {
                ConcurrentDictionary<short, Action> handlers;
                Action clientHandler;

                if (clientHandlersMap.TryRemove(handlerID, out handlers))
                {
                    if (!handlers.TryRemove(commandCode, out clientHandler))
                    {
                        throw new ArgumentException($"Ошибка при удалении клиентского обработчика: обработчик для команды с кодом {commandCode} не найден.");
                    }
                }
                else
                {
                    throw new ArgumentException($"Ошибка при удалении клиентского обработчика: для команд серверного обработчика с ID {handlerID} не зарегистрирован ни один такой обработчик.");
                }
            }
        }
    }
}