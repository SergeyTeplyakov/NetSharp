using System;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Threading.Tasks;
using NetSharp.Communications;

namespace NetSharp.ServerSide
{
    using HandlerFactory = Func<Connection, ConnectedClient, ConnectingHandler>;

    /// <summary>
    /// Абстракция порта.
    /// </summary>
    public class Acceptor
    {
        Socket socketServer;
        bool isActive;
        HandlerFactory handlerFactory;
        PreprocessingConnection preprocessingConnection;

        protected readonly int backlog;

        public readonly IPEndPoint LocalIPEndPoint;

        /// <summary>
        /// Конструктор.
        /// </summary>
        /// <param name="address">Локальный адрес на котором будет происходить ожидание подключения.</param>
        /// <param name="port">Порт.</param>
        /// <param name="backlog">Количество подключений в очереде.</param>
        /// <param name="handlerFactory">Фабрика для создания ассоциированного с этим ассептором обработчика,
        /// т.е. такого обработчика который будет обрабатывать полученное этим ассептором подключение.</param>
        public Acceptor(IPAddress address, int port, int backlog, HandlerFactory handlerFactory, Host host)
        {
            LocalIPEndPoint = new IPEndPoint(address, port);
            preprocessingConnection = new PreprocessingConnection(host);
            socketServer = new Socket(LocalIPEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            this.backlog = backlog;
            this.handlerFactory = handlerFactory;
        }

        /// <summary>
        /// Запуск предобработки.
        /// </summary>
        async Task StartPreprocessingConnection(Socket socket)
        {
            await preprocessingConnection.Start(socket, handlerFactory).ConfigureAwait(false);
        }

        void AcceptConnection()
        {
            Task<Socket> waitConnectionTask = socketServer.AcceptTaskAsync();

            waitConnectionTask.ContinueWith(async task =>
            {
                Socket socketClient = task.Result;

                if (task.Exception == null)
                {
                    if (isActive)
                        AcceptConnection();
                    
                    Logger.Write(Source.Server, $"Новое подключение. Удалённая конечная точка: {socketClient.RemoteEndPoint}");

                    await StartPreprocessingConnection(socketClient).ConfigureAwait(false);
                }
            });
        }

        /// <summary>
        /// Запуск прослушивания входящих подключений.
        /// </summary>
        public void Open()
        {
            try
            {
                socketServer.Bind(LocalIPEndPoint);
            }
            catch (SocketException ex)
            {
                throw new AcceptorException("Ошибка при старте ассептора: не удалось связать сокет с указанной конечной точкой.", ex);
            }
            catch (SecurityException ex)
            {
                throw new AcceptorException("Ошибка при старте ассептора: не удалось связать сокет с указанной конечной точкой.", ex);
            }

            try
            {
                socketServer.Listen(backlog);
            }
            catch (SocketException ex)
            {
                throw new AcceptorException("Ошибка при старте ассептора: не удалось перевести сокет в состояние прослушивания.", ex);
            }

            isActive = true;

            Logger.Write(Source.Server, $"Порт открыт. Локальная конечная точка: {LocalIPEndPoint}");

            AcceptConnection();
        }

        public void Close()
        {
            isActive = false;

            if (socketServer != null)
                socketServer.Close();
        }
    }
}
