
namespace NetSharp
{
    public static class NetSharpProtocol
    {
        public static class Commands
        {
            /// <summary>
            /// Действие разрешено.
            /// </summary>
            public const short ACCEPT = 1;
            /// <summary>
            /// Действие запрещено.
            /// </summary>
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
            /// Остановка обработчика с указанным ID.
            /// </summary>
            public const short STOP_HANDLER = 7;
            /// <summary>
            /// Восстановление подключения после обрыва связи.
            /// </summary>
            public const short RECONNECT = 8;
            /// <summary>
            /// Отмена текущего действия.
            /// </summary>
            public const short CANCEL = 9;
        }

        public static class Errors
        {
            /// <summary>
            /// Неизвестный код команды.
            /// </summary>
            public const int UNKNOWN_CODE = 1;
            /// <summary>
            /// Клиент с указанным ID не найден.
            /// </summary>
            public const int CLIENT_NOT_FOUND = 2;
            /// <summary>
            /// Обработчик с указанным ID не найден.
            /// </summary>
            public const int HANDLER_NOT_FOUND = 3;
        }
    }
}
