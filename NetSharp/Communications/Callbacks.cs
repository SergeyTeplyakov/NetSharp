using System;
using System.Threading.Tasks;

namespace NetSharp.Communications.Callbacks
{
    /// <summary>
    /// Базовый класс для классов инкапсулирующих логику иполняемую
    /// непосредственно после подключения клиента к серверу.
    /// </summary>
    public abstract class ConnectionCallback
    {
        protected readonly Connection connection;

        public object Result { get; protected set; }

        protected ConnectionCallback(Connection connection)
        {
            this.connection = connection;
        }

        public abstract Task Invoke();

        protected static void ResponseErrorCheck(string text, short code)
        {
            switch (code)
            {
                case NetSharpProtocol.Errors.CLIENT_NOT_FOUND:
                    throw new CommunicationException($"{text}: клиент c указанным ID не зарегистрирован на сервере.");
                case NetSharpProtocol.Errors.UNKNOWN_CODE:
                    throw new CommunicationException($"{text}: сервер получил неизвестный код команды.");
                case NetSharpProtocol.Errors.HANDLER_NOT_FOUND:
                    throw new CommunicationException($"{text}: на сервере не найден обработчик с заданным ID.");
                default:
                    throw new CommunicationException($"{text}: неизвестный код ошибки. Код ошибки: {code}.");
            }
        }
    }

    /// <summary>
    /// Действия выполняемые при первом подключении клиента к серверу:
    /// отправка запроса на подключение; получение ID созданного на сервере клиента и ID командного обработчика.
    /// </summary>
    class NewConnectionCallback : ConnectionCallback
    {
        public NewConnectionCallback(Connection connection)
            : base(connection)
        {

        }

        public override async Task Invoke()
        {
            Initialization init = new Initialization(connection);

            await init.SendAsyncControlCommand(NetSharpProtocol.Commands.NEW_CLIENT).ConfigureAwait(false);

            await init.ReceiveAsyncControlCommand().ConfigureAwait(false);

            if (init.CommandCode == NetSharpProtocol.Commands.ACCEPT)
                Result = init.GetClientAndHandlerID(); //Получаю ID клиента и ID обработчика.
            else
                throw new CommunicationException("Ошибка при подключении нового клиента к серверу: получен неизвестный код ответа.");
        }
    }

    /// <summary>
    /// Действия выполняемые при подключении от ранее подключившегося клиента:
    /// отправка запроса на подключение; получение ID обработчика выделенного данному подключению.
    /// </summary>
    class ExistingClientConnectionCallback : ConnectionCallback
    {
        Guid clientID;

        public ExistingClientConnectionCallback(Connection connection, Guid clientID)
            : base(connection)
        {
            this.clientID = clientID;
        }

        public override async Task Invoke()
        {
            Initialization init = new Initialization(connection);
            string text = "Ошибка при подключении существующего клиента к серверу";

            await init.SendAsyncClientOrHandlerID(NetSharpProtocol.Commands.NOT_NEW_CLIENT, clientID).ConfigureAwait(false);

            await init.ReceiveAsyncControlCommand().ConfigureAwait(false);

            if (init.CommandCode == NetSharpProtocol.Commands.ACCEPT)
                Result = init.GetClientOrHandlerID(); //Получаю ID обработчика.
            else if (init.CommandCode == NetSharpProtocol.Commands.DENY)
                ResponseErrorCheck(text, init.ErrorCode);
            else
                throw new CommunicationException($"{text}: получен неизвестный код ответа сервера.");
        }
    }

    /// <summary>
    /// Действия выполняемые при подключении после обрыва соединения:
    /// отправка запроса на восстановление связи, который включает в себя ID обработчика
    /// в котором произошла потеря подключения и ID клиента, который содержит данный обработчик.
    /// </summary>
    class ReconnectClientConnectionCallback : ConnectionCallback
    {
        Guid clientID;
        Guid handlerID;

        public ReconnectClientConnectionCallback(Connection connection, Guid clientID, Guid handlerID)
            : base(connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (clientID == Guid.Empty)
                throw new ArgumentException(nameof(clientID));

            if (handlerID == Guid.Empty)
                throw new ArgumentException(nameof(handlerID));

            this.clientID = clientID;
            this.handlerID = handlerID;
        }

        public override async Task Invoke()
        {
            Initialization init = new Initialization(connection);

            string text = "Ошибка при восстановлении подключения";

            await init.SendAsyncClientAndHandlerID(NetSharpProtocol.Commands.RECONNECT, clientID, handlerID).ConfigureAwait(false);

            await init.ReceiveAsyncControlCommand().ConfigureAwait(false);

            if (init.CommandCode == NetSharpProtocol.Commands.DENY)
                ResponseErrorCheck(text, init.ErrorCode);
            else if (init.CommandCode != NetSharpProtocol.Commands.ACCEPT)
                throw new CommunicationException($"{text}: неизвестный код ответа сервера. Код ответа: {init.CommandCode}.");
        }
    }
}
