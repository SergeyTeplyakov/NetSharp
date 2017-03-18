
namespace NetSharp
{
    public static class NetSharpProtocol
    {
        public static class Commands
        {
            public const short ACCEPT = 1;
            public const short DENY = 2;
            /// <summary>
            /// Подключение нового клиента.
            /// </summary>
            public const short NEW_CLIENT = 3;
            /// <summary>
            /// Подключение от уже существующего клиента.
            /// </summary>
            public const short NOT_NEW_CLIENT = 4;
            /// <summary>
            /// ID клиента.
            /// </summary>
            public const short CLIENT_ID = 5;
            /// <summary>
            /// ID обработчика.
            /// </summary>
            public const short HANDLER_ID = 6;
            /// <summary>
            /// Останавливает обработчик с указанным ID.
            /// </summary>
            public const short STOP_HANDLER = 7;
            /// <summary>
            /// Заголовок и данные сообщения получены без ошибок.
            /// </summary>
            public const short NO_ERROR = 8;
            /// <summary>
            /// Заголовок получен с ошибками.
            /// </summary>
            public const short HEADER_ERROR = 9;
            /// <summary>
            /// Данные получены с ошибками.
            /// </summary>
            public const short DATA_ERROR = 10;
            /// <summary>
            /// Восстановление подключения после обрыва связи.
            /// </summary>
            public const short RECONNECT = 11;
            /// <summary>
            /// Отмена текущего действия.
            /// </summary>
            public const short CANCEL = 12;
        }

        public static class Errors
        {
            public const int UNKNOWN_CODE = 1;
            public const int CLIENT_NOT_FOUND = 2;
            public const int HANDLER_NOT_FOUND = 3;
        }
    }
}
