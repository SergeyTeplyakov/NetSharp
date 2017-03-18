using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetSharp.Communications;

namespace NetSharp.ServerSide
{
    /// <summary>
    /// Инкапсулирует логику первичной обработки команд от клиента к серверу
    /// и запуск соотвествующих им действий.
    /// </summary>
    class PreprocessingConnection
    {
        readonly Host host;

        public PreprocessingConnection(Host host)
        {
            this.host = host;
        }

        /// <summary>
        /// Начинает предобработку нового подключения.
        /// </summary>
        /// <param name="handlerFactory">Фабрика для создания обработчика данного подключения.</param>
        public void Start(Socket socket, Func<Connection, ConnectedClient, ConnectingHandler> handlerFactory)
        {
            ConnectingHandler handler = null;
            ConnectedClient client = null;
            Connection connection = new Connection(socket);

            Task.Factory.StartNew(async () =>
            {
                try
                {
                    Initialization init = new Initialization(connection);

                    await init.ReceiveAsyncControlCommand().ConfigureAwait(false);

                    Logger.Write(Source.Server, $"Начата предобработка нового подключения. Получен код команды: {init.CommandCode}");

                    switch (init.CommandCode)
                    {
                        case NetSharpProtocol.Commands.NEW_CLIENT:
                            {
                                client = host.ClientManager.CreateNewClient();

                                handler = handlerFactory(connection, client);
                                connection.InitializationOnServer(client.ID, handler.ID, handler.Name, client.ReconnectManager);
                                client.AddHandler(handler);

                                Logger.Write(Source.Server, $"Создан новый клиент. Удалённый адрес: {connection.RemoteEndPoint}",
                                    data: Data.Create().
                                    SetClientID(client.ID).
                                    SetHandlerData(handler.ID, handler.Name));

                                await init.SendAsyncClientAndHandlerID(NetSharpProtocol.Commands.ACCEPT, client.ID, handler.ID).ConfigureAwait(false);

                                await handler.StartService().ConfigureAwait(false);
                            }
                            break;

                        case NetSharpProtocol.Commands.NOT_NEW_CLIENT:
                            {
                                Guid clientID = init.GetClientOrHandlerID();

                                client = host.ClientManager.GetClientByID(clientID);

                                if (client != null)
                                {
                                    handler = handlerFactory(connection, client);
                                    connection.InitializationOnServer(client.ID, handler.ID, handler.Name, client.ReconnectManager);
                                    client.AddHandler(handler);

                                    Logger.Write(Source.Server, "Добавлен обработчик.",
                                        data: Data.Create().
                                        SetClientID(client.ID).
                                        SetHandlerData(handler.ID, handler.Name));

                                    await init.SendAsyncClientOrHandlerID(NetSharpProtocol.Commands.ACCEPT, handler.ID).ConfigureAwait(false);

                                    await handler.StartService().ConfigureAwait(false);
                                }
                                else
                                {
                                    Logger.Write(Source.Server, $"Новое подключение от уже подключённого клиента. Ошибка: клиент с указанным ID не найден на сервере.",
                                        data: Data.Create().SetClientID(clientID));

                                    await init.SendAsyncControlCommand(NetSharpProtocol.Commands.DENY, NetSharpProtocol.Errors.CLIENT_NOT_FOUND).ConfigureAwait(false);
                                }
                            }
                            break;

                        case NetSharpProtocol.Commands.RECONNECT:
                            {
                                Guid[] ids = init.GetClientAndHandlerID();

                                client = host.ClientManager.GetClientByID(ids[0]);

                                if (client != null)
                                {
                                    if (client.ReconnectManager.ProcessNewConnection(ids[1], socket))
                                    {
                                        Logger.Write(Source.Server, $"Подключение для обработчика с ID {ids[1]} успешно восстановлено.");

                                        await init.SendAsyncControlCommand(NetSharpProtocol.Commands.ACCEPT).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        Logger.Write(Source.Server, $"Запрос на восстановление подключения не может быть выполнен, т.к. обработчик с ID {ids[1]} не зарегистрирован как ожидающий восстановления подключения или был удалён по истечении временного интервала.");

                                        await init.SendAsyncControlCommand(NetSharpProtocol.Commands.DENY,
                                            NetSharpProtocol.Errors.HANDLER_NOT_FOUND).ConfigureAwait(false);
                                    }
                                }
                                else
                                {
                                    Logger.Write(Source.Server, $"Запрос на восстановление подключения не может быть выполнен, т.к. клиент с ID {ids[1]} не найден.");

                                    await init.SendAsyncControlCommand(NetSharpProtocol.Commands.DENY,
                                        NetSharpProtocol.Errors.CLIENT_NOT_FOUND).ConfigureAwait(false);
                                }
                            }
                            break;

                        default:
                            {
                                await init.SendAsyncControlCommand(NetSharpProtocol.Commands.DENY, NetSharpProtocol.Errors.UNKNOWN_CODE).ConfigureAwait(false);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    if (handler == null)
                    {
                        Logger.Write(Source.Server, "Исключение в задаче обработчика. Обработчик не был запущен.", ex);
                        connection.Close();
                    }
                    else
                    {
                        Type exType = ex.GetType();

                        if ((exType == typeof(ObjectDisposedException) || exType == typeof(CommunicationException)) && handler.HardStop)
                        {
                            Logger.Write(Source.Server, $"Обработчик завершил работу. Причина: выполнена жесткая остановка.",
                                    data: Data.Create().SetHandlerData(handler.ID, handler.Name));
                        }
                        else
                        {
                            Logger.Write(Source.Server, "Исключение в задаче обработчика. Обработчик завершил работу.", ex,
                                Data.Create().SetHandlerData(handler.ID, handler.Name));
                        }

                        client.RemoveHandler(handler.ID);
                    }

                    return;
                }

                Logger.Write(Source.Server, $"Обработчик завершил работу. Причина: нормальное завершение.",
                    data: Data.Create().SetHandlerData(handler.ID, handler.Name));

                client.RemoveHandler(handler.ID);
            });
        }
    }
}