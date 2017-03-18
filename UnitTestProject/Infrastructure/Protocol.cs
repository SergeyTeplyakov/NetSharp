public static class Protocol
{
    public static class Commands
    {
        /// <summary>
        /// Передача файла.
        /// </summary>
        public const int UPLOAD_FILE = 1;
    }

    public static class Errors
    {
        /// <summary>
        /// Для записи файла нет достаточного места.
        /// </summary>
        public const int FT_NOT_ENOUGH_SPACE = 1;
        /// <summary>
        /// Любая другая ошибка связанная с чтением или записью файла.
        /// </summary>
        public const int FT_OTHER_ERROR = 2;
    }
}