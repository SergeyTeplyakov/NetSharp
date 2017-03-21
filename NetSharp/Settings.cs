namespace NetSharp
{
    // Это должен быть экземплярный класс, а не статический
    // Настройки должны приниматься в коснтрукторе.
    // Сейчас ничего настроить без перекомпиляции нельзя.
    static class Settings
    {
        // Неверная идиома именвавния. Так в дот-нете не именюут.
        public const byte COUNT_RECONNECTION_ATTEMPTS_MAX = 20;
        public const byte COUNT_TRANSFER_DATA_ATTEMPTS_MAX = 10;
        public const byte CHECK_INTERVAL = 1;
        public const byte CONNECT_TIME_WAITING = 10;
        public const byte TIME_WAITING_RECONNECT = 10;
    }
}
